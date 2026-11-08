import { parseFinancialJson } from './decimal.mjs';
let token = '';
export async function api<T = any>(path: string, method = 'GET', body?: unknown): Promise<T> {
  if (method !== 'GET' && !token) {
    const r = await fetch('/api/auth/csrf', { credentials: 'same-origin' });
    if (!r.ok) throw new Error('Conexiunea cu serverul a eșuat. / Server connection failed.');
    token = (await r.json()).token;
  }
  const headers: Record<string, string> = { 'X-Money-Format': 'exact-v1' };
  if (method !== 'GET') {
    headers['X-CSRF-TOKEN'] = token;
    headers['Idempotency-Key'] = crypto.randomUUID();
  }
  if (body && !(body instanceof FormData)) headers['Content-Type'] = 'application/json';
  const response = await fetch('/api' + path, {
    method,
    headers,
    credentials: 'same-origin',
    body: body instanceof FormData ? body : body === undefined ? undefined : JSON.stringify(body),
  });
  if (!response.ok) {
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
  if (['/auth/login', '/auth/logout', '/auth/change-password'].includes(path)) token = '';
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
      } else
        reject(
          new Error(
            result[localStorage.getItem('decont.language') || 'ro'] ||
              result.code ||
              xhr.statusText,
          ),
        );
    };
    xhr.send(body);
  });
}
