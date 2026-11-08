import React, { useState } from 'react';
import { api } from './api';
import { Field } from './Fields';
export default function EmployeeImport({ t, run, refresh }: any) {
  const [preview, P] = useState<any>(null),
    [reason, R] = useState(''),
    [password, Pass] = useState('');
  return (
    <section className="panel">
      <h2>{t('Import angajați CSV / XLSX', 'Import employees CSV / XLSX')}</h2>
      <a className="text-button" href="/api/admin/employee-import/template">
        {t('Descarcă șablonul', 'Download template')}
      </a>
      <Field name={t('Fișier angajați', 'Employee file')}>
        <input
          type="file"
          accept=".csv,.xlsx"
          onChange={(e) => {
            const file = e.target.files?.[0];
            if (file)
              run(async () => {
                const form = new FormData();
                form.append('file', file);
                P(await api('/admin/employee-import/preview', 'POST', form));
              });
          }}
        />
      </Field>
      {preview && (
        <>
          <p>
            {preview.count} {t('conturi valide', 'valid accounts')} · {preview.errors.length}{' '}
            {t('erori', 'errors')}
          </p>
          {preview.errors.map((e: any) => (
            <p className="issue blocking" key={e.row}>
              {e.row}: {e.message}
            </p>
          ))}
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>{t('Utilizator', 'Username')}</th>
                  <th>{t('Nume', 'Name')}</th>
                  <th>Email</th>
                  <th>Worker</th>
                  <th>Manager</th>
                </tr>
              </thead>
              <tbody>
                {preview.rows.map((r: any) => (
                  <tr key={r.username}>
                    <td>{r.username}</td>
                    <td>{r.name}</td>
                    <td>{r.email}</td>
                    <td>{r.worker}</td>
                    <td>{r.managerUsername}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Field
            name={t(
              'Parolă temporară pentru conturile importate',
              'Temporary password for imported accounts',
            )}
            note={t(
              'Schimbarea este obligatorie la prima autentificare. Comunică parola prin canalul intern autorizat.',
              'Change is mandatory at first sign-in. Communicate through your authorized internal channel.',
            )}
          >
            <input
              type="password"
              autoComplete="new-password"
              minLength={12}
              value={password}
              onChange={(e) => Pass(e.target.value)}
            />
          </Field>
          <Field name={t('Motiv import', 'Import reason')}>
            <input minLength={3} value={reason} onChange={(e) => R(e.target.value)} />
          </Field>
          <button
            className="primary"
            disabled={
              !preview.count ||
              preview.errors.length > 0 ||
              password.length < 12 ||
              reason.trim().length < 3
            }
            onClick={() =>
              run(async () => {
                await api('/admin/employee-import/confirm', 'POST', {
                  rows: preview.rows,
                  reason,
                  temporaryPassword: password,
                });
                P(null);
                Pass('');
                await refresh();
              })
            }
          >
            {t('Confirmă importul', 'Confirm import')}
          </button>
        </>
      )}
    </section>
  );
}
