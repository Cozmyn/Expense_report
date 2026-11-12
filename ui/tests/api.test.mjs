import test from 'node:test';
import assert from 'node:assert/strict';
import ts from 'typescript';
import { readFileSync } from 'node:fs';
globalThis.localStorage = { getItem: () => 'en' };
async function fresh() {
  const source = readFileSync(new URL('../src/api.ts', import.meta.url), 'utf8').replace(
    "'./decimal.mjs'",
    JSON.stringify(new URL('../src/decimal.mjs', import.meta.url).href),
  );
  const js = ts.transpile(source, { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ES2022 });
  return import(
    'data:text/javascript;base64,' + Buffer.from(js + '\n//' + Math.random()).toString('base64')
  );
}
test('Mutation obtains CSRF cookie token and sends a unique operation key', async () => {
  const calls = [];
  globalThis.fetch = async (url, options) => {
    calls.push({ url, options });
    return Response.json(url.endsWith('/csrf') ? { token: 'real-token' } : { id: 'saved' });
  };
  const { api } = await fresh();
  const result = await api('/reports', 'POST', { purpose: 'Visit' });
  assert.equal(result.id, 'saved');
  assert.equal(calls[0].url, '/api/auth/csrf');
  assert.equal(calls[1].options.headers['X-CSRF-TOKEN'], 'real-token');
  assert.match(calls[1].options.headers['Idempotency-Key'], /^[a-f0-9-]{36}$/);
  assert.equal(calls[1].options.credentials, 'same-origin');
});
test('Permission rejection is shown in the selected language', async () => {
  globalThis.fetch = async () =>
    Response.json({ ro: 'Acces interzis', en: 'Access denied' }, { status: 403 });
  const { api } = await fresh();
  await assert.rejects(() => api('/reports'), /Access denied/);
});
test('Validation issue arrays become readable messages', async () => {
  globalThis.fetch = async () =>
    Response.json(
      { en: JSON.stringify([{ en: 'Missing receipt' }, { en: 'Missing category' }]) },
      { status: 422 },
    );
  const { api } = await fresh();
  await assert.rejects(() => api('/reports'), /Missing receipt\nMissing category/);
});
test('Failed CSRF bootstrap prevents submitting the mutation', async () => {
  let calls = 0;
  globalThis.fetch = async () => {
    calls++;
    return new Response('', { status: 503 });
  };
  const { api } = await fresh();
  await assert.rejects(() => api('/reports', 'POST', {}), /Server connection failed/);
  assert.equal(calls, 1);
});
test('Login refreshes the CSRF token for the authenticated identity', async () => {
  let csrf = 0;
  const mutations = [];
  globalThis.fetch = async (url, options) => {
    if (url.endsWith('/csrf')) return Response.json({ token: 'token-' + ++csrf });
    mutations.push(options.headers['X-CSRF-TOKEN']);
    return Response.json({ ok: true });
  };
  const { api } = await fresh();
  await api('/auth/login', 'POST', { username: 'alice', password: 'secret' });
  await api('/reports', 'POST', { purpose: 'Visit' });
  assert.deepEqual(mutations, ['token-1', 'token-2']);
});

test('Authenticated session expiry raises a single workspace event', async () => {
  const events = [];
  globalThis.window = { dispatchEvent: (event) => events.push(event.type) };
  globalThis.fetch = async () =>
    Response.json({ ro: 'Sesiunea a expirat', en: 'Session expired' }, { status: 401 });
  const { api, SESSION_EXPIRED_EVENT } = await fresh();
  await assert.rejects(() => api('/reports'), /Session expired/);
  assert.deepEqual(events, [SESSION_EXPIRED_EVENT]);
});

test('Initial unauthenticated check and incorrect login never claim an expired session', async () => {
  const events = [];
  globalThis.window = { dispatchEvent: (event) => events.push(event.type) };
  globalThis.fetch = async (url) => url.endsWith('/csrf')
    ? Response.json({ token: 'unit-test-token' })
    : Response.json({ en: 'Please sign in' }, { status: 401 });
  const { api } = await fresh();
  await assert.rejects(() => api('/me'), /Please sign in/);
  await assert.rejects(() => api('/auth/login', 'POST', { username: 'someone', password: 'wrong' }));
  assert.equal(events.length, 0);
});

test('Empty-body 401 during receipt upload still reports the expired session', async () => {
  const events = [];
  globalThis.window = { dispatchEvent: (event) => events.push(event.type) };
  globalThis.fetch = async () => Response.json({ token: 'unit-test-csrf' });
  globalThis.XMLHttpRequest = class {
    constructor() {
      this.upload = {};
      this.status = 401;
      this.statusText = 'Unauthorized';
      this.responseText = '';
    }
    open() {}
    setRequestHeader() {}
    send() { this.onload?.(); }
  };
  const { upload, SESSION_EXPIRED_EVENT } = await fresh();
  await assert.rejects(
    () => upload('/reports/test/documents', new FormData(), () => {}),
    /Session expired\. Sign in again\./,
  );
  assert.deepEqual(events, [SESSION_EXPIRED_EVENT]);
});

