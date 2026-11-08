import React, { useEffect, useRef, useState } from 'react';
import { api } from './api';
type Region = { page: number; x: number; y: number; width: number; height: number };
type Doc = {
  name: string;
  url: string;
  mime?: string;
  previewUrl?: string | null;
  reportId?: string;
  id?: string;
  pageCount?: number;
  initialPage?: number;
};
type Props = {
  document: Doc;
  t: (ro: string, en: string) => string;
  regions?: Region[];
  focus?: Region | null;
  focusPage?: number;
  focusToken?: number;
  onRegionSelect?: (r: Region) => void;
};
/** View/crop coordinates refer to the original rendered page; originals are never modified. */
export default function DocumentViewer({
  document: doc,
  t,
  regions = [],
  focus,
  focusPage,
  focusToken,
  onRegionSelect,
}: Props) {
  const [zoom, Z] = useState(100),
    [rotation, R] = useState(0),
    [loaded, L] = useState(false),
    [failed, F] = useState(false),
    [attempt, A] = useState(0),
    [ratio, Ratio] = useState(1),
    [page, P] = useState(doc.initialPage || Number(doc.url.match(/#page=(\d+)/)?.[1]) || 1),
    [pages, Pages] = useState(doc.pageCount || 1),
    [crop, Crop] = useState(false),
    [draft, Draft] = useState<Region | null>(null);
  const [viewer, Viewer] = useState('');
  const start = useRef<{ x: number; y: number } | null>(null);
  const pdf = doc.mime === 'application/pdf',
    paged = pdf && !!doc.reportId && !!doc.id,
    nativePdf = pdf && !paged;
  const unsupported = doc.mime?.includes('heic') && !doc.previewUrl;
  useEffect(() => {
    let active = true;
    if (paged)
      api('/reports/' + doc.reportId + '/documents/' + doc.id + '/preview-info')
        .then((info) => {
          if (active) {
            Viewer(info.viewer || '');
            Pages(info.pages);
            P((n) => Math.min(n, info.pages));
          }
        })
        .catch(() => {
          if (active) F(true);
        });
    return () => {
      active = false;
    };
  }, [doc.reportId, doc.id, attempt]);
  useEffect(() => {
    const target = focus?.page || focusPage;
    if (target && target !== page) {
      P(target);
      L(false);
      F(false);
    }
  }, [focus, focusPage, focusToken]);
  const pageChange = (value: number) => {
    P(Math.max(1, Math.min(pages, value)));
    L(false);
    F(false);
    Draft(null);
  };
  const src = paged
    ? '/api/reports/' +
      doc.reportId +
      '/documents/' +
      doc.id +
      '/pages/' +
      page +
      '/preview' +
      (viewer ? '?viewer=' + encodeURIComponent(viewer) : '')
    : doc.previewUrl || doc.url;
  const odd = rotation % 180 !== 0;
  const displayRatio = odd ? 1 / ratio : ratio;
  const layer: React.CSSProperties = {
    position: 'absolute',
    width: (odd ? ratio * 100 : 100) + '%',
    height: (odd ? 100 / ratio : 100) + '%',
    left: (odd ? (1 - ratio) * 50 : 0) + '%',
    top: (odd ? (1 - 1 / ratio) * 50 : 0) + '%',
    transform: 'rotate(' + rotation + 'deg)',
  };
  const point = (e: React.PointerEvent<HTMLDivElement>) => {
    const rect = e.currentTarget.getBoundingClientRect();
    return {
      x: Math.max(0, Math.min(1, (e.clientX - rect.left) / rect.width)),
      y: Math.max(0, Math.min(1, (e.clientY - rect.top) / rect.height)),
    };
  };
  const outline = (r: Region, key: string, color: string) => (
    <div
      key={key}
      aria-hidden="true"
      style={{
        position: 'absolute',
        left: r.x * 100 + '%',
        top: r.y * 100 + '%',
        width: r.width * 100 + '%',
        height: r.height * 100 + '%',
        border: '3px solid ' + color,
        background: color + '18',
        boxSizing: 'border-box',
      }}
    />
  );
  return (
    <section
      className="document-viewer"
      aria-label={t('Previzualizare document', 'Document preview')}
    >
      <h3>{t('Document original', 'Original document')}</h3>
      <p className="small">{doc.name}</p>
      {paged && (
        <div className="document-toolbar">
          <button
            type="button"
            className="secondary"
            disabled={page <= 1}
            onClick={() => pageChange(page - 1)}
          >
            {t('Pagina anterioară', 'Previous page')}
          </button>
          <label>
            {t('Pagina', 'Page')}{' '}
            <select
              aria-label={t('Pagina documentului', 'Document page')}
              value={page}
              onChange={(e) => pageChange(Number(e.target.value))}
            >
              {Array.from({ length: pages }, (_, n) => (
                <option key={n} value={n + 1}>
                  {n + 1}
                </option>
              ))}
            </select>{' '}
            / {pages}
          </label>
          <button
            type="button"
            className="secondary"
            disabled={page >= pages}
            onClick={() => pageChange(page + 1)}
          >
            {t('Pagina următoare', 'Next page')}
          </button>
        </div>
      )}
      {!nativePdf && !unsupported && (
        <div
          className="document-toolbar"
          role="group"
          aria-label={t('Controale imagine', 'Image controls')}
        >
          <button
            type="button"
            className="secondary"
            disabled={zoom <= 50}
            onClick={() => Z((n) => Math.max(50, n - 25))}
            aria-label={t('Micșorează', 'Zoom out')}
          >
            −
          </button>
          <output aria-live="polite">{zoom}%</output>
          <button
            type="button"
            className="secondary"
            disabled={zoom >= 300}
            onClick={() => Z((n) => Math.min(300, n + 25))}
            aria-label={t('Mărește', 'Zoom in')}
          >
            +
          </button>
          <button
            type="button"
            className="secondary"
            onClick={() => {
              R((n) => (n + 90) % 360);
              Crop(false);
            }}
          >
            {t('Rotește', 'Rotate')}
          </button>
          <button
            type="button"
            className="secondary"
            onClick={() => {
              Z(100);
              R(0);
              Crop(false);
              Draft(null);
            }}
          >
            {t('Resetează', 'Reset')}
          </button>
          {onRegionSelect && (
            <button
              type="button"
              className="secondary"
              disabled={rotation !== 0}
              aria-pressed={crop}
              onClick={() => Crop((n) => !n)}
            >
              {t('Delimitează regiunea', 'Select source region')}
            </button>
          )}
        </div>
      )}
      {crop && (
        <p className="small" role="status">
          {t(
            'Trage pe imagine de la un colț al bonului la colțul opus. Poți folosi și câmpurile procentuale de mai jos.',
            'Drag across the receipt from one corner to the opposite corner. You can also use the percentage inputs below.',
          )}
        </p>
      )}
      {unsupported ? (
        <p>
          {t(
            'Pentru previzualizarea HEIC, salvează bonul și așteaptă scanarea.',
            'To preview HEIC, save the receipt and wait for scanning.',
          )}
        </p>
      ) : (
        <>
          {!loaded && !failed && (
            <p role="status">{t('Se încarcă documentul…', 'Loading document…')}</p>
          )}
          {failed ? (
            <div role="alert">
              <p>
                {t(
                  'Previzualizarea nu s-a încărcat. Reîncearcă sau deschide originalul.',
                  'The preview could not load. Retry or open the original.',
                )}
              </p>
              <button
                type="button"
                className="secondary"
                onClick={() => {
                  F(false);
                  L(false);
                  A((n) => n + 1);
                }}
              >
                {t('Reîncearcă', 'Retry')}
              </button>
            </div>
          ) : nativePdf ? (
            <>
              <iframe
                key={attempt}
                title={doc.name}
                src={doc.url}
                onLoad={() => L(true)}
                onError={() => F(true)}
              />
              <p className="small muted">
                {t(
                  'PDF-ul local folosește controalele browserului până la încărcare și scanare.',
                  'Local PDF uses browser controls until uploaded and scanned.',
                )}
              </p>
            </>
          ) : (
            <div
              className="document-image-scroll"
              tabIndex={0}
              aria-label={t('Imagine; derulează pentru detalii', 'Image; scroll for details')}
            >
              <div
                className="document-image-stage"
                style={{
                  width: zoom + '%',
                  aspectRatio: String(displayRatio),
                  touchAction: crop ? 'none' : 'auto',
                  cursor: crop ? 'crosshair' : 'auto',
                }}
                onPointerDown={(e) => {
                  if (!crop || rotation !== 0) return;
                  e.preventDefault();
                  start.current = point(e);
                  e.currentTarget.setPointerCapture(e.pointerId);
                }}
                onPointerMove={(e) => {
                  if (!start.current || !crop) return;
                  const p = point(e),
                    a = start.current;
                  Draft({
                    page,
                    x: Math.min(a.x, p.x),
                    y: Math.min(a.y, p.y),
                    width: Math.abs(p.x - a.x),
                    height: Math.abs(p.y - a.y),
                  });
                }}
                onPointerUp={(e) => {
                  if (!start.current) return;
                  const p = point(e),
                    a = start.current;
                  start.current = null;
                  const selected = {
                    page,
                    x: Math.min(a.x, p.x),
                    y: Math.min(a.y, p.y),
                    width: Math.abs(p.x - a.x),
                    height: Math.abs(p.y - a.y),
                  };
                  if (selected.width > 0.01 && selected.height > 0.01) {
                    Draft(selected);
                    onRegionSelect?.(selected);
                  }
                  if (e.currentTarget.hasPointerCapture(e.pointerId))
                    e.currentTarget.releasePointerCapture(e.pointerId);
                }}
                onPointerCancel={() => {
                  start.current = null;
                  Draft(null);
                }}
              >
                <img
                  key={attempt + ':' + page}
                  draggable={false}
                  alt={doc.name}
                  src={src}
                  style={layer}
                  onLoad={(e) => {
                    Ratio(e.currentTarget.naturalWidth / e.currentTarget.naturalHeight || 1);
                    L(true);
                  }}
                  onError={() => F(true)}
                />
                <div style={{ ...layer, pointerEvents: 'none' }}>
                  {regions
                    .filter((r) => r.page === page)
                    .map((r, n) => outline(r, 'region' + n, '#075e50'))}
                  {focus?.page === page && outline(focus, 'focus', '#1d4ed8')}
                  {draft?.page === page && outline(draft, 'draft', '#b45309')}
                </div>
              </div>
            </div>
          )}
        </>
      )}
      <a href={doc.url} target="_blank" rel="noopener noreferrer">
        {t('Deschide originalul', 'Open original')}
      </a>
    </section>
  );
}
