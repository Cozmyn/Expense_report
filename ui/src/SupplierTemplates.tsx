import React, { useEffect, useState } from 'react';
import { api } from './api';

type Any = Record<string, any>;

/** Admin/Finance-managed supplier layouts. Every revision is append-only and
 * sourced from separate human-verified originals, never synthetic OCR training. */
export default function SupplierTemplates({ t, run }: Any) {
  const [cui, C] = useState('');
  const [layoutId, Layout] = useState('');
  const [name, Name] = useState('');
  const [reason, Reason] = useState('');
  const [versions, Versions] = useState<Any[]>([]);
  const [samples, Samples] = useState<Any[]>([]);
  const [selected, Selected] = useState<string[]>([]);
  const [loading, Loading] = useState(false);
  const [error, ErrorMessage] = useState('');
  const key = cui.trim().replace(/^RO\s*/i, '').replace(/\s/g, '');

  const refresh = async () => {
    const rows = await api('/finance/supplier-ocr-templates'
      + (key ? '?cui=' + encodeURIComponent(key) : ''));
    Versions(rows);
  };
  useEffect(() => {
    let active = true;
    api('/finance/supplier-ocr-templates')
      .then(rows => { if (active) Versions(rows); })
      .catch(err => { if (active) ErrorMessage((err as Error).message); });
    return () => { active = false; };
  }, []);

  const lookup = async () => {
    Loading(true);
    ErrorMessage('');
    try {
      await refresh();
      const result = key ? await api('/finance/supplier-ocr-templates/examples?cui='
        + encodeURIComponent(key)) : [];
      Samples(result);
      Selected([]);
    } catch (err) {
      ErrorMessage((err as Error).message);
    } finally {
      Loading(false);
    }
  };

  const create = async () => {
    if (!key || !layoutId || !name || selected.length < 2 || reason.trim().length < 3)
      return;
    await run(async () => {
      await api('/finance/supplier-ocr-templates', 'POST', {
        cui: key, layoutId, name, verifiedGroups: selected, reason,
      });
      await refresh();
      Selected([]);
      Reason('');
    });
  };
  const toggle = async (item: Any) => {
    if (reason.trim().length < 3) return;
    await run(async () => {
      await api('/finance/supplier-ocr-templates/'
        + encodeURIComponent(item.cui) + '/' + encodeURIComponent(item.layoutId)
        + '/toggle?enabled=' + String(!item.enabled),
        'POST', { reason });
      await refresh();
      Reason('');
    });
  };

  return (
    <section className="panel supplier-template-manager">
      <h2>{t('Șabloane OCR versionate ale furnizorilor', 'Versioned supplier OCR layouts')}</h2>
      <p className="small muted">{t(
        'Un furnizor poate avea șabloane diferite pentru filiale, facturi sau case de marcat. Fiecare versiune necesită două documente verificate manual. Dezactivarea nu șterge istoricul.',
        'One supplier may have different layouts for branches, invoices or cash registers. Each version requires two separately verified originals. Disabling a layout preserves history.'
      )}</p>
      {error && <p role="alert" className="error">{error}</p>}
      <div className="form-grid">
        <label>{t('CUI furnizor', 'Supplier VAT ID')}
          <input value={cui} onChange={e => C(e.target.value)} placeholder="18546294"
            aria-label={t('CUI furnizor', 'Supplier VAT ID')} />
        </label>
        <button type="button" className="secondary" disabled={loading}
          onClick={() => void lookup()}>
          {t('Găsește documentele verificate', 'Find verified source documents')}
        </button>
      </div>
      {samples.length > 0 && (
        <fieldset>
          <legend>{t('Alege documentele originale de antrenare (minimum două)', 'Select verified source documents (two minimum)')}</legend>
          {samples.map((sample: Any) => (
            <label key={sample.bundle} className="simple-row">
              <input type="checkbox" checked={selected.includes(sample.bundle)}
                onChange={e => Selected(old => e.target.checked
                  ? [...old, sample.bundle] : old.filter(x => x !== sample.bundle))} />
              {t('Document verificat', 'Verified document')}: {sample.bundle}
              <small>{sample.fields.join(', ')}</small>
            </label>
          ))}
        </fieldset>
      )}
      <div className="form-grid">
        <label>{t('Cod șablon/filială', 'Layout/branch ID')}
          <input value={layoutId} onChange={e => Layout(e.target.value)}
            pattern="[A-Za-z0-9_-]{2,40}" maxLength={40}
            placeholder="factura-filiala-2" />
        </label>
        <label>{t('Denumire șablon', 'Layout name')}
          <input value={name} onChange={e => Name(e.target.value)} maxLength={100}
            placeholder={t('Factură / casă nouă', 'Invoice / new till')} />
        </label>
        <label>{t('Motiv audit (obligatoriu)', 'Audit reason (required)')}
          <input value={reason} onChange={e => Reason(e.target.value)} minLength={3}
            placeholder={t('De ce actualizezi șablonul?', 'Why change this layout?')} />
        </label>
      </div>
      <button type="button" className="primary"
        disabled={selected.length < 2 || !layoutId || !name || reason.trim().length < 3}
        onClick={() => void create()}>
        {t('Creează o versiune nouă', 'Create a new layout version')}
      </button>
      <h3>{t('Versiuni active și dezactivate', 'Enabled and disabled versions')}</h3>
      {versions.length === 0 && <p className="small muted">{t('Niciun șablon administrat încă.', 'No managed templates yet.')}</p>}
      {versions.map(item => (
        <article className="simple-row" key={item.cui + ':' + item.layoutId}>
          <span>
            <strong>{item.cui} · {item.name}</strong> · {item.layoutId}
            {' · '}{t('Versiunea', 'Revision')} {item.revision}
            {' · '}{item.enabled ? t('Activ', 'Enabled') : t('Dezactivat', 'Disabled')}
            <small>{item.examples?.length || 0} {t('dovezi istorice păstrate', 'preserved verified samples')}</small>
          </span>
          <button type="button" className="secondary"
            disabled={reason.trim().length < 3} onClick={() => void toggle(item)}>
            {item.enabled ? t('Dezactivează doar acest șablon', 'Disable only this layout')
              : t('Reactivează șablonul', 'Re-enable layout')}
          </button>
        </article>
      ))}
    </section>
  );
}
