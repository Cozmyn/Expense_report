import DocumentViewer from './DocumentViewer';
import React, { useEffect, useState } from 'react';
import { api } from './api';
import { Field } from './Fields';
import { ocrFieldLabel } from './OcrLabels';
type Any = Record<string, any>;
type Region = { page: number; x: number; y: number; width: number; height: number };
export default function OcrGroupBoard({
  file,
  report,
  t,
  run,
  refresh,
  propose,
  linkExisting,
}: Any) {
  const advancedRef = React.useRef<HTMLElement>(null);
  const [activeGroupId, ActiveGroup] = useState<string | null>(null);
  const [focus, Focus] = useState<Region | null>(null),
    [focusPage, FP] = useState(1),
    [focusToken, FT] = useState(0);
  const [changing, Changing] = useState(false);
  const [data, Data] = useState<Any | null>(null),
    [error, E] = useState(''),
    [retry, Retry] = useState(0),
    [selected, S] = useState<string[]>([]),
    [reason, R] = useState(''),
    [splitPages, SP] = useState(''),
    [regionGroup, RG] = useState<Any | null>(null),
    [regions, Regions] = useState<Region[]>([]),
    [fieldGroup, FieldGroup] = useState<Any | null>(null),
    [fieldKey, FieldKey] = useState('amount'),
    [fieldRegion, FieldRegion] = useState<Region | null>(null),
    [history, H] = useState<Any[] | null>(null),
    [feedback, Feedback] = useState<Any[] | null>(null),
    [events, Events] = useState<Any[] | null>(null),
    [existing, X] = useState<Any>({});
  const base = '/reports/' + report.id + '/documents/' + file.id;
  useEffect(() => {
    let active = true;
    api(base + '/review')
      .then((result) => {
        if (active) {
          Data(result);
          E('');
        }
      })
      .catch((e) => {
        if (active) E(e.message);
      });
    return () => {
      active = false;
    };
  }, [report.version, file.id, file.ocr, retry]);
  const perform = (work: () => Promise<any>) => {
    if (changing) return;
    Changing(true);
    return run(async () => {
      try {
        await work();
      } finally {
        Changing(false);
      }
    });
  };
  const command = (action: string, extra: Any = {}) =>
    perform(async () => {
      await api(base + '/groups', 'POST', {
        version: report.version,
        action,
        groups: selected,
        reason,
        ...extra,
      });
      S([]);
      RG(null);
      await refresh();
    });
  if (error)
    return (
      <div role="alert">
        <p>{error}</p>
        <button className="secondary" onClick={() => Retry((n) => n + 1)}>
          {t('Reîncearcă', 'Retry')}
        </button>
      </div>
    );
  if (!data)
    return (
      <p role="status">{t('Se încarcă grupurile de documente…', 'Loading document groups…')}</p>
    );
  if (!data.available)
    return (
      <>
        <Field name={t('Motiv verificare manuală', 'Manual review reason')}>
          <input value={reason} onChange={(e) => R(e.target.value)} />
        </Field>
        <button
          className="secondary"
          onClick={() =>
            perform(async () => {
              await api(base + '/review/start', 'POST', { version: report.version, reason });
              await refresh();
            })
          }
        >
          {t('Deschide verificarea paginilor', 'Start page review')}
        </button>
        <p className="small">
          {t(
            'Dacă OCR nu este disponibil, adaugă un motiv și verifică manual fiecare pagină.',
            'If OCR is unavailable, enter a reason and review each page manually.',
          )}
        </p>
      </>
    );
  const review = data.review,
    groups = review.candidates as Any[],
    stale = review.version !== report.version;
  const activeGroup = groups.find((g: Any) => g.id === activeGroupId)
    || groups.find((g: Any) => g.state !== 'Verified') || groups[0];
  const select = (id: string, checked: boolean) =>
    S((old) => (checked ? [...old, id] : old.filter((x) => x !== id)));
  const warning = (value: string) => {
    const parts = value.split(' / ');
    return parts.length > 1 ? t(parts[0], parts.slice(1).join(' / ')) : value;
  };
  const grouping = (value: string) => {
    const labels: Record<string, [string, string]> = {
      'Unverified text boundary': [
        'Grupare sugerată din text; verifică originalul',
        'Suggested grouping from text; check the original',
      ],
      'Consecutive printed page numbering; human verification required': [
        'Paginile par să aparțină aceleiași facturi; verifică',
        'Pages may belong to one invoice; please check',
      ],
      'Single receipt chosen by employee': [
        'Un singur bon ales la încărcare',
        'Single receipt selected at upload',
      ],
      'Human regrouping; review source history': [
        'Grupare modificată manual; verifică istoricul',
        'Grouping changed manually; check the history',
      ],
    };
    const label = labels[value] || [
      'Grupare sugerată; verifică originalul',
      'Suggested grouping; check the original',
    ];
    return t(label[0], label[1]);
  };
  const openRegions = (g: Any) => {
    RG(g);
    Regions(g.regions.map((r: Region) => ({ ...r })));
  };
  return (
    <section
      className="ocr-group-board"
      aria-label={t('Verificarea documentelor', 'Document review')}
    >
      <h3>{t('Documente de verificat', 'Documents to review')}</h3>
      <p className="small muted">
        {t(
          'Verifică fiecare bon înainte de a crea cheltuiala. Poți delimita separat mai multe bonuri din același PDF.',
          'Review each receipt before creating an expense. You can select several receipts from one PDF.',
        )}
      </p>
      {file.receiptMode === 'Single' && groups.some((g: Any) =>
        g.warnings?.some((w: string) => w.startsWith('MULTIPLE_SECTIONS_SINGLE:'))
      ) && (
        <div className="issue warning" role="alert">
          <strong>{t('Au fost detectate mai multe bonuri', 'Several receipts were detected')}</strong>
          <p>{t(
            'Nu salva un total combinat. Separă documentele fără să încarci din nou fișierul.',
            'Do not save a combined total. Separate the receipts without uploading again.'
          )}</p>
          <button type="button" className="primary" disabled={changing || stale}
            onClick={() => perform(async () => {
              const candidate = groups.find((g: Any) => g.warnings?.some(
                (w: string) => w.startsWith('MULTIPLE_SECTIONS_SINGLE:')));
              if (!candidate) return;
              await api(base + '/groups', 'POST', {
                version: report.version,
                action: 'expand',
                groups: [candidate.id],
                reason: 'Separate multiple OCR receipt candidates selected as one receipt',
              });
              await refresh();
            })}>
            {t('Separă bonurile detectate', 'Separate detected receipts')}
          </button>
        </div>
      )}
      <section className="quick-help">
        <h4>{t('Mai multe bonuri într-un PDF?', 'Multiple receipts in one PDF?')}</h4>
        <ol>
          <li>{t('Deschide delimitarea și trasează o regiune pentru fiecare bon.', 'Open the selection tool and draw one region per receipt.')}</li>
          <li>{t('Creează câte un grup separat și verifică datele extrase.', 'Create one document group per receipt and review the extracted fields.')}</li>
          <li>{t('Confirmă fiecare grup pentru a crea cheltuieli separate.', 'Confirm each group to create separate expenses.')}</li>
        </ol>
      </section>
      {(file.receiptMode !== 'Single' || review.tombstones?.some(
        (g: Any) => g.state === 'Split'
      )) && (
        <button type="button" className="secondary"
          onClick={() => { RG({ id: 'new-region', pages: [1] }); Regions([]); }}>
          {t('Nu este detectat un bon? Selectează-l din PDF', 'Missing a receipt? Select it in the PDF')}
        </button>
      )}
      {review.attempts > 1 && (
        <p className="small muted">{t(
          'Câmpurile selectate folosesc ultima tentativă OCR, păstrând toate încercările în istoric.',
          'Selected fields use the latest OCR attempt; previous attempts remain in the history.'
        )}</p>
      )}
      {(changing || stale) && (
        <p role="status">
          {t(
            'Se actualizează grupurile. Așteaptă înainte de următoarea modificare.',
            'Updating document groups. Wait before the next change.',
          )}
          {stale && !changing && (
            <button className="text-button" onClick={() => run(refresh)}>
              {t('Actualizează decontul', 'Refresh report')}
            </button>
          )}
        </p>
      )}
      <fieldset className="ocr-review-controls" disabled={changing || stale}>
        {review.duplicateUpload && (
          <p className="issue warning" role="status">
            {t(
              'Același fișier a mai fost încărcat. Verifică dublurile înainte de a crea bonuri; o regiune deja verificată nu poate genera încă un bon.',
              'This file has been uploaded before. Check duplicates before creating receipts; a verified source region cannot create another receipt.',
            )}
          </p>
        )}
        {review.pageStates
          ?.filter((p: Any) => p.error)
          .map((p: Any) => (
            <p className="issue warning" key={p.number}>
              {t('Pagina necesită verificare manuală', 'Page requires manual review')}: {p.number}
            </p>
          ))}
        <p>
          {review.pageCount} {t('pagini originale', 'original pages')} ·{' '}
          {groups.filter((g) => g.state === 'Verified').length}/{groups.length}{' '}
          {t('documente verificate', 'documents verified')} · {review.unassignedPages.length}{' '}
          {t('pagini neatribuite', 'unassigned pages')}
        </p>
        {review.unassignedPages.length > 0 && (
          <div className="issue warning ocr-page-warning" role="status">
            <strong>
              {t('Pagini de verificat', 'Pages needing review')}: {review.unassignedPages.join(', ')}
            </strong>
            <p>
              {t(
                'Atribuie aceste pagini unui bon sau marchează-le ca documente justificative.',
                'Assign these pages to a receipt or mark them as supporting documents.',
              )}
            </p>
            <button
              type="button"
              className="secondary"
              onClick={() => {
                advancedRef.current?.scrollIntoView({ block: 'nearest' });
                advancedRef.current?.querySelector<HTMLInputElement>('input')?.focus();
              }}
            >
              {t('Verifică paginile', 'Review pages')}
            </button>
          </div>
        )}
        {(file.receiptMode !== 'Single' || review.unassignedPages.length > 0 || groups.length > 1) && (
        <section className="ocr-group-tools" ref={advancedRef}>
          <h4>{t('Verificarea paginilor și a bonurilor', 'Review pages and receipts')}</h4>
        <Field
          name={t('Motivul modificării grupării', 'Reason for grouping change')}
          note={t(
            'Necesar pentru delimitare, ștergere sau marcarea documentelor justificative.',
            'Required for regrouping, deletion or supporting-page disposition.',
          )}
        >
          <input value={reason} onChange={(e) => R(e.target.value)} />
        </Field>
        {review.unassignedPages.length > 0 && (
          <div className="issue warning" role="status">
            <strong>
              {t('Pagini neatribuite', 'Unassigned pages')}: {review.unassignedPages.join(', ')}
            </strong>
            <p>
              {t(
                'Atribuie fiecare pagină sau marcheaz-o justificativă cu motiv. Trimiterea este blocată până la verificare.',
                'Assign each page or mark it as supporting evidence with a reason. Submission is blocked until review is complete.',
              )}
            </p>
            {review.unassignedPages.map((page: number) => (
              <div className="actions" key={page}>
                <span>
                  {t('Pagina', 'Page')} {page}
                </span>
                <button
                  className="secondary"
                  disabled={reason.trim().length < 3}
                  onClick={() =>
                    command('add', {
                      groups: [],
                      regions: [{ page, x: 0, y: 0, width: 1, height: 1 }],
                    })
                  }
                >
                  {t('Creează document din pagină', 'Create document from page')}
                </button>
                <button
                  className="secondary"
                  disabled={reason.trim().length < 3}
                  onClick={() =>
                    perform(async () => {
                      await api(base + '/pages', 'POST', {
                        version: report.version,
                        page,
                        supporting: true,
                        reason,
                      });
                      await refresh();
                    })
                  }
                >
                  {t('Marchează justificativă', 'Mark supporting')}
                </button>
              </div>
            ))}
          </div>
        )}
        {review.dispositions.map((d: Any) => (
          <div className="simple-row" key={d.page}>
            <span>
              {t('Pagina justificativă', 'Supporting page')} {d.page}: {d.reason}
            </span>
            <button
              className="text-button"
              disabled={reason.trim().length < 3}
              onClick={() =>
                perform(async () => {
                  await api(base + '/pages', 'POST', {
                    version: report.version,
                    page: d.page,
                    supporting: false,
                    reason,
                  });
                  await refresh();
                })
              }
            >
              {t('Reverifică pagina', 'Review page again')}
            </button>
          </div>
        ))}
        <div className="actions">
          <button
            className="secondary"
            disabled={selected.length < 2 || reason.trim().length < 3}
            onClick={() => command('merge')}
          >
            {t('Unește documentele selectate', 'Merge selected documents')}
          </button>
          <Field
            name={t('Pagini pentru separare', 'Pages to split')}
            note={t(
              'De exemplu: 2, 3. Selectează un singur grup.',
              'For example: 2, 3. Select one group.',
            )}
          >
            <input value={splitPages} onChange={(e) => SP(e.target.value)} />
          </Field>
          <button
            className="secondary"
            disabled={selected.length !== 1 || reason.trim().length < 3 || !splitPages}
            onClick={() =>
              command('split', { pages: splitPages.split(',').map((s) => Number(s.trim())) })
            }
          >
            {t('Separă paginile', 'Split pages')}
          </button>
        </div>
        </section>
        )}
        {groups.length > 1 && (
          <div className="receipt-group-switcher" role="group" aria-label={t('Selectează bonul pentru verificare', 'Choose receipt to review')}>
            {groups.map((g: Any, index: number) => (
              <button type="button" key={g.id}
                className={activeGroup?.id === g.id ? 'primary' : 'secondary'}
                aria-pressed={activeGroup?.id === g.id}
                onClick={() => {
                  ActiveGroup(g.id);
                  FP(g.pages?.[0] || 1);
                  Focus(g.regions?.[0] || null);
                  FT((n) => n + 1);
                }}>
                {t('Bon', 'Receipt')} {index + 1} · {g.state === 'Verified' ? t('Verificat', 'Verified') : t('De verificat', 'Needs review')}
              </button>
            ))}
          </div>
        )}
        <div className="ocr-review-layout">
          <aside className="ocr-review-original">
            <DocumentViewer
              document={{
                name: file.name,
                url: '/api/report-attachments/' + file.id,
                mime: file.mime,
                id: file.id,
                reportId: report.id,
                pageCount: review.pageCount,
                previewUrl: file.mime === 'image/heic' ? base + '/preview' : null,
              }}
              t={t}
              focus={focus || activeGroup?.regions?.[0] || null}
              regions={activeGroup?.regions || []}
              focusPage={activeGroup?.pages?.[0] || focusPage}
              focusToken={focusToken}
            />
            {!focus && (
              <p className="small muted">
                {t(
                  'Când coordonatele nu sunt disponibile, verifică manual pagina sursă.',
                  'When precise coordinates are unavailable, check the source page manually.',
                )}
              </p>
            )}
          </aside>
          <div className="ocr-review-groups">
            {groups.map((g: Any, index: number) => activeGroup?.id !== g.id ? null : (
              <section className="capture-document" key={g.id}>
                <h4>
                  {t('Document detectat', 'Detected document')} {index + 1} · {t('Pagini', 'Pages')}{' '}
                  {g.pages.join(', ')} ·{' '}
                  {g.state === 'Verified'
                    ? t('Verificat', 'Verified')
                    : t('De verificat', 'Needs review')}
                </h4>
                <label className="confirmation">
                  <input
                    type="checkbox"
                    checked={selected.includes(g.id)}
                    disabled={g.state === 'Verified'}
                    onChange={(e) => select(g.id, e.target.checked)}
                  />
                  {t('Selectează documentul', 'Select document')} {index + 1}
                </label>
                <p className="small muted">{grouping(g.groupingReason)}</p>
                <section>
                  <h4>
                    {t('Dovezi și alternative de grupare', 'Grouping evidence and alternatives')}
                  </h4>
                  {g.alternatives?.map((a: Any, n: number) => (
                    <div key={n}>
                      <strong>
                        {t('Pagini', 'Pages')}: {a.pages.join(', ')}
                      </strong>
                      {Object.entries(a.alternatives || a.fields).map(
                        ([key, value]: [string, any]) => (
                          <p key={key}>
                            {key}:{' '}
                            {(Array.isArray(value) ? value : [value])
                              .map((v: Any) => v.value ?? t('Necunoscut', 'Unknown'))
                              .join(' / ')}
                          </p>
                        ),
                      )}
                    </div>
                  ))}
                </section>
                <dl className="ocr-fields">
                  {Object.entries(g.fields).map(([key, f]: [string, any]) => (
                    <div
                      className={
                        typeof f.confidence === 'number' && f.confidence < 0.7
                          ? 'issue warning'
                          : ''
                      }
                      key={key}
                    >
                      <dt>
                        {ocrFieldLabel(key, t)}
                      </dt>
                      <dd>
                        {f.value ?? t('Necunoscut', 'Unknown')}{' '}
                        <small>
                          {t('Verifică pe original', 'Check original')} · {t('pagina', 'page')}{' '}
                          {f.page}
                        </small>
                        <section className="ocr-field-more">
                          <h4>
                            {t('Cum a fost citit', 'Recognition details')}
                          </h4>
                          <blockquote>{f.evidence}</blockquote>
                          <span className="small">
                            {typeof f.confidence === 'number'
                              ? t('Calitatea recunoașterii textului: ', 'Text recognition quality: ') +
                                Math.round(f.confidence * 100) +
                                '%'
                              : f.source === 'TextLayer'
                                ? t('Text din PDF; fără scor OCR', 'Text from PDF; no OCR score')
                                : t(
                                    'Scor indisponibil; verifică originalul',
                                    'Score unavailable; check the original',
                                  )}
                          </span>
                        </section>
                        <button
                          className="text-button"
                          onClick={() => {
                            FP(f.page);
                            Focus(f.box ? { page: f.page, ...f.box } : null);
                            FT((n) => n + 1);
                          }}
                        >
                          {t('Arată sursa câmpului', 'Show field source')}
                        </button>
                      </dd>
                    </div>
                  ))}
                </dl>
                {g.warnings.map((w: string, n: number) => (
                  <p className="issue warning" key={n}>
                    {warning(w)}
                  </p>
                ))}
                {g.state === 'Verified' && g.correctedExpense && (
                  <p className="success">
                    {t('Total verificat', 'Verified total')}: {g.correctedExpense.amount}{' '}
                    {g.correctedExpense.currency}
                  </p>
                )}
                <div className="actions">
                  <button
                    className="primary"
                    disabled={g.state === 'Verified'}
                    onClick={() => propose({ ...g, sourceGroupId: g.id })}
                  >
                    {t('Revizuiește și creează bonul', 'Review and create receipt')}
                  </button>
                  <a
                    className="secondary"
                    href={'/api/report-attachments/' + file.id + '#page=' + g.pages[0]}
                    target="_blank"
                    rel="noreferrer"
                  >
                    {t('Vezi paginile sursă', 'View source pages')}
                  </a>
                  {g.state !== 'Verified' && (
                    <>
                      <button
                        className="secondary"
                        onClick={() => {
                          FieldGroup(g);
                          FieldRegion(null);
                        }}
                      >
                        {t('Selectează un câmp din bon', 'Select a field from the receipt')}
                      </button>
                      <button className="secondary" onClick={() => openRegions(g)}>
                        {t('Ajustează paginile / regiunea', 'Adjust pages / region')}
                      </button>
                      <button
                        className="secondary"
                        disabled={reason.trim().length < 3}
                        onClick={() => command('supporting', { groups: [g.id] })}
                      >
                        {t('Document justificativ', 'Supporting document')}
                      </button>
                      <button
                        className="secondary danger"
                        disabled={reason.trim().length < 3}
                        onClick={() => {
                          if (
                            window.confirm(
                              t(
                                'Elimini propunerea? Originalul și istoricul se păstrează; paginile vor necesita reverificare.',
                                'Remove this proposal? The original and history remain; its pages will need review.',
                              ),
                            )
                          )
                            command('delete', { groups: [g.id] });
                        }}
                      >
                        {t('Elimină propunerea', 'Remove proposal')}
                      </button>
                    </>
                  )}
                </div>
                {g.state !== 'Verified' && report.lines.length > 0 && (
                  <div className="actions">
                    <Field name={t('Asociază unui bon existent', 'Link to an existing receipt')}>
                      <select
                        value={existing[g.id] || ''}
                        onChange={(e) => X((old: Any) => ({ ...old, [g.id]: e.target.value }))}
                      >
                        <option value="">{t('Selectează bonul', 'Select receipt')}</option>
                        {report.lines.map((l: Any) => (
                          <option key={l.id} value={l.id}>
                            {l.position}. {l.data.description}
                          </option>
                        ))}
                      </select>
                    </Field>
                    <button
                      className="secondary"
                      disabled={!existing[g.id]}
                      onClick={() =>
                        linkExisting(
                          { ...g, sourceGroupId: g.id },
                          report.lines.find((l: Any) => l.id === existing[g.id]),
                        )
                      }
                    >
                      {t('Verifică asocierea', 'Review link')}
                    </button>
                  </div>
                )}
              </section>
            ))}
          </div>
        </div>
        {fieldGroup && (
          <section className="panel ocr-field-selector" aria-label={t('Selectarea câmpului', 'Field selection')}>
            <div className="section-heading">
              <h4>{t('Selectează textul direct de pe bon', 'Select text directly on the receipt')}</h4>
              <button type="button" className="text-button" onClick={() => FieldGroup(null)}>
                {t('Închide', 'Close')}
              </button>
            </div>
            <p>
              {t(
                'Alege câmpul, apasă „Delimitează regiunea” și trasează un dreptunghi peste text. Valoarea devine o propunere pe care o verifici înainte de salvare.',
                'Choose a field, select “Select source region” and draw a box around the text. The result is a suggestion to verify before saving.',
              )}
            </p>
            <Field name={t('Ce informație selectezi?', 'Which field are you selecting?')}>
              <select value={fieldKey} onChange={(e) => { FieldKey(e.target.value); FieldRegion(null); }}>
                {['supplier', 'cui', 'invoice', 'date', 'currency', 'amount', 'description'].map((key) => (
                  <option key={key} value={key}>{ocrFieldLabel(key, t)}</option>
                ))}
              </select>
            </Field>
            <DocumentViewer
              key={fieldGroup.id}
              document={{
                name: file.name,
                url: '/api/report-attachments/' + file.id,
                mime: file.mime,
                id: file.id,
                reportId: report.id,
                pageCount: review.pageCount,
                initialPage: fieldGroup.pages[0],
                previewUrl: file.mime === 'image/heic' ? base + '/preview' : null,
              }}
              t={t}
              focus={fieldRegion}
              onRegionSelect={(selected) => FieldRegion(selected)}
            />
            {fieldRegion && (
              <p role="status" className="small">
                {t('Regiune selectată pe pagina', 'Selected region on page')} {fieldRegion.page}.
                {' '}{t('Aplică selecția, apoi verifică textul propus.', 'Apply the selection, then review the suggested text.')}
              </p>
            )}
            <div className="actions">
              <button
                type="button"
                className="primary"
                disabled={changing || stale || !fieldRegion}
                onClick={() =>
                  perform(async () => {
                    await api(base + '/groups/' + fieldGroup.id + '/select-field', 'POST', {
                      version: report.version,
                      field: fieldKey,
                      region: fieldRegion,
                      reason: reason.trim().length >= 3 ? reason : 'User selected source text',
                    });
                    FieldGroup(null);
                    FieldRegion(null);
                    await refresh();
                  })
                }
              >
                {t('Folosește textul selectat', 'Use selected text')}
              </button>
              <button className="secondary" type="button" onClick={() => FieldRegion(null)}>
                {t('Selectează din nou', 'Select again')}
              </button>
            </div>
          </section>
        )}
        {regionGroup && (
          <section className="panel">
            <h4>{t('Delimitarea sursei', 'Source boundaries')}</h4>
            <DocumentViewer
              key={regionGroup.id}
              document={{
                name: file.name,
                url: '/api/report-attachments/' + file.id,
                mime: file.mime,
                id: file.id,
                reportId: report.id,
                pageCount: review.pageCount,
                initialPage: regions[0]?.page || 1,
                previewUrl: file.mime === 'image/heic' ? base + '/preview' : null,
              }}
              t={t}
              regions={regions}
              onRegionSelect={(region) =>
                Regions((old) => [...old.filter((r) => r.page !== region.page), region])
              }
            />
            <p>
              {t(
                'Procente din pagina originală: stânga, sus, lățime, înălțime. Pentru două bonuri pe aceeași pagină, regiunile nu trebuie să se suprapună.',
                'Percentages of the original page: left, top, width, height. Two receipts on the same page must have non-overlapping regions.',
              )}
            </p>
            {regions.map((r: Region, index: number) => (
              <div className="form-grid" key={index}>
                {(['page', 'x', 'y', 'width', 'height'] as const).map((key) => (
                  <Field
                    key={key}
                    name={t(
                      {
                        page: 'Pagina',
                        x: 'Stânga (%)',
                        y: 'Sus (%)',
                        width: 'Lățime (%)',
                        height: 'Înălțime (%)',
                      }[key],
                      {
                        page: 'Page',
                        x: 'Left (%)',
                        y: 'Top (%)',
                        width: 'Width (%)',
                        height: 'Height (%)',
                      }[key],
                    )}
                  >
                    <input
                      type="number"
                      min={key === 'page' ? 1 : 0}
                      max={key === 'page' ? review.pageCount : 100}
                      step={key === 'page' ? 1 : 0.1}
                      value={key === 'page' ? r[key] : r[key] * 100}
                      onChange={(e) =>
                        Regions((old) =>
                          old.map((v, n) =>
                            n === index
                              ? { ...v, [key]: Number(e.target.value) / (key === 'page' ? 1 : 100) }
                              : v,
                          ),
                        )
                      }
                    />
                  </Field>
                ))}
                <button
                  className="secondary"
                  onClick={() => Regions((old) => old.filter((_, n) => n !== index))}
                >
                  {t('Elimină regiunea', 'Remove region')}
                </button>
              </div>
            ))}
            <div className="actions">
              <button
                className="secondary"
                onClick={() =>
                  Regions((old) => [...old, { page: 1, x: 0, y: 0, width: 1, height: 1 }])
                }
              >
                {t('Adaugă pagină / regiune', 'Add page / region')}
              </button>
              {regionGroup.id !== 'new-region' && (
                <button
                  className="secondary"
                  disabled={reason.trim().length < 3}
                  onClick={() => command('regions', { groups: [regionGroup.id], regions })}
                >
                  {t('Salvează delimitarea', 'Save boundaries')}
                </button>
              )}
              <button
                className="secondary"
                disabled={reason.trim().length < 3 || regions.length !== 1}
                onClick={() => command('add', { groups: [], regions })}
              >
                {t('Creează un bon separat din regiune', 'Create separate receipt group from region')}
              </button>
              <button className="text-button" onClick={() => RG(null)}>
                {t('Închide', 'Close')}
              </button>
            </div>
          </section>
        )}
        <section>
          <h4>
            {t('Grupuri modificate sau eliminate', 'Changed or removed groups')} (
            {review.tombstones?.length || 0})
          </h4>
          {review.tombstones?.map((g: Any) => (
            <div className="simple-row" key={g.id}>
              <span>
                {g.state} · {g.id.slice(0, 8)}
              </span>
              {['Deleted', 'Supporting'].includes(g.state) && (
                <button
                  className="secondary"
                  disabled={reason.trim().length < 3}
                  onClick={() => command('restore', { groups: [g.id] })}
                >
                  {t('Restaurează propunerea', 'Restore proposal')}
                </button>
              )}
            </div>
          ))}
        </section>
        <button
          className="text-button"
          onClick={() => perform(async () => Events(await api(base + '/events')))}
        >
          {t('Evenimente de procesare', 'Processing events')}
        </button>
        {events && (
          <ol>
            {events.map((event: Any) => (
              <li key={event.id}>
                {t(
                  (
                    {
                      DocumentUploaded: 'Document încărcat',
                      ProcessingStarted: 'Procesare începută',
                      DocumentNormalized: 'Pagini pregătite',
                      DocumentSegmented: 'Grupuri identificate',
                      ExtractionCompleted: 'Extragere finalizată',
                      HumanCorrectionApplied: 'Valori corectate',
                      DocumentVerified: 'Document verificat',
                      OcrJobFailed: 'Extragere eșuată',
                    } as Any
                  )[event.type] || event.type,
                  event.type,
                )}{' '}
                · {new Date(event.created).toLocaleString(t('ro-RO', 'en-GB'))} ·{' '}
                {t(
                  (
                    {
                      Pending: 'În așteptare',
                      Processing: 'În livrare',
                      Completed: 'Publicat',
                      Failed: 'Livrare eșuată',
                    } as Any
                  )[event.delivery] || event.delivery,
                  event.delivery,
                )}
              </li>
            ))}
          </ol>
        )}
        <button
          className="text-button"
          onClick={() => perform(async () => Feedback(await api(base + '/feedback')))}
        >
          {t('Istoric valori corectate', 'Correction history')}
        </button>
        {feedback?.map((entry: Any, index: number) => (
          <section key={index}>
            <h4>
              {new Date(entry.at).toLocaleString(t('ro-RO', 'en-GB'))} · {entry.actor}
            </h4>
            {entry.fields.length === 0 ? (
              <p>
                {t(
                  'Valorile au fost confirmate fără corecții.',
                  'Values were confirmed without corrections.',
                )}
              </p>
            ) : (
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>{t('Câmp', 'Field')}</th>
                      <th>OCR</th>
                      <th>{t('Anterior', 'Previous')}</th>
                      <th>{t('Verificat', 'Verified')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {entry.fields.map((f: Any) => (
                      <tr key={f.field}>
                        <td>
                          {f.field.startsWith('vat.')
                            ? t(
                                (f.field.endsWith('.vat') ? 'TVA ' : 'Bază ') +
                                  f.field.split('.')[1] +
                                  '%',
                                (f.field.endsWith('.vat') ? 'VAT ' : 'Base ') +
                                  f.field.split('.')[1] +
                                  '%',
                              )
                            : ocrFieldLabel(f.field, t)}
                        </td>
                        <td>{f.raw ?? '—'}</td>
                        <td>{f.previous ?? '—'}</td>
                        <td>{f.verified ?? '—'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        ))}
        <button
          className="text-button"
          onClick={() => perform(async () => H(await api(base + '/extractions')))}
        >
          {t('Istoric extrageri originale', 'Original extraction history')}
        </button>
        {history?.map((a) => (
          <section key={a.id}>
            <h4>
              {new Date(a.created).toLocaleString(t('ro-RO', 'en-GB'))} ·{' '}
              {a.fingerprint.slice(0, 12)}
            </h4>
            <pre className="ocr-text">{JSON.parse(a.json).text}</pre>
          </section>
        ))}
      </fieldset>
    </section>
  );
}
