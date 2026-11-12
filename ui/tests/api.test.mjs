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


test('Lost response reuses exactly the same mutation key on a manual retry', async () => {
  const writes = [];
  globalThis.fetch = async (url, options) => {
    if (url.endsWith('/csrf')) return Response.json({ token: 'token' });
    writes.push({ key: options.headers['Idempotency-Key'], body: options.body });
    if (writes.length === 1) throw new TypeError('Network disconnected after commit');
    return Response.json({ id: 'same-server-result' });
  };
  const { api } = await fresh();
  const payload = { purpose: 'Trip', version: 3 };
  await assert.rejects(() => api('/reports', 'POST', payload), /Network disconnected/);
  assert.deepEqual(await api('/reports', 'POST', payload), { id: 'same-server-result' });
  assert.equal(writes.length, 2);
  assert.equal(writes[0].key, writes[1].key);
  assert.equal(writes[0].body, writes[1].body);
});

test('Different payloads or completed operations receive different keys', async () => {
  const keys = [];
  globalThis.fetch = async (url, options) => {
    if (url.endsWith('/csrf')) return Response.json({ token: 'token' });
    keys.push(options.headers['Idempotency-Key']);
    return Response.json({ ok: true });
  };
  const { api } = await fresh();
  await api('/reports', 'POST', { purpose: 'A' });
  await api('/reports', 'POST', { purpose: 'B' });
  await api('/reports', 'POST', { purpose: 'A' });
  assert.equal(new Set(keys).size, 3);
});

test('Explicit operation ID is propagated on an identical manual retry', async () => {
  const seen = [];
  globalThis.fetch = async (url, options) => {
    if (url.endsWith('/csrf')) return Response.json({ token: 'token' });
    seen.push(options.headers['Idempotency-Key']);
    return Response.json({ ro: 'Indisponibil', en: 'Unavailable' }, { status: 503 });
  };
  const { api } = await fresh();
  await assert.rejects(() => api('/reports', 'POST', { purpose: 'A' }, {
    operationId: 'request-from-confirmation-screen-0001'
  }), /Unavailable/);
  await assert.rejects(() => api('/reports', 'POST', { purpose: 'A' }), /Unavailable/);
  assert.deepEqual(seen, [
    'request-from-confirmation-screen-0001',
    'request-from-confirmation-screen-0001',
  ]);
});

test('Client rejection clears the pending operation rather than reusing an invalid key', async () => {
  const seen = [];
  globalThis.fetch = async (url, options) => {
    if (url.endsWith('/csrf')) return Response.json({ token: 'token' });
    seen.push(options.headers['Idempotency-Key']);
    return Response.json({ en: 'Validation failed' }, { status: 422 });
  };
  const { api } = await fresh();
  await assert.rejects(() => api('/reports', 'POST', { purpose: 'A' }), /Validation failed/);
  await assert.rejects(() => api('/reports', 'POST', { purpose: 'A' }), /Validation failed/);
  assert.notEqual(seen[0], seen[1]);
});

test('Interrupted successful response body remains retryable with the original key', async () => {
  const seen = [];
  globalThis.fetch = async (url, options) => {
    if (url.endsWith('/csrf')) return Response.json({ token: 'token' });
    seen.push(options.headers['Idempotency-Key']);
    if (seen.length === 1)
      return { ok: true, status: 200, text: async () => { throw new Error('Response cut off'); } };
    return Response.json({ id: 'committed' });
  };
  const { api } = await fresh();
  await assert.rejects(() => api('/reports', 'POST', { purpose: 'Review' }), /Response cut off/);
  assert.deepEqual(await api('/reports', 'POST', { purpose: 'Review' }), { id: 'committed' });
  assert.equal(seen[0], seen[1]);
});
