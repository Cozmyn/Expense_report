import { parseFinancialJson } from './decimal.mjs';
let token = '';
// A failed transport is not proof that the server did not commit a mutation.
// Reuse the same idempotency key for manual retries of the *identical* request.
// Keep these keys only in memory: never persist sensitive request bodies.
const uncertainMutations = new Map<string, string>();
export type MutationOptions = { operationId?: string; signal?: AbortSignal };
/** Tell the authenticated workspace to offer a safe return to sign-in on a 401. */
export const SESSION_EXPIRED_EVENT = 'decont:session-expired';
function handleExpiredSession(path: string, status: number): boolean {
  const expired =
    status === 401 &&
    !['/me', '/auth/login', '/auth/logout', '/auth/csrf'].includes(path);
  if (expired) {
    token = '';
    uncertainMutations.clear();
    window.dispatchEvent(new Event(SESSION_EXPIRED_EVENT));
  }
  return expired;
}
function expiredSessionMessage(): string {
  return localStorage.getItem('decont.language') === 'en'
    ? 'Session expired. Sign in again.'
    : 'Sesiunea a expirat. Autentifică-te din nou.';
}
export async function api<T = any>(
  path: string,
  method = 'GET',
  body?: unknown,
  options: MutationOptions = {},
): Promise<T> {
  if (method !== 'GET' && !token) {
    const r = await fetch('/api/auth/csrf', { credentials: 'same-origin' });
    if (!r.ok) throw new Error('Conexiunea cu serverul a eșuat. / Server connection failed.');
    token = (await r.json()).token;
  }
  const payload = body instanceof FormData ? body : body === undefined ? undefined : JSON.stringify(body);
  const isMutation = method !== 'GET';
  // FormData uploads use upload() with a caller-provided operation key.
  const signature = isMutation && !(body instanceof FormData)
    ? method + '\n' + path + '\n' + (payload ?? '')
    : null;
  const operationId = isMutation
    ? options.operationId || (signature && uncertainMutations.get(signature)) || crypto.randomUUID()
    : null;
  if (signature && operationId) uncertainMutations.set(signature, operationId);
  const headers: Record<string, string> = { 'X-Money-Format': 'exact-v1' };
  if (operationId) {
    headers['X-CSRF-TOKEN'] = token;
    headers['Idempotency-Key'] = operationId;
  }
  if (body && !(body instanceof FormData)) headers['Content-Type'] = 'application/json';
  // If fetch rejects (offline, lost reply, aborted request), deliberately retain
  // the operation key: the caller may retry without creating a duplicate.
  const response = await fetch('/api' + path, {
    method,
    headers,
    credentials: 'same-origin',
    body: payload,
    signal: options.signal,
  });
  // Successful or deterministic client-error responses terminate the command.
  // A server failure can occur after a commit and therefore stays retryable.
  if (signature && (response.ok || (response.status >= 400 && response.status < 500)))
    uncertainMutations.delete(signature);
  if (!response.ok) {
    if (handleExpiredSession(path, response.status)) throw new Error(expiredSessionMessage());
    const p = await response
      .json()
      .catch(() => ({ ro: 'Server indisponibil.', en: 'Server unavailable.' }));
    const lang = localStorage.getItem('decont.language') || 'ro';
    let message = p[lang] || p.code || response.statusText;
    try {
      const issues = JSON.parse(message);
      if (Array.isArray(issues)) message = issues.map((i) => i[lang]).join('\n');
    } catch {}
    throw new Error(message);
  }
  if (['/auth/login', '/auth/logout', '/auth/change-password'].includes(path)) {
    token = '';
    uncertainMutations.clear();
  }
  return response.status === 204 ? (undefined as T) : parseFinancialJson(await response.text());
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
