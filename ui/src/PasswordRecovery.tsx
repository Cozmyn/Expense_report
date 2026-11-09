import React, { useState } from 'react';
import { api } from './api';
import { Field } from './Fields';
export default function PasswordRecovery({ t }: any) {
  const [open, O] = useState(false),
    [username, U] = useState(''),
    [password, P] = useState(''),
    [message, M] = useState(''),
    [busy, B] = useState(false);
  const params = new URLSearchParams(location.search);
  const token = params.get('resetToken'),
    userId = params.get('resetUser');
  return (
    <div className="password-recovery">
      {!token && !open ? (
        <button className="text-button" type="button" onClick={() => O(true)}>
          {t('Ai uitat parola?', 'Forgot password?')}
        </button>
      ) : (
        <form
          onSubmit={async (e) => {
            e.preventDefault();
            B(true);
            try {
              if (token && userId) {
                await api('/auth/reset-password', 'POST', { userId, token, password });
                history.replaceState(null, '', location.pathname);
                M(
                  t(
                    'Parola a fost schimbată. Autentifică-te cu noua parolă.',
                    'Password changed. Sign in with the new password.',
                  ),
                );
                O(false);
              } else {
                const result = await api('/auth/forgot-password', 'POST', { username });
                M(t(result.ro, result.en));
              }
            } catch (error) {
              M((error as Error).message);
            } finally {
              B(false);
            }
          }}
        >
          <Field name={token ? t('Parolă nouă', 'New password') : t('Utilizator', 'Username')}>
            <input
              required
              type={token ? 'password' : 'text'}
              minLength={token ? 12 : 1}
              value={token ? password : username}
              onChange={(e) => (token ? P(e.target.value) : U(e.target.value))}
            />
          </Field>
          <button type="submit" className="secondary" disabled={busy}>
            {token
              ? t('Schimbă parola', 'Reset password')
              : t('Solicită linkul de resetare', 'Request reset link')}
          </button>
        </form>
      )}
      {message && <p role="status">{message}</p>}
    </div>
  );
}
