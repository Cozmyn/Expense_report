import { parseFinancialJson } from './decimal.mjs';
let token = '';
// Keep an operation key after an ambiguous transport/server failure. Retrying the
// same command in this tab must recover the server's committed response instead of
// creating a second receipt, export or notification. Never persist payloads locally.
const unresolvedMutations = new Map<string, { key: string; until: number }>();
const mutationLifetimeMs = 24 * 60 * 60 * 1000;
const operationPrefix = 'decont.operation.';
export type ApiOptions = { operationId?: string; signal?: AbortSignal };
export function newOperationId(): string { return crypto.randomUUID(); }

function persistedOperation(signature: string): { key: string; until: number } | null {
  try {
    if (typeof sessionStorage === 'undefined') return null;
    const value = sessionStorage.getItem(operationPrefix + signature);
    if (!value) return null;
    const found = JSON.parse(value);
    if (typeof found.key === 'string' && /^[0-9a-f-]{36}$/i.test(found.key)
        && typeof found.until === 'number' && found.until > Date.now())
      return found;
    sessionStorage.removeItem(operationPrefix + signature);
  } catch { /* Storage may be disabled by browser privacy policy. */ }
  return null;
}
function rememberOperation(signature: string, value: { key: string; until: number }) {
  unresolvedMutations.set(signature, value);
  try {
    if (typeof sessionStorage !== 'undefined')
      sessionStorage.setItem(operationPrefix + signature, JSON.stringify(value));
  } catch { /* In-memory retry remains safe within the current tab. */ }
}
function releaseOperation(signature: string) {
  unresolvedMutations.delete(signature);
  try {
    if (typeof sessionStorage !== 'undefined')
      sessionStorage.removeItem(operationPrefix + signature);
  } catch {}
}
async function mutationKey(path: string, method: string,
    payload: string | undefined, explicit?: string) {
  if (explicit) return { key: explicit, signature: '' };
  if (path.startsWith('/auth/')) return { key: newOperationId(), signature: '' };
  // Persist only the *digest* of a mutation identity and its random UUID.
  // Never place names, amounts, dates or whole request bodies into browser
  // storage. Replaying an uncertain request after a tab reload uses the same ID.
  const material = new TextEncoder().encode(method + '\\u0000' + path + '\\u0000' + (payload ?? ''));
  const hash = await crypto.subtle.digest('SHA-256', material);
  const signature = Array.from(new Uint8Array(hash)).map(b => b.toString(16).padStart(2, '0')).join('');
  const now = Date.now();
  for (const [id, value] of unresolvedMutations)
    if (value.until <= now) releaseOperation(id);
  const prior = unresolvedMutations.get(signature) || persistedOperation(signature);
  if (prior) {
    rememberOperation(signature, prior);
    return { key: prior.key, signature };
  }
  if (unresolvedMutations.size >= 64)
    releaseOperation(unresolvedMutations.keys().next().value!);
  const key = newOperationId();
  rememberOperation(signature, { key, until: now + mutationLifetimeMs });
  return { key, signature };
}
function mutationPayloadIdentity(body: unknown): string | undefined {
  if (body === undefined) return undefined;
  if (!(body instanceof FormData)) return JSON.stringify(body);
  // Avoid treating every multipart upload on the same path as the *same*
  // operation. File metadata is stable on retry and does not require reading
  // a 100-MiB upload into RAM to hash it. The server still verifies full
  // request bytes and rejects any accidental metadata collision as a 409.
  return JSON.stringify(Array.from(body.entries(), ([name, value]) =>
    typeof value === 'string'
      ? [name, 'text', value]
      : [name, 'file', value.name, value.size, value.type, value.lastModified]
  ));
}
/** Tell the authenticated workspace to offer a safe return to sign-in on a 401. */
export const SESSION_EXPIRED_EVENT = 'decont:session-expired';
function handleExpiredSession(path: string, status: number): boolean {
  const expired =
    status === 401 &&
    !['/me', '/auth/login', '/auth/logout', '/auth/csrf'].includes(path);
  if (expired) {
    token = '';
    window.dispatchEvent(new Event(SESSION_EXPIRED_EVENT));
  }
  return expired;
}
function expiredSessionMessage(): string {
  return localStorage.getItem('decont.language') === 'en'
    ? 'Session expired. Sign in again.'
    : 'Sesiunea a expirat. Autentifică-te din nou.';
}
export async function api<T = any>(path: string, method = 'GET', body?: unknown, options: ApiOptions = {}): Promise<T> {
  if (method !== 'GET' && !token) {
    const r = await fetch('/api/auth/csrf', { credentials: 'same-origin' });
    if (!r.ok) throw new Error('Conexiunea cu serverul a eșuat. / Server connection failed.');
    token = (await r.json()).token;
  }
  const headers: Record<string, string> = { 'X-Money-Format': 'exact-v1' };
  const payload = body instanceof FormData ? undefined : mutationPayloadIdentity(body);
  const operation = method !== 'GET'
    ? await mutationKey(path, method, mutationPayloadIdentity(body), options.operationId) : null;
  if (operation) {
    headers['X-CSRF-TOKEN'] = token;
    headers['Idempotency-Key'] = operation.key;
  }
  if (body && !(body instanceof FormData)) headers['Content-Type'] = 'application/json';
  const response = await fetch('/api' + path, {
    method,
    headers,
    credentials: 'same-origin',
    signal: options.signal,
    body: body instanceof FormData ? body : payload,
  });
  if (!response.ok) {
    // A timeout, server error, or serializable/unique-key race may occur
    // after another worker committed this same mutation. Keep the operation
    // key until the exact committed result can be replayed.
    if (handleExpiredSession(path, response.status)) throw new Error(expiredSessionMessage());
    const p = await response
      .json()
      .catch(() => ({ ro: 'Server indisponibil.', en: 'Server unavailable.' }));
    const uncertain = response.status >= 500 || response.status === 408
      || response.status === 429
      || (response.status === 409 && p.code === 'concurrent_write');
    if (operation?.signature && !uncertain) releaseOperation(operation.signature);
    const lang = localStorage.getItem('decont.language') || 'ro';
    let message = p[lang] || p.code || response.statusText;
    try {
      const issues = JSON.parse(message);
      if (Array.isArray(issues)) message = issues.map((i) => i[lang]).join('\n');
    } catch {}
    throw new Error(message);
  }
  const result = response.status === 204 ? (undefined as T) : parseFinancialJson(await response.text());
  if (operation?.signature) releaseOperation(operation.signature);
  if (['/auth/login', '/auth/logout', '/auth/change-password'].includes(path)) token = '';
  return result;
}
export async function upload(
  path: string,
  body: FormData,
  progress: (percent: number) => void,
  headers: Record<string, string> = {},
  key = crypto.randomUUID(),
) {
  if (!token) {
    const result = await fetch('/api/auth/csrf', { credentials: 'same-origin' });
    if (!result.ok) throw new Error('Conexiunea cu serverul a eșuat. / Server connection failed.');
    token = (await result.json()).token;
  }
  return new Promise<any>((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open('POST', '/api' + path);
    xhr.withCredentials = true;
    xhr.setRequestHeader('X-Money-Format', 'exact-v1');
    xhr.setRequestHeader('X-CSRF-TOKEN', token);
    xhr.setRequestHeader('Idempotency-Key', key);
    Object.entries(headers).forEach(([name, value]) => xhr.setRequestHeader(name, value));
    xhr.upload.onprogress = (event) => {
      if (event.lengthComputable) progress(Math.round((event.loaded / event.total) * 100));
    };
    xhr.onerror = () => reject(new Error('Încărcarea a eșuat. / Upload failed.'));
    xhr.onload = () => {
      // Some session-expiry responses are empty; notify before parsing their body.
      if (handleExpiredSession(path, xhr.status)) {
        reject(new Error(expiredSessionMessage()));
        return;
      }
      let result;
      try {
        result = parseFinancialJson(xhr.responseText);
      } catch {
        reject(new Error('Răspuns invalid. / Invalid response.'));
        return;
      }
      if (xhr.status >= 200 && xhr.status < 300) {
        progress(100);
        resolve(result);
      } else {
        reject(
          new Error(
            result[localStorage.getItem('decont.language') || 'ro'] ||
              result.code ||
              xhr.statusText,
          ),
        );
      }
    };
    xhr.send(body);
  });
}