test('Ambiguous network failure reuses the same key and successful replay clears it', async () => {
  const keys = [];
  let attempt = 0;
  globalThis.fetch = async (url, options) => {
    if (url.endsWith('/csrf')) return Response.json({ token: 'csrf' });
    keys.push(options.headers['Idempotency-Key']);
    if (++attempt === 1) throw new TypeError('Network response lost');
    return Response.json({ id: 'existing-receipt' });
  };
  const { api } = await fresh();
  await assert.rejects(() => api('/reports/42/lines', 'POST', { amount: '10.00' }), /Network response lost/);
  const replay = await api('/reports/42/lines', 'POST', { amount: '10.00' });
  assert.equal(replay.id, 'existing-receipt');
  await api('/reports/42/lines', 'POST', { amount: '10.00' });
  assert.equal(keys[0], keys[1]);
  assert.notEqual(keys[1], keys[2]);
});
test('A transient server error preserves the operation key until successful replay', async () => {
  const keys = [];
  let count = 0;
  globalThis.fetch = async (url, options) => {
    if (url.endsWith('/csrf')) return Response.json({ token: 'csrf' });
    keys.push(options.headers['Idempotency-Key']);
    return ++count === 1
      ? Response.json({ en: 'Temporary' }, { status: 503 })
      : Response.json({ ok: true });
  };
  const { api } = await fresh();
  await assert.rejects(() => api('/reports', 'POST', { purpose: 'Unique purpose' }), /Temporary/);
  await api('/reports', 'POST', { purpose: 'Unique purpose' });
  assert.equal(keys[0], keys[1]);
});
test('Caller-supplied operation ID remains unchanged for an explicit replay', async () => {
  const keys = [];
  globalThis.fetch = async (url, options) => {
    if (url.endsWith('/csrf')) return Response.json({ token: 'csrf' });
    keys.push(options.headers['Idempotency-Key']);
    return Response.json({ ok: true });
  };
  const { api, newOperationId } = await fresh();
  const id = newOperationId();
  await api('/reports', 'POST', { purpose: 'Journey' }, { operationId: id });
  await api('/reports', 'POST', { purpose: 'Journey' }, { operationId: id });
  assert.deepEqual(keys, [id, id]);
});

test('Unknown-commit mutation key survives reload without storing any financial request payload', async () => {
  const storage = new Map();
  globalThis.sessionStorage = {
    getItem: key => storage.get(key) ?? null,
    setItem: (key, value) => storage.set(key, value),
    removeItem: key => storage.delete(key),
  };
  const keys = [];
  let attempt = 0;
  globalThis.fetch = async (url, options) => {
    if (url.endsWith('/csrf')) return Response.json({ token: 'csrf' });
    keys.push(options.headers['Idempotency-Key']);
    if (++attempt === 1) throw new TypeError('Lost committed response');
    return Response.json({ receiptId: 'original' });
  };
  const first = await fresh();
  const body = { amount: '987654.32', privateDescription: 'NOT_TO_BE_PERSISTED' };
  await assert.rejects(() => first.api('/reports/42/lines', 'POST', body), /Lost/);
  assert.equal(storage.size, 1);
  assert.ok([...storage.keys(), ...storage.values()].every(text =>
    !String(text).includes('NOT_TO_BE_PERSISTED') && !String(text).includes('987654.32')));
  const second = await fresh();
  assert.deepEqual(await second.api('/reports/42/lines', 'POST', body),
    { receiptId: 'original' });
  assert.equal(keys[0], keys[1]);
  assert.equal(storage.size, 0);
  delete globalThis.sessionStorage;
});

test('A 409 serializable commit race preserves the exact idempotency key for replay', async () => {
  const keys = [];
  let tries = 0;
  globalThis.fetch = async (url, options) => {
    if (url.endsWith('/csrf')) return Response.json({ token: 'csrf' });
    keys.push(options.headers['Idempotency-Key']);
    return ++tries === 1
      ? Response.json({ code: 'concurrent_write', en: 'Concurrent update' }, { status: 409 })
      : Response.json({ id: 'original-financial-row' });
  };
  const { api } = await fresh();
  await assert.rejects(() => api('/reports/42/lines', 'POST', { amount: '12.34' }), /Concurrent update/);
  assert.deepEqual(await api('/reports/42/lines', 'POST', { amount: '12.34' }),
    { id: 'original-financial-row' });
  assert.equal(keys[0], keys[1]);
});
