import { add, sub, sum, formatMoney, compare } from './decimal.mjs';
import DocumentViewer from './DocumentViewer';
import React, { useEffect, useState, useRef } from 'react';
import { X, AlertCircle, CheckCircle2 } from 'lucide-react';
import { api } from './api';
import { Field } from './Fields';
import { ocrFieldLabel } from './OcrLabels';
import { attentionFields, fieldProvenance } from './receiptProvenance.mjs';
import { stageEncryptedDraft, recoverEncryptedDrafts, clearOwnEncryptedDraftIfMatch,
  discardEncryptedDraft } from './encryptedDraft.mjs';
type Any = Record<string, any>;
export function newReceipt() {
  return {
    draftKey: crypto.randomUUID(),
    data: {
      supplier: '',
      cui: null,
      invoice: '',
      date: new Date().toISOString().slice(0, 10),
      country: 'RO',
      documentType: 'Receipt',
      hasCompanyCui: null,
      supplierConfirmed: false,
      category: '',
      description: '',
      payment: 'Personal',
      cardId: null,
      costCenter: '',
      project: '',
      amount: 0,
      currency: 'RON',
      taxCurrency: 'RON',
      taxes: [],
    },
  };
}
export default function ReceiptModal({
  expense,
  reportPayment,
  reportCardId,
  reportCostCenter,
  employeeCostCenter,
  finance,
  draftStorageKey,
  masters,
  label,
  t,
  busy,
  onClose,
  save,
  saveDraft,
  onSourceSelected,
  inline = false,
}: Any) {
  const [d, D] = useState<Any>({
    ...expense.data,
    // Existing expense keeps its allocation; each NEW receipt defaults to the
    // employee's database assignment, independently from the header choice.
    costCenter: expense.id
      ? (expense.data.costCenter || '')
      : (employeeCostCenter || expense.data.costCenter || reportCostCenter || ''),
    hasCompanyCui:
      expense.data.hasCompanyCui ??
      (expense.data.documentType === 'InvoiceWithCui'
        ? true
        : expense.data.documentType === 'InvoiceWithoutCui'
          ? false
          : null),
  });
  const [draftError, DraftError] = useState('');
  const [original, Original] = useState<any | null>(null);
  const [focusedSource, FS] = useState<any | null>(null),
    [focusedPage, FP] = useState(expense.ocrCandidate?.pages?.[0] || 1),
    [focusedToken, FT] = useState(0);
  const [scanField, ScanField] = useState<string | null>(null),
    [scanRegion, ScanRegion] = useState<any | null>(null),
    [manualHighlights, ManualHighlights] = useState<any[]>([]),
    [scanBusy, ScanBusy] = useState(false),
    [scanError, ScanError] = useState(''),
    [financeScanReason, FinanceScanReason] = useState('');
  const [lookup, Lookup] = useState<Any | null>(null),
    [looking, Looking] = useState(false),
    [lookupError, LE] = useState(''),
    [external, External] = useState<Any | null>(null),
    [externalReason, ER] = useState('');
  useEffect(() => {
    if (expense.sourceOriginal) {
      Original(expense.sourceOriginal);
      return;
    }
    const old = expense.evidence?.find((e: Any) => e.scan === 'Clean');
    Original(
      old
        ? {
            ...old,
            url:
              old.mime === 'image/heic'
                ? '/api/reports/' + expense.reportId + '/documents/' + old.id + '/preview'
                : '/api/evidence/' + old.id,
            previewUrl:
              old.mime === 'image/heic'
                ? '/api/reports/' + expense.reportId + '/documents/' + old.id + '/preview'
                : null,
          }
        : null,
    );
  }, [expense.id, expense.sourceOriginal?.id]);
  const options = (kind: string) =>
    masters.filter(
      (m: Any) =>
        m.kind === kind &&
        m.active &&
        (kind !== 'vat' || ![5, 19].includes(Number(JSON.parse(m.json).rate))) &&
        m.from <= d.date &&
        (!m.to || m.to >= d.date),
    );
  const [taxes, Taxes] = useState<Any>(() =>
    Object.fromEntries(
      Array.from(
        new Set([
          0,
          11,
          21,
          ...masters
            .filter((m: Any) => m.kind === 'vat' && m.active)
            .map((m: Any) => Number(JSON.parse(m.json).rate)),
        ]),
      ).map((pct) => {
        const old = d.taxes.find(
          (x: Any) =>
            Number(
              masters.find((m: Any) => m.id === x.rateId)?.json
                ? JSON.parse(masters.find((m: Any) => m.id === x.rateId).json).rate
                : -1,
            ) === pct,
        );
        return [
          pct,
          { ...old, rateId: old?.rateId || '', base: old?.base ?? 0, vat: old?.vat ?? 0 },
        ];
      }),
    ),
  );
  const [states, States] = useState<Any>({ project: 'OPTIONAL', costCenter: 'REQUIRED' });
  const [manualFields, ManualFields] = useState<string[]>([]);
  const [encryptedBackups, EncryptedBackups] = useState<Any[]>([]);
  const [localBackupError, LocalBackupError] = useState('');
  const [ocrVerified, OcrVerified] = useState(false);
  const [draftReady, DR] = useState(false),
    [draftStatus, DS] = useState(''),
    [draftConflictServer, DraftConflictServer] = useState<Any | null>(null),
    [draftSaveFailed, DraftSaveFailed] = useState(false);
  const lastSaved = useRef('');
  const draftRevision = useRef(0);
  const draftConflicted = useRef(false);
  const saving = useRef(false);
  const modified = useRef(false);
  const requestCloseRef = useRef<() => void>(() => {});
  const saveNextRequested = useRef(false);
  const formRef = useRef<HTMLFormElement>(null);
  const titleRef = useRef<HTMLHeadingElement>(null);
  const dialogRef = useRef<HTMLElement>(null);
  const draftQueue = useRef<Promise<any>>(Promise.resolve());
  const draftTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const latestDraft = useRef<Any>({});
  latestDraft.current = { data: d, taxes, sourceVersion: expense.ocrVersion };
  // Crash recovery is opt-in on restore. Never prefer local over server silently.
  useEffect(() => {
    if (!draftStorageKey || expense.id) return;
    let active = true;
    recoverEncryptedDrafts(draftStorageKey)
      .then((records) => { if (active) EncryptedBackups(records); })
      .catch(() => { if (active) LocalBackupError(t(
        'Recuperarea criptată locală nu este disponibilă în acest browser.',
        'Encrypted local recovery is unavailable in this browser.')); });
    return () => { active = false; };
  }, [draftStorageKey, expense.id]);
  useEffect(() => {
    if (!draftReady || !modified.current || !draftStorageKey || expense.id) return;
    const timer = setTimeout(() => {
      void stageEncryptedDraft(draftStorageKey, latestDraft.current)
        .catch(() => LocalBackupError(t(
          'Copierea criptată locală a eșuat; păstrează fila deschisă până se salvează pe server.',
          'Encrypted local backup failed; keep the tab open until server save succeeds.')));
    }, 150);
    return () => clearTimeout(timer);
  }, [d, taxes, draftReady, draftStorageKey, expense.id]);
  // A current snapshot is persisted on close, even if a debounce timer has
  // not fired. The queue serializes optimistic revisions across rapid edits.
  const persistDraft = () => {
    if (!draftReady || !draftStorageKey || expense.id) return Promise.resolve();
    if (draftConflicted.current)
      return Promise.reject(new Error('Draft changed in another tab'));
    const value = latestDraft.current;
    const json = JSON.stringify(value);
    if (lastSaved.current === json) return draftQueue.current;
    const next = draftQueue.current.catch(() => {}).then(async () => {
      if (draftConflicted.current) throw new Error('Draft changed in another tab');
      // Stage before network send, retaining recoverable bytes after a crash.
      try { await stageEncryptedDraft(draftStorageKey, value); }
      catch { LocalBackupError(t('Copie criptată indisponibilă', 'Encrypted backup unavailable')); }
      const saved = await api('/preferences/' + draftStorageKey, 'PUT',
        { ...value, draftRevision: draftRevision.current });
      draftRevision.current = Number(saved.draftRevision);
      lastSaved.current = json;
      void clearOwnEncryptedDraftIfMatch(draftStorageKey, value).catch(() => {});
      DraftSaveFailed(false);
      DS(t('Ciornă salvată automat', 'Draft autosaved'));
    }).catch(async (error: Error) => {
      DraftSaveFailed(true);
      if (error.message.includes('altă filă') || error.message.includes('another tab')) {
        draftConflicted.current = true;
        DS(t('Conflict între file: modificările locale nu au fost suprascrise.',
          'Conflict between tabs: local edits have not been overwritten.'));
        try {
          DraftConflictServer(await api('/preferences/' + draftStorageKey));
        } catch { /* On session expiry, local copy remains visible. */ }
      } else {
        DS(t('Nu s-a putut salva. Verifică sesiunea/conexiunea și reîncearcă.',
          'Save failed. Check connection/session and retry.'));
      }
      throw error;
    });
    draftQueue.current = next;
    return next;
  };
  useEffect(() => {
    if (inline) return; // Embedded review is a normal region, never a modal trap.
    const previouslyFocused = document.activeElement instanceof HTMLElement
      ? document.activeElement
      : null;
    // Only the active dialog may be reached by sequential keyboard navigation.
    // Inert siblings prevent background controls from leaking into the focus order.
    const dialog = dialogRef.current;
    const overlay = dialog?.parentElement;
    const siblings = overlay ? Array.from(overlay.parentElement?.children || []) : [];
    const prior = siblings.filter((element): element is HTMLElement =>
      element instanceof HTMLElement && element !== overlay)
      .map(element => ({ element, inert: element.inert }));
    prior.forEach(({ element }) => { element.inert = true; });
    titleRef.current?.focus();
    return () => {
      prior.forEach(({ element, inert }) => { element.inert = inert; });
      previouslyFocused?.focus();
    };
  }, [inline]);
  useEffect(() => {
    let active = true;
    if (!draftStorageKey || expense.id) {
      DR(true);
      return;
    }
    api('/preferences/' + draftStorageKey)
      .then((saved) => {
        if (
          active &&
          !modified.current &&
          saved.data &&
          (!expense.sourceGroupId || saved.sourceVersion === expense.ocrVersion) &&
          (!expense.ocrCandidate || expense.sourceGroupId) &&
          (!expense.sourceAttachmentId || expense.sourceGroupId)
        ) {
          D((current: Any) => {
            const merged = { ...current, ...saved.data };
            // A stale or incomplete private draft must not erase a new
            // structured OCR suggestion from the same source version.
            for (const [key, source] of Object.entries(expense.ocrCandidate?.fields || {}) as [string, Any][]) {
              if ((merged[key] === '' || merged[key] == null) && source.value != null)
                merged[key] = source.value;
            }
            return merged;
          });
          Taxes(saved.taxes || taxes);
          DS(t('Ciornă restaurată', 'Draft restored'));
        }
        if (active) {
          // The server-side revision is metadata, not part of the editable draft.
          // Compare only user-visible fields to avoid a spurious autosave on load.
          lastSaved.current = saved.data
            ? JSON.stringify({ data: saved.data, taxes: saved.taxes, sourceVersion: saved.sourceVersion })
            : '';
          draftRevision.current = Number(saved.draftRevision || 0);
          DR(true);
        }
      })
      .catch(() => {
        if (active) DR(true);
      });
    return () => {
      active = false;
    };
  }, []);
  useEffect(() => {
    if (!draftReady || !draftStorageKey || expense.id || saving.current || draftConflicted.current) return;
    const json = JSON.stringify(latestDraft.current);
    if (json === lastSaved.current) return;
    const timer = setTimeout(() => {
      if (saving.current) return;
      DS(t('Se salvează ciorna…', 'Saving draft…'));
      void persistDraft().catch(() => {}); // UI keeps error and local draft.
    }, 800);
    draftTimer.current = timer;
    return () => clearTimeout(timer);
  }, [d, taxes, draftReady]);

  useEffect(() => {
    let active = true;
    const timer = setTimeout(
      () =>
        api(
          '/rules?' +
            new URLSearchParams({
              date: d.date,
              country: d.country,
              documentType: d.documentType,
              category: d.category,
              payment: d.payment,
              currency: d.currency,
              hasCompanyCui: String(d.hasCompanyCui),
            }),
        )
          .then((result) => {
            if (active) States(result);
          })
          .catch(() => {}),
      150,
    );
    return () => {
      active = false;
      clearTimeout(timer);
    };
  }, [d.date, d.country, d.documentType, d.category, d.payment, d.currency, d.hasCompanyCui]);
  useEffect(() => {
    if (reportPayment)
      D((old) => ({
        ...old,
        payment: reportPayment,
        cardId: reportPayment === 'CompanyCard' ? reportCardId : null,
      }));
  }, [reportPayment, reportCardId, draftReady]);
  const requestClose = async () => {
    if (saving.current || busy) return;
    if (!modified.current) { onClose(); return; }
    if (expense.id || !draftStorageKey) {
      if (window.confirm(t('Închizi fără salvarea modificărilor?', 'Discard unsaved changes?')))
        onClose();
      return;
    }
    // Never silently discard text after network loss, expired session,
    // another tab's edit or an unflushed debounce.
    if (draftConflicted.current) {
      DraftError(t('Conflict: copiază ciorna locală și compar-o cu versiunea serverului.',
        'Conflict: copy the local draft and compare it with the server version.'));
      return;
    }
    if (!draftReady) {
      DraftError(t('Ciorna încă se încarcă; nu închide până la finalizarea sincronizării.',
        'Draft is loading; do not close before synchronization completes.'));
      return;
    }
    saving.current = true;
    if (draftTimer.current) clearTimeout(draftTimer.current);
    try {
      await draftQueue.current.catch(() => {});
      await persistDraft();
      onClose();
    } catch {
      DraftError(t('Ciorna nu a putut fi salvată. Formularul rămâne deschis; reîncearcă după autentificare/conectare.',
        'Draft could not be saved. The form stays open; retry after signing in/reconnecting.'));
    } finally {
      saving.current = false;
    }
  };
  requestCloseRef.current = requestClose;
  const set = (key: string, value: any) => D((old) => ({ ...old, [key]: value }));
  const domestic = d.country === 'RO',
    vat = domestic && d.hasCompanyCui === true;
  useEffect(() => {
    if (inline) return; // Inline form must not intercept keyboard shortcuts globally.
    const listener = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        e.preventDefault();
        requestCloseRef.current();
      }
      if (e.key === 'Tab') {
        const elements = Array.from(
          dialogRef.current?.querySelectorAll<HTMLElement>(
            'a[href], button, input, select, textarea, iframe, [tabindex]:not([tabindex="-1"])',
          ) || [],
        ).filter((el) => el.getClientRects().length > 0
          && !el.hasAttribute('disabled') && !el.closest('[inert]'));
        const first = elements[0],
          last = elements[elements.length - 1];
        if (e.shiftKey && (document.activeElement === first
          || !dialogRef.current?.contains(document.activeElement))) {
          e.preventDefault();
          last?.focus();
        } else if (!e.shiftKey && (document.activeElement === last
          || !dialogRef.current?.contains(document.activeElement))) {
          e.preventDefault();
          first?.focus();
        }
      }
    };
    window.addEventListener('keydown', listener);
    return () => window.removeEventListener('keydown', listener);
  }, [inline]);
  useEffect(() => {
    let active = true;
    if (!expense.id && expense.reportId && d.date)
      api(
        '/reports/' +
          expense.reportId +
          '/expense-defaults?' +
          new URLSearchParams({ date: d.date }),
      )
        .then((value) => {
          if (active)
            D((old) => ({
              ...old,
              costCenter: old.costCenter || value.costCenter || '',
              project: old.project || value.project || '',
            }));
        })
        .catch(() => {});
    return () => {
      active = false;
    };
  }, [expense.reportId, d.date]);
  useEffect(() => {
    const cc = options('costCenters');
    if (!d.costCenter && cc.length === 1) set('costCenter', cc[0].code);
  }, [masters, d.date]);
  useEffect(() => {
    let active = true;
    Lookup(null);
    LE('');
    if (!vat || !d.cui) {
      Looking(false);
      return;
    }
    Looking(true);
    const timer = setTimeout(() => {
      api('/suppliers/lookup?' + new URLSearchParams({ cui: d.cui, date: d.date }))
        .then((result) => {
          if (!active) return;
          Lookup(result);
          // UX-05: official supplier names never overwrite the printed
          // merchant/trading name that OCR or the employee already entered.
          // The displayed identity and fiscal CUI remain separate.
          if (result.found && result.name)
            D((old) => {
              const requested = String(old.cui || '').replace(/^RO\s*/i, '').replace(/\D/g, '');
              const returned = String(result.cui || '').replace(/^RO\s*/i, '').replace(/\D/g, '');
              return !String(old.supplier || '').trim() && requested && requested === returned
                ? { ...old, supplier: result.name, supplierConfirmed: false } : old;
            });
        })
        .catch((e) => {
          if (active) LE((e as Error).message);
        })
        .finally(() => {
          if (active) Looking(false);
        });
    }, 300);
    return () => {
      active = false;
      clearTimeout(timer);
    };
  }, [d.cui, d.date, vat]);
  useEffect(() => {
    const handler = (event: BeforeUnloadEvent) => {
      const changed =
        modified.current &&
        (expense.id
          ? JSON.stringify(d) !== JSON.stringify(expense.data)
          : JSON.stringify({
              data: d,
              taxes,
              sourceVersion: expense.ocrVersion,
            }) !== lastSaved.current);
      if (changed && !saving.current) {
        event.preventDefault();
        event.returnValue = '';
      }
    };
    window.addEventListener('beforeunload', handler);
    return () => window.removeEventListener('beforeunload', handler);
  }, [d, taxes]);
  const rates = (pct: number) =>
    options('vat').filter((m: Any) => Number(JSON.parse(m.json).rate) === pct);
  const taxRows = Array.from(
    new Set<number>([
      0,
      11,
      21,
      ...options('vat').map((m: Any) => Number(JSON.parse(m.json).rate)),
    ]),
  ).map((pct) => ({
    ...(taxes[pct] || { base: 0, vat: 0 }),
    pct,
    rateId: rates(pct).some((m: Any) => m.id === taxes[pct]?.rateId)
      ? taxes[pct].rateId
      : rates(pct)[0]?.id || '',
  }));
  const reconstructed = sum(taxRows.map((row) => add(row.base || '0', row.vat || '0')));
  const [vatRetry, VatRetry] = useState(0);
  const [vatCheck, VatCheck] = useState<Any | null>(null),
    [vatPending, VatPending] = useState(false),
    [vatCheckError, VatCheckError] = useState('');
  const expected =
    vatCheck?.expected ??
    (d.taxCurrency === 'RON' && d.currency !== 'RON' ? null : String(d.amount || '0'));
  const difference = expected === null ? 0 : sub(reconstructed, expected);
  const used = taxRows.filter(
    (row) =>
      compare(row.base || '0', '0') !== 0 ||
      compare(row.vat || '0', '0') !== 0 ||
      row.state === 0 ||
      row.state === 1,
  );
  const blankTax = taxRows.some((row) => row.base === '' || row.vat === '');
  const localTaxError =
    vat &&
    (blankTax ||
      used.length === 0 ||
      used.some((row) =>
        !row.rateId ||
        (rates(row.pct).length > 1 && !taxes[row.pct]?.rateId) ||
        (row.pct === 0 && compare(row.vat || '0', '0') !== 0)));
  const validationKey = JSON.stringify({
    ...d,
    taxCurrency: d.currency,
    amount: String(d.amount),
    taxes:
      d.vatTreatment && d.vatTreatment !== 'STANDARD'
        ? []
        : used.map((row) => ({ ...row, base: String(row.base), vat: String(row.vat) })),
  });
  useEffect(() => {
    let active = true;
    VatCheck(null);
    VatCheckError('');
    if (!vat) {
      VatPending(false);
      return;
    }
    VatPending(true);
    const timer = setTimeout(() => {
      api('/vat-validation', 'POST', JSON.parse(validationKey))
        .then((result) => {
          if (active) VatCheck({ ...result, key: validationKey });
        })
        .catch((e) => {
          if (active) VatCheckError((e as Error).message);
        })
        .finally(() => {
          if (active) VatPending(false);
        });
    }, 250);
    return () => {
      active = false;
      clearTimeout(timer);
    };
  }, [validationKey, vat, vatRetry]);
  const validationCurrent = vatCheck?.key === validationKey;
  const taxError =
    vat &&
    (((!d.vatTreatment || d.vatTreatment === 'STANDARD') && localTaxError) ||
      vatPending ||
      !validationCurrent ||
      !!vatCheckError ||
      vatCheck?.issues?.some((p: Any) => p.blocking));
  const supplierError =
    vat && (looking || !lookup?.valid || (!lookup.found && !d.supplierConfirmed));
  const updateTax = (pct: number, key: string, value: any) =>
    Taxes((old: Any) => ({ ...old, [pct]: { ...old[pct], [key]: value } }));
  // Suggestions are navigation aids only. The original document remains the source of truth.
  const ocrShortcuts = (['date', 'invoice', 'supplier', 'cui', 'description', 'currency', 'amount'] as const)
    .filter((key) => vat || !['invoice', 'supplier', 'cui'].includes(key))
    .map((key) => ({ key, value: expense.ocrCandidate?.fields?.[key]?.value }))
    .filter((field) => field.value != null && String(field.value).trim().length > 0);
  const ocrTaxShortcuts =
    vat && (!d.vatTreatment || d.vatTreatment === 'STANDARD')
      ? (expense.ocrCandidate?.taxes || []).filter((tax: Any) =>
          taxRows.some((row) => row.pct === Number(tax.rate)),
        )
      : [];
  const focusOcrField = (key: string) => {
    const field = formRef.current?.querySelector<HTMLElement>(`[data-ocr-field="${key}"]`);
    field?.scrollIntoView({ block: 'center' });
    field?.focus();
  };

  const sourceHighlights = [
    ...Object.entries(expense.ocrCandidate?.fields || {})
      .filter(([key, field]: [string, any]) =>
        field?.box && field.value != null &&
        String(d[key] ?? '').trim() === String(field.value).trim())
      .map(([_key, field]: [string, any]) => ({ page: field.page, ...field.box })),
    ...(d.country === 'RO' && d.hasCompanyCui === true
      ? (expense.ocrCandidate?.taxes || [])
          .filter((tax: Any) => tax.box)
          .map((tax: Any) => ({ page: tax.page, ...tax.box }))
      : []),
  ];
  const scanAction = (key: string) =>
    expense.sourceGroupId && expense.sourceAttachmentId && original ? (
      <button type="button" className="ocr-scan-action"
        aria-label={t('Selectează ' + key + ' din document', 'Select ' + key + ' from document')}
        onClick={() => { ScanField(key); ScanRegion(null); ScanError(''); }}>
        {t('Selectează pe bon', 'Select on receipt')}
      </button>
    ) : null;
  const trackedFields = vat
    ? ['date', 'invoice', 'supplier', 'cui', 'description', 'currency', 'amount']
    : ['date', 'description', 'currency', 'amount'];
  const needsAttention = expense.ocrCandidate
    ? attentionFields(d, expense.ocrCandidate.fields || {}, manualFields, ocrVerified, trackedFields)
    : [];
  const statusAction = (key: string) => {
    const state = fieldProvenance({
      value: d[key], source: expense.ocrCandidate?.fields?.[key] || null,
      changed: manualFields.includes(key), verified: ocrVerified,
      calculated: key === 'costCenter' || key === 'project',
    });
    const names: Record<string, [string, string]> = {
      unread: ['Necitit · verifică', 'Unread · check source'],
      ocr: ['Propus OCR', 'OCR proposal'],
      calculated: ['Calculat', 'Calculated'],
      manual: ['Modificat manual', 'Entered / changed manually'],
      verified: ['Verificat de utilizator', 'Human verified'],
    };
    return (
      <>
        <span className={'small receipt-provenance status-' + state}
          aria-label={t('Starea câmpului', 'Field status') + ': ' + t(...names[state])}>
          {t(...names[state])}
        </span>
        {scanAction(key)}
      </>
    );
  };
  const applyScan = async (reread = false) => {
    if (!scanField || !scanRegion || !expense.sourceGroupId || !expense.sourceAttachmentId) return;
    ScanBusy(true);
    ScanError('');
    try {
      const detail = await api('/reports/' + expense.reportId);
      if (finance && financeScanReason.trim().length < 3)
        throw new Error(t('Completează motivul corecției Finance.', 'Enter a reason for the Finance correction.'));
      await api(
        '/reports/' + expense.reportId + '/documents/' + expense.sourceAttachmentId
          + '/groups/' + expense.sourceGroupId + '/select-field',
        'POST',
        { version: detail.report.version, field: scanField, region: scanRegion,
          reread, reason: finance ? financeScanReason : 'Employee verified the source region' },
      );
      const review = await api(
        '/reports/' + expense.reportId + '/documents/' + expense.sourceAttachmentId + '/review',
      );
      const updated = review.review?.candidates?.find((g: Any) => g.id === expense.sourceGroupId);
      const value = updated?.fields?.[scanField]?.value;
      if (value == null) throw new Error(t('Valoarea nu a fost recunoscută. Scrie-o manual.',
        'The value could not be recognised. Enter it manually.'));
      if (scanField.startsWith('taxBase:') || scanField.startsWith('taxVat:')) {
        const [kind, rate] = scanField.split(':');
        updateTax(Number(rate), kind === 'taxBase' ? 'base' : 'vat', String(value));
      } else {
        set(scanField, String(value));
        if (scanField === 'currency') set('taxCurrency', String(value));
      }
      ManualHighlights((old) => [...old, scanRegion]);
      FS(scanRegion);
      FP(scanRegion.page);
      FT((n: number) => n + 1);
      ScanField(null);
      ScanRegion(null);
      await onSourceSelected?.();
    } catch (e) {
      ScanError((e as Error).message);
    } finally {
      ScanBusy(false);
    }
  };
  const fileSection = (
    <section className="receipt-section receipt-evidence-note">
      <h3>{t('Document justificativ', 'Supporting document')}</h3>
      <p className="receipt-hint">
        {original
          ? t('Acest bon este asociat documentului încărcat. Verifică în previzualizare înainte de salvare.',
              'This receipt is linked to the uploaded document. Verify it in the preview before saving.')
          : t('Încarcă documentul din secțiunea unică de la începutul decontului, apoi asociază-l bonului.',
              'Upload the document in the single upload section at the top of the report, then link it to this receipt.')}
      </p>
    </section>
  );
  return (
    <div className={inline ? 'receipt-workbench-content' : 'modal-overlay'}>
      <section
        className={inline ? 'receipt-modal receipt-modal-inline' : 'modal receipt-modal'}
        ref={dialogRef}
        role={inline ? 'region' : 'dialog'}
        aria-modal={inline ? undefined : true}
        aria-labelledby="expense-title"
      >
        <div className="section-heading">
          <div>
            <h2 id="expense-title" ref={titleRef} tabIndex={-1}>
              {expense.id
                ? t('Editează bonul / documentul', 'Edit receipt / document')
                : t('Adaugă un bon / document', 'Add receipt / document')}
            </h2>
            <p className="muted small">
              {t(
                'Un rând în decont pentru fiecare bon. Completează valorile de pe documentul original.',
                'One report line per receipt. Copy values from the original document.',
              )}
            </p>
          </div>
          <button className="icon" aria-label={t('Închide', 'Close')} onClick={requestClose}>
            <X />
          </button>
        </div>
        <div className={original ? 'receipt-editor-layout with-original' : 'receipt-editor-layout'}>
          {original && (
            <aside className="receipt-original">
              <DocumentViewer
                key={original.url}
                document={{ ...original, reportId: expense.reportId }}
                t={t}
                regions={expense.ocrCandidate?.regions || []}
                highlights={[...sourceHighlights, ...manualHighlights]}
                onRegionSelect={scanField ? ScanRegion : undefined}
                focus={focusedSource}
                focusPage={focusedPage}
                focusToken={focusedToken}
              />
              <p className="small muted">
                {t('Zonele verzi sunt valori citite și folosite în formular; zona albastră indică informația selectată.',
                   'Green regions are recognised values used in the form; the blue region is the selected source.')}
              </p>
              {scanField && (
                <section className="ocr-scan-panel" role="region" aria-label={t('Selectare valoare din document', 'Select value from document')}>
                  <h4>{t('Selectează valoarea pe document', 'Select the value on the document')}</h4>
                  <p>{t('Apasă „Delimitează regiunea”, trasează peste text și confirmă. Poți oricând scrie manual în câmp.',
                      'Choose “Select source region”, draw over the text and confirm. You can always type the value manually.')}</p>
                  {scanRegion && <p role="status">{t('Regiune selectată', 'Region selected')}: {t('pagina', 'page')} {scanRegion.page}</p>}
                  {finance && (
                    <Field name={t('Motivul corecției OCR (Finance)', 'Reason for OCR correction (Finance)')}>
                      <input required minLength={3} value={financeScanReason}
                        onChange={(e) => FinanceScanReason(e.target.value)}
                        placeholder={t('Ce informație a fost corectată?', 'What source information was corrected?')} />
                    </Field>
                  )}
                  {scanError && <p className="error" role="alert">{scanError}</p>}
                  <div className="actions">
                    <button type="button" className="primary"
                      disabled={!scanRegion || scanBusy || (finance && financeScanReason.trim().length < 3)}
                      onClick={() => applyScan(false)}>
                      {scanBusy ? t('Se citește…', 'Reading…') : t('Folosește textul existent', 'Use existing text')}
                    </button>
                    <button type="button" className="secondary"
                      disabled={!scanRegion || scanBusy || (finance && financeScanReason.trim().length < 3)}
                      onClick={() => applyScan(true)}>
                      {t('Recitește zona cu OCR', 'Re-read region with OCR')}
                    </button>
                    <button type="button" className="secondary" onClick={() => { ScanField(null); ScanRegion(null); ScanError(''); }}>
                      {t('Completez manual', 'Enter manually')}
                    </button>
                  </div>
                </section>
              )}
            </aside>
          )}
          <form
            ref={formRef}
            onChangeCapture={(event) => {
              modified.current = true;
              OcrVerified(false);
              const key = (event.target as HTMLElement).dataset.ocrField;
              if (key) ManualFields(old => old.includes(key) ? old : [...old, key]);
            }}
            onFocusCapture={(e) => {
              const key = (e.target as HTMLElement).dataset.ocrField;
              if (!key || !expense.ocrCandidate) return;
              const field = key.startsWith('tax:')
                ? expense.ocrCandidate.taxes?.find(
                    (v: Any) => Number(v.rate) === Number(key.slice(4)),
                  )
                : expense.ocrCandidate.fields?.[key];
              if (field) {
                FP(field.page);
                FS(field.box ? { page: field.page, ...field.box } : null);
                FT((n) => n + 1);
              }
            }}
            onSubmit={async (e) => {
              e.preventDefault();
              if ((taxError && !finance) || supplierError) return;
              saving.current = true;
              if (draftTimer.current) clearTimeout(draftTimer.current);
              await draftQueue.current.catch(() => {});
              try {
                const openNext = saveNextRequested.current;
                saveNextRequested.current = false;
                const savedSnapshot = latestDraft.current;
                await save(
                  {
                    ...d,
                    taxCurrency: d.currency,
                    amount: String(d.amount),
                    documentType: [
                      'Receipt',
                      'Invoice',
                      'InvoiceWithCui',
                      'InvoiceWithoutCui',
                    ].includes(d.documentType)
                      ? d.documentType === 'Receipt'
                        ? 'Receipt'
                        : d.hasCompanyCui
                          ? 'InvoiceWithCui'
                          : 'InvoiceWithoutCui'
                      : d.documentType,
                    hasCompanyCui: domestic ? d.hasCompanyCui : false,
                    cui: vat ? d.cui : null,
                    supplier: String(d.supplier || '').trim(),
                    supplierConfirmed: vat && !lookup?.found ? d.supplierConfirmed : false,
                    taxes: vat
                      ? d.vatTreatment && d.vatTreatment !== 'STANDARD'
                        ? []
                        : used.map((row) => ({
                            ...row,
                            base: String(row.base),
                            vat: String(row.vat),
                          }))
                      : [],
                    cardId: d.payment === 'CompanyCard' ? d.cardId : null,
                  },
                  [],
                  openNext,
                );
                if (!expense.id && draftStorageKey) {
                  void clearOwnEncryptedDraftIfMatch(draftStorageKey, savedSnapshot).catch(() => {});
                }
                if (!expense.id && draftStorageKey && !draftConflicted.current) {
                  try {
                    const cleared = await api('/preferences/' + draftStorageKey, 'PUT',
                      { draftRevision: draftRevision.current });
                    draftRevision.current = Number(cleared.draftRevision || 0);
                  } catch {
                    // Saving the receipt succeeded. Preserve a possibly newer
                    // concurrent draft instead of deleting someone else's work.
                  }
                }
              } finally {
                saving.current = false;
              }
            }}
          >
            {expense.ocrCandidate && (
              <div className={'issue ' + (needsAttention.length ? 'warning' : 'success')}
                role="status" aria-live="polite">
                <strong>{needsAttention.length
                  ? t(needsAttention.length + ' câmpuri necesită atenție înainte de confirmare',
                      needsAttention.length + ' fields need review before confirmation')
                  : t('Câmpurile completate au fost confirmate', 'Completed fields have been confirmed')
                }</strong>
                {needsAttention.length > 0 && (
                  <div className="actions">
                    {needsAttention.map(key => (
                      <button type="button" key={key} className="text-button"
                        onClick={() => focusOcrField(key)}>
                        {ocrFieldLabel(key, t)}
                      </button>
                    ))}
                  </div>
                )}
              </div>
            )}
            {expense.ocrCandidate && (
              <section className="receipt-ocr-review" aria-label={t('Propuneri OCR', 'OCR suggestions')}>
                <h3>{t('Verifică datele propuse de OCR', 'Review the OCR suggestions')}</h3>
                <p>
                  {t(
                    'Selectează o valoare ca să ajungi la câmpul ei și să verifici locul din original. Corectează ce este greșit sau lipsește.',
                    'Select a value to open its field and check the source on the original. Correct anything missing or wrong.',
                  )}
                </p>
                {(ocrShortcuts.length > 0 || ocrTaxShortcuts.length > 0) && (
                  <div className="ocr-shortcuts">
                    {ocrShortcuts.map(({ key, value }) => (
                      <button
                        key={key}
                        type="button"
                        className="ocr-shortcut"
                        onClick={() => focusOcrField(key)}
                      >
                        <span>{ocrFieldLabel(key, t)}</span>
                        <strong>{String(value)}</strong>
                      </button>
                    ))}
                    {ocrTaxShortcuts.map((tax: Any, i: number) => (
                      <button
                        key={tax.rate + '-' + i}
                        type="button"
                        className="ocr-shortcut"
                        onClick={() => focusOcrField('tax:' + tax.rate)}
                      >
                        <span>{t('TVA', 'VAT')} {tax.rate}%</span>
                        <strong>{tax.base} + {tax.vat}</strong>
                      </button>
                    ))}
                  </div>
                )}
                <p className="receipt-hint">
                  {t(
                    'Aceste valori sunt doar sugestii, nu confirmări. Documentul original este afișat lângă formular.',
                    'These values are suggestions, not confirmations. The original document is shown beside the form.',
                  )}
                </p>
              </section>
            )}
            {draftError && (
              <p className="error" role="alert">
                {draftError}
              </p>
            )}
            {draftStatus && (
              <p className="small muted" role="status">
                {draftStatus}
              </p>
            )}
            {draftSaveFailed && !draftConflicted.current && (
              <button type="button" className="secondary" onClick={() =>
                void persistDraft().catch(() => {})}>
                {t('Reîncearcă salvarea ciornei', 'Retry saving draft')}
              </button>
            )}
            {draftConflicted.current && (
              <section className="issue blocking" role="alert">
                <h4>{t('Conflict între două file', 'Conflict between tabs')}</h4>
                <p>{t('Nu suprascriem ciorna de pe server. Datele tale locale rămân în formular; compară și copiază înainte de reîncărcare.',
                  'We will not overwrite the server draft. Your local edits remain in the form; compare and copy before reloading.')}</p>
                <details>
                  <summary>{t('Compară versiunile ciornei', 'Compare draft versions')}</summary>
                  <label>{t('Modificările din această filă', 'Local changes')}
                    <textarea readOnly rows={6} value={JSON.stringify(latestDraft.current, null, 2)}/>
                  </label>
                  {draftConflictServer && <label>{t('Ciorna de pe server', 'Server draft')}
                    <textarea readOnly rows={6} value={JSON.stringify(draftConflictServer, null, 2)}/>
                  </label>}
                </details>
              </section>
            )}
            {compare(d.amount || '0', '0') < 0 && (
              <p className="issue warning">
                {t(
                  'Sumă negativă: verifică documentul și sensul operațiunii. Finance va primi aceeași avertizare.',
                  'Negative amount: check evidence and transaction direction. Finance will receive the same warning.',
                )}
              </p>
            )}
            {fileSection}
            <section className="receipt-section">
              <h3>
                {t('Tipul cheltuielii și data', 'Expense category and date')}
              </h3>
              {expense.ocrCandidate?.categorySuggestions?.length > 0 && (
                <div className="receipt-hint">
                  <p>
                    {t(
                      'Categorii sugerate — confirmă alegerea',
                      'Suggested categories — confirm your choice',
                    )}
                  </p>
                  {expense.ocrCandidate.categorySuggestions
                    .filter((s: Any) => options('expenseTypes').some((m: Any) => m.code === s.code))
                    .map((s: Any) => (
                      <button
                        type="button"
                        className="secondary"
                        key={s.code}
                        onClick={() => set('category', s.code)}
                      >
                        {t(s.nameRo, s.nameEn)} · {s.matchedTerms.join(', ')}
                      </button>
                    ))}
                </div>
              )}
              <div className="form-grid">
                <Field name={t('Tip cheltuială', 'Expense category')}>
                  <select
                    required
                    value={d.category}
                    onChange={(e) => set('category', e.target.value)}
                  >
                    <option value="">{t('Selectează', 'Select')}</option>
                    {options('expenseTypes').map((m: Any) => (
                      <option key={m.id} value={m.code}>
                        {label(m)}
                      </option>
                    ))}
                  </select>
                </Field>
                <Field name={t('Data bonului / documentului', 'Receipt / document date')} action={statusAction('date')}>
                  <input
                    data-ocr-field="date"
                    required
                    type="date"
                    max={new Date().toISOString().slice(0, 10)}
                    value={d.date}
                    onChange={(e) => {
                      set('date', e.target.value);
                      set('supplierConfirmed', false);
                    }}
                  />
                </Field>
              </div>
              <Field name={t('Tip document', 'Document type')}>
                <select
                  value={
                    ['InvoiceWithCui', 'InvoiceWithoutCui'].includes(d.documentType)
                      ? 'Invoice'
                      : d.documentType
                  }
                  onChange={(e) =>
                    set(
                      'documentType',
                      e.target.value === 'Invoice' ? 'InvoiceWithCui' : e.target.value,
                    )
                  }
                >
                  <option value="Receipt">{t('Bon fiscal', 'Receipt')}</option>
                  <option value="Invoice">{t('Factură', 'Invoice')}</option>
                  {options('documentTypes')
                    .filter(
                      (m: Any) =>
                        !['Receipt', 'Invoice', 'InvoiceWithCui', 'InvoiceWithoutCui'].includes(
                          m.code,
                        ),
                    )
                    .map((m: Any) => (
                      <option key={m.id} value={m.code}>
                        {label(m)}
                      </option>
                    ))}
                </select>
              </Field>
              <Field name={t('Furnizorul este din România?', 'Is the supplier from Romania?')}>
                <select
                  required
                  value={!d.country ? '' : domestic ? 'yes' : 'no'}
                  onChange={(e) =>
                    D((old) => ({
                      ...old,
                      country:
                        e.target.value === 'yes' ? 'RO' : e.target.value === 'no' ? 'ZZ' : '',
                      hasCompanyCui: e.target.value === 'yes'
                        ? (old.country === 'RO' ? old.hasCompanyCui : null) : false,
                      cui: e.target.value === 'yes' ? old.cui : null,
                      supplier: old.supplier,
                      supplierConfirmed: e.target.value === 'yes' ? old.supplierConfirmed : false,
                      taxCurrency: old.currency,
                    }))
                  }
                >
                  <option value="">
                    {t('Selectează țara furnizorului', 'Select supplier country')}
                  </option>
                  <option value="yes">{t('Da, din România', 'Yes, Romania')}</option>
                  <option value="no">{t('Nu, din străinătate', 'No, outside Romania')}</option>
                </select>
              </Field>
              {!!d.country &&
                !domestic &&
                options('countries').some((m: Any) => m.code !== 'RO') && (
                  <Field name={t('Țara furnizorului (opțional)', 'Supplier country (optional)')}>
                    <select value={d.country} onChange={(e) => set('country', e.target.value)}>
                      <option value="ZZ">{t('Țară nespecificată', 'Unspecified country')}</option>
                      {options('countries')
                        .filter((m: Any) => m.code !== 'RO' && m.code !== 'ZZ')
                        .map((m: Any) => (
                          <option key={m.id} value={m.code}>
                            {label(m)}
                          </option>
                        ))}
                    </select>
                  </Field>
                )}
              {!!d.country && !domestic && (
                <p className="receipt-hint">
                  {t(
                    'Nu se solicită CUI furnizor sau defalcare TVA. Completează descrierea, moneda și suma bonului.',
                    'Supplier VAT ID and domestic VAT breakdown are not required. Enter description, currency and receipt amount.',
                  )}
                </p>
              )}
              {domestic && (
                <Field
                  name={t(
                    'Bonul are înscris CUI-ul companiei?',
                    'Does the receipt show the company VAT ID?',
                  )}
                  note={t(
                    'Este CUI-ul companiei care decontează, nu CUI-ul furnizorului.',
                    'This is the reimbursing company VAT ID, not the supplier VAT ID.',
                  )}
                >
                  <select
                    required
                    value={d.hasCompanyCui === null ? '' : d.hasCompanyCui ? 'yes' : 'no'}
                    onChange={(e) =>
                      D((old) => ({
                        ...old,
                        hasCompanyCui: e.target.value === '' ? null : e.target.value === 'yes',
                        cui: old.cui,
                        supplier: old.supplier,
                        supplierConfirmed: old.supplierConfirmed,
                        taxCurrency: old.currency,
                      }))
                    }
                  >
                    <option value="">{t('Selectează da sau nu', 'Select yes or no')}</option>
                    <option value="yes">
                      {t('Da, CUI-ul companiei apare pe bon', 'Yes, company VAT ID is printed')}
                    </option>
                    <option value="no">
                      {t(
                        'Nu, CUI-ul companiei nu apare pe bon',
                        'No, company VAT ID is not printed',
                      )}
                    </option>
                  </select>
                </Field>
              )}
              {domestic && d.hasCompanyCui === false && (
                <p className="receipt-hint">
                  {t(
                    'Fără CUI-ul companiei: completează descrierea, moneda și suma. Nu completa CUI furnizor sau TVA.',
                    'Without company VAT ID: enter description, currency and amount. Do not enter supplier VAT ID or VAT breakdown.',
                  )}
                </p>
              )}
            </section>
            {vat && (
              <section className="receipt-section">
                <h3>
                  {t(
                    'Furnizor și identificarea documentului',
                    'Supplier and document details',
                  )}
                </h3>
                <div className="form-grid">
                  <Field name={t('CUI furnizor', 'Supplier VAT ID')} action={statusAction('cui')}>
                    <input
                      data-ocr-field="cui"
                      required
                      value={d.cui || ''}
                      onChange={(e) =>
                        D((old) => ({
                          ...old,
                          cui: e.target.value,
                          supplier: '',
                          supplierConfirmed: false,
                        }))
                      }
                    />
                  </Field>
                  <Field name={t('Număr bon / document', 'Receipt / document number')} action={statusAction('invoice')}>
                    <input
                      data-ocr-field="invoice"
                      required
                      value={d.invoice}
                      onChange={(e) => set('invoice', e.target.value)}
                    />
                  </Field>
                </div>
                {looking && (
                  <p role="status">
                    {t(
                      'Se verifică furnizorul în baza internă…',
                      'Checking the supplier in the internal database…',
                    )}
                  </p>
                )}
                {lookup && (
                  <div
                    className={'supplier-result ' + (lookup.found ? 'found' : 'missing')}
                    role="status"
                  >
                    {lookup.found ? <CheckCircle2 size={18} /> : <AlertCircle size={18} />}
                    <span>
                      {t(lookup.messageRo, lookup.messageEn)}
                      {lookup.found && (
                        <strong>
                          {lookup.name} · CUI {lookup.cui}
                        </strong>
                      )}
                    </span>
                  </div>
                )}
                {d.cui && (
                  <section className="receipt-optional">
                    <h4>
                      {t('Verificare suplimentară a furnizorului', 'Additional supplier checks')}
                    </h4>
                    <div className="actions">
                    <button
                      type="button"
                      className="secondary"
                      onClick={async () => {
                        try {
                          External(
                            await api('/suppliers/validate', 'POST', { cui: d.cui, date: d.date }),
                          );
                        } catch (e) {
                          LE((e as Error).message);
                        }
                      }}
                    >
                      {t(
                        'Verifică prin serviciul extern (opțional)',
                        'Check external service (optional)',
                      )}
                    </button>
                    {external && (
                      <span role="status">{t(external.messageRo, external.messageEn)}</span>
                    )}
                    {finance &&
                      external &&
                      (external.state !== 'Verified' || external.valid === false) && (
                        <>
                          <Field
                            name={t(
                              'Motiv verificare manuală CUI',
                              'Manual VAT ID verification reason',
                            )}
                          >
                            <input value={externalReason} onChange={(e) => ER(e.target.value)} />
                          </Field>
                          <button
                            type="button"
                            className="secondary"
                            disabled={externalReason.trim().length < 3}
                            onClick={async () => {
                              try {
                                External({
                                  ...(await api('/suppliers/validation-override', 'POST', {
                                    cui: d.cui,
                                    date: d.date,
                                    reason: externalReason,
                                  })),
                                  messageRo: 'Verificat manual de Finance.',
                                  messageEn: 'Manually verified by Finance.',
                                });
                              } catch (e) {
                                LE((e as Error).message);
                              }
                            }}
                          >
                            {t('Confirmă verificarea manuală', 'Confirm manual verification')}
                          </button>
                        </>
                      )}
                    </div>
                  </section>
                )}
                {lookup?.found &&
                  expense.ocrCandidate?.fields?.supplier?.value &&
                  expense.ocrCandidate.fields.supplier.value.trim().toUpperCase() !==
                    lookup.name.trim().toUpperCase() && (
                    <p className="issue warning">
                      {t(
                        'Denumirea citită diferă de registru. Verifică originalul și CUI-ul.',
                        'Printed supplier name differs from the registry. Check the original and VAT ID.',
                      )}{' '}
                      · {expense.ocrCandidate.fields.supplier.value}
                    </p>
                  )}
                {lookupError && (
                  <p className="error" role="alert">
                    {lookupError}
                  </p>
                )}
                <Field name={t('Denumire furnizor', 'Supplier name')} action={statusAction('supplier')}>
                  <input
                    data-ocr-field="supplier"
                    required
                    value={d.supplier || ''}
                    onChange={(e) => set('supplier', e.target.value)}
                  />
                </Field>
                {lookup?.found && lookup.name &&
                  String(d.supplier || '').trim().toLocaleLowerCase() !== lookup.name.trim().toLocaleLowerCase() && (
                  <div className="issue warning" role="status">
                    {t('Numele comercial extras diferă de denumirea fiscală. Numele tipărit rămâne păstrat.',
                      'The printed trading name differs from the fiscal name. The printed name is preserved.')}
                    <button className="text-button" type="button" onClick={() =>
                      D((old) => ({ ...old, supplier: lookup.name, supplierConfirmed: false }))}>
                      {t('Folosește explicit denumirea fiscală', 'Explicitly use fiscal business name')}
                    </button>
                  </div>
                )}
                {lookup?.valid && !lookup.found && (
                  <label className="confirmation">
                    <input
                      type="checkbox"
                      required
                      checked={d.supplierConfirmed}
                      onChange={(e) => set('supplierConfirmed', e.target.checked)}
                    />
                    {t(
                      'Am verificat CUI-ul pe bon și confirm că este corect. Furnizorul va fi completat de Finance.',
                      'I checked the VAT ID on the receipt and confirm it is correct. Finance will add the supplier.',
                    )}
                  </label>
                )}
              </section>
            )}
            <section className="receipt-section">
              <h3>{t('Descriere și sumă', 'Description and amount')}</h3>
              <Field name={t('Descriere / justificare', 'Description / business justification')} action={statusAction('description')}>
                <textarea
                  data-ocr-field="description"
                  required
                  value={d.description}
                  onChange={(e) => set('description', e.target.value)}
                />
              </Field>
              <div className="form-grid">
                <Field name={t('Moneda bonului', 'Receipt currency')} action={statusAction('currency')}>
                  <input
                    data-ocr-field="currency"
                    required
                    pattern="[A-Z]{3}"
                    maxLength={3}
                    value={d.currency}
                    onChange={(e) =>
                      D((old) => ({
                        ...old,
                        currency: e.target.value.toUpperCase(),
                        taxCurrency: e.target.value.toUpperCase(),
                      }))
                    }
                  />
                </Field>
                <Field
                  name={t('Total bon în moneda documentului', 'Receipt total in document currency')} action={statusAction('amount')}
                >
                  <input
                    data-ocr-field="amount"
                    required
                    type="number"
                    step="any"
                    value={d.amount || ''}
                    onChange={(e) => set('amount', e.target.value)}
                  />
                </Field>
              </div>
              {vat && (
                <section className="receipt-section">
                  <Field
                    name={t('Tratament TVA document', 'Document VAT treatment')}
                    note={t(
                      'Tratamentele speciale necesită o politică aprobată Finance.',
                      'Special treatments require an approved Finance policy.',
                    )}
                  >
                    <select
                      value={d.vatTreatment || 'STANDARD'}
                      onChange={(e) => set('vatTreatment', e.target.value)}
                    >
                      {[
                        ['STANDARD', t('TVA pe cote', 'VAT by rate')],
                        ['EXEMPT', t('Scutit', 'Exempt')],
                        ['OUTSIDE_SCOPE', t('În afara sferei', 'Outside scope')],
                        ['NOT_PRESENT', t('TVA neprezentat', 'VAT not present')],
                        ['UNKNOWN', t('Necunoscut — de verificat', 'Unknown — review needed')],
                      ].map(([value, name]) => (
                        <option key={value} value={value}>
                          {name}
                        </option>
                      ))}
                    </select>
                  </Field>
                  {d.vatTreatment && d.vatTreatment !== 'STANDARD' && (
                    <>
                      <Field name={t('Justificare tratament TVA', 'VAT treatment explanation')}>
                        <textarea
                          required
                          value={d.vatExplanation || ''}
                          onChange={(e) => set('vatExplanation', e.target.value)}
                        />
                      </Field>
                      {vatCheck?.issues?.map((issue: Any, i: number) => (
                        <p className={'issue ' + (issue.blocking ? 'blocking' : 'warning')} key={i}>
                          {t(issue.ro, issue.en)}
                        </p>
                      ))}
                    </>
                  )}
                </section>
              )}
              {vat && (!d.vatTreatment || d.vatTreatment === 'STANDARD') && (
                <div className="vat-breakdown">
                  <h3>
                    {t(
                      'Baze și TVA pe cote — valori de pe bon',
                      'Taxable bases and VAT by rate — receipt values',
                    )}
                  </h3>
                  <p>
                    {t(
                      'Pentru fiecare cotă de pe bon, copiază baza fără TVA și TVA-ul plătit. Lasă 0 la cotele care nu apar. Totalul se recompune din toate bazele + TVA.',
                      'For each rate on the receipt, copy the base excluding VAT and the VAT paid. Leave unused rates at 0. The total is reconstructed from all bases plus VAT.',
                    )}
                  </p>
                  <p className="small muted">
                    {t('Baza și TVA sunt în moneda documentului: ', 'Tax base and paid VAT use the document currency: ')}
                    <strong>{d.currency}</strong>.
                  </p>
                  <div className="vat-grid vat-head">
                    <span>{t('Cotă', 'Rate')}</span>
                    <span>{t('Bază fără TVA', 'Base excluding VAT')}</span>
                    <span>{t('TVA plătit', 'VAT paid')}</span>
                    <span>{t('Total cotă', 'Rate total')}</span>
                  </div>
                  {taxRows.map((row) => (
                    <div key={row.pct} className="vat-grid">
                      <strong>{row.pct}%</strong>
                      <Field name={t('Bază ' + row.pct + '%', 'Base ' + row.pct + '%')}
                        action={scanAction('taxBase:' + row.pct)}>
                        <input
                          data-ocr-field={'tax:' + row.pct}
                          type="number"
                          step="any"
                          required
                          value={row.base}
                          onChange={(e) => updateTax(row.pct, 'base', e.target.value)}
                        />
                      </Field>
                      <Field name={t('TVA ' + row.pct + '%', 'VAT ' + row.pct + '%')}
                        action={row.pct === 0 ? null : scanAction('taxVat:' + row.pct)}>
                        <input
                          type="number"
                          step="any"
                          required
                          readOnly={row.pct === 0}
                          value={row.vat}
                          onChange={(e) => updateTax(row.pct, 'vat', e.target.value)}
                        />
                      </Field>
                      {expense.ocrCandidate?.taxes?.some(
                        (tax: Any) => Number(tax.rate) === row.pct
                      ) && (
                        <small className="receipt-hint">
                          {expense.ocrCandidate.taxes.some(
                            (tax: Any) => Number(tax.rate) === row.pct && tax.source === 'Derived'
                          ) ? t(
                            'ATENȚIE: baza TVA este calculată, nu citită din bon. Confirmă valoarea pe original.',
                            'WARNING: taxable base is calculated, not read from the receipt. Verify it against the original.'
                          ) : t(
                            'Propus din original; verifică baza și TVA-ul efectiv.',
                            'Suggested from the original; verify the taxable base and actual paid VAT.'
                          )}
                        </small>
                      )}
                      <span>
                        {formatMoney(
                          add(row.base || '0', row.vat || '0'),
                          undefined,
                          vatCheck?.calculation?.policy?.exponent ?? 2,
                        )}{' '}
                        {d.taxCurrency}
                      </span>
                      {rates(row.pct).length > 1 && (
                        finance ? (
                          <Field name={t('Mapare TVA pentru cotă ' + row.pct + '%',
                            'VAT mapping for rate ' + row.pct + '%')}
                            note={t('Finance alege explicit maparea contabilă. Se păstrează în bon și în istoric.',
                                    'Finance explicitly selects the accounting mapping. The choice is retained in the receipt and audit history.')}>
                            <select required value={taxes[row.pct]?.rateId || ''}
                              onChange={(e) => updateTax(row.pct, 'rateId', e.target.value)}>
                              <option value="">{t('Alege maparea activă', 'Choose active mapping')}</option>
                              {rates(row.pct).map((m: Any) => (
                                <option key={m.id} value={m.id}>
                                  {m.code} · {t(m.nameRo, m.nameEn)}
                                </option>
                              ))}
                            </select>
                          </Field>
                        ) : (
                          <p className="vat-mapping issue blocking" role="alert">
                            {t('Sunt mai multe mapări TVA active pentru această cotă. Finance trebuie să aleagă maparea corectă.',
                               'Multiple VAT mappings exist for this rate. Finance must select the correct accounting mapping.')}
                          </p>
                        )
                      )}
                      {!row.rateId &&
                        (compare(row.base || '0', '0') !== 0 ||
                          compare(row.vat || '0', '0') !== 0) && (
                          <p className="vat-mapping issue blocking">
                            {t(
                              'Lipsește maparea activă pentru această cotă. Contactează Finance.',
                              'Active mapping is missing for this rate. Contact Finance.',
                            )}
                          </p>
                        )}
                    </div>
                  ))}
                  <div
                    className={'vat-reconstruction ' + (taxError ? 'invalid' : 'valid')}
                    role="status"
                  >
                    <span>
                      {t('Total recompus', 'Reconstructed total')}:{' '}
                      <small className="receipt-provenance status-calculated">{t('Calculat', 'Calculated')}</small>{' '}
                      <strong>
                        {formatMoney(
                          reconstructed,
                          undefined,
                          vatCheck?.calculation?.policy?.exponent ?? 2,
                        )}{' '}
                        {d.taxCurrency}
                      </strong>
                    </span>
                    <span>
                      {t('Total bon', 'Receipt total')}:{' '}
                      <strong>
                        {expected === null
                          ? t('Se verifică prin cursul intern', 'Checked using internal FX')
                          : formatMoney(
                              expected,
                              undefined,
                              vatCheck?.calculation?.policy?.exponent ?? 2,
                            ) +
                            ' ' +
                            d.taxCurrency}
                      </strong>
                    </span>
                    {expected !== null && (
                      <span>
                        {t('Diferență', 'Difference')}:{' '}
                        <strong>
                          {formatMoney(
                            difference,
                            undefined,
                            vatCheck?.calculation?.policy?.exponent ?? 2,
                          )}{' '}
                          {d.taxCurrency}
                        </strong>
                      </span>
                    )}
                  </div>
                  {vatPending && (
                    <p role="status">
                      {t(
                        'Se verifică TVA la cursul intern…',
                        'Checking VAT using the internal FX rate…',
                      )}
                    </p>
                  )}
                  {vatCheckError && (
                    <p className="issue blocking" role="alert">
                      {vatCheckError}{' '}
                      <button
                        type="button"
                        className="secondary"
                        onClick={() => VatRetry((n) => n + 1)}
                      >
                        {t('Reîncearcă', 'Retry')}
                      </button>
                    </p>
                  )}
                  {validationCurrent &&
                    vatCheck?.issues?.map((issue: Any, index: number) => (
                      <p
                        key={index}
                        className={'issue ' + (issue.blocking ? 'blocking' : 'warning')}
                        role="alert"
                      >
                        {t(issue.ro, issue.en)}
                      </p>
                    ))}
                  {localTaxError && (
                    <p className="issue blocking" role="alert">
                      {t(
                        'Verifică defalcarea: bazele + TVA trebuie să corespundă totalului bonului; TVA 0% trebuie să fie 0. TVA la 11% și 21% se verifică față de bază (toleranță de rotunjire maximum 1 RON).',
                        'Check the breakdown: bases + VAT must match the receipt total; VAT at 0% must be 0. VAT at 11% and 21% is checked against the base (rounding tolerance up to 1 RON).',
                      )}
                    </p>
                  )}
                </div>
              )}
            </section>
            <section className="receipt-section">
              <h3>{t('Plată și alocare contabilă', 'Payment and accounting allocation')}</h3>
              <p className="small muted">
                {t(
                  'Metoda de plată și cardul sunt stabilite în antet. Centrul de cost al fiecărui bon se poate modifica separat.',
                  'Payment method and card are set in the report header. Each receipt can have a different cost centre.',
                )}
              </p>
              <div className="form-grid">
                {reportPayment ? (
                  <p className="small">
                    {t(
                      'Datele de plată sunt preluate din antetul decontului.',
                      'Payment details are inherited from the report header.',
                    )}
                  </p>
                ) : (
                  <>
                    <Field name={t('Metoda de plată', 'Payment method')}>
                      <select
                        disabled={!!reportPayment}
                        value={d.payment}
                        onChange={(e) => set('payment', e.target.value)}
                      >
                        <option value="Personal">{t('Plată personală', 'Personal payment')}</option>
                        <option value="CompanyCard">{t('Card companie', 'Company card')}</option>
                      </select>
                    </Field>
                    {d.payment === 'CompanyCard' && (
                      <Field name={t('Card utilizat', 'Company card used')}>
                        <select
                          required
                          value={d.cardId || ''}
                          onChange={(e) => set('cardId', e.target.value)}
                        >
                          <option value="">{t('Selectează', 'Select')}</option>
                          {options('cards').map((m: Any) => (
                            <option key={m.id} value={m.id}>
                              {label(m)}
                            </option>
                          ))}
                        </select>
                      </Field>
                    )}
                  </>
                )}
                <Field name={t('Centru de cost al bonului', 'Receipt cost centre')}
                  action={statusAction('costCenter')}
                  note={t('Propus din alocarea angajatului. Poți modifica doar acest bon; antetul și celelalte cheltuieli nu se schimbă.',
                          'Prefilled from the employee allocation. Changing it affects only this receipt, not the header or other expenses.')}>
                    <select
                      required
                      value={d.costCenter}
                      onChange={(e) => set('costCenter', e.target.value)}
                    >
                      <option value="">{t('Selectează', 'Select')}</option>
                      {options('costCenters').map((m: Any) => (
                        <option key={m.id} value={m.code}>
                          {label(m)}
                        </option>
                      ))}
                    </select>
                  </Field>
                {states.project !== 'NOT_APPLICABLE' && (
                  <Field
                    action={statusAction('project')}
                    name={
                      states.project === 'REQUIRED'
                        ? t('Proiect (obligatoriu)', 'Project (required)')
                        : t('Proiect (opțional)', 'Project (optional)')
                    }
                  >
                    <select
                      required={states.project === 'REQUIRED'}
                      value={d.project || ''}
                      onChange={(e) => set('project', e.target.value)}
                    >
                      <option value="">{t('Fără proiect', 'No project')}</option>
                      {options('projects').map((m: Any) => (
                        <option key={m.id} value={m.code}>
                          {label(m)}
                        </option>
                      ))}
                    </select>
                  </Field>
                )}
              </div>
            </section>
            {finance && (
              <section className="receipt-section">
                <h3>
                  {t(
                    'Mapare contabilă Finance (opțional)',
                    'Finance accounting override (optional)',
                  )}
                </h3>
                <label>
                  <input
                    type="checkbox"
                    checked={!!d.accounting}
                    onChange={(e) =>
                      set(
                        'accounting',
                        e.target.checked
                          ? { mainAccount: '', localAccount: '', subAccount: '' }
                          : null,
                      )
                    }
                  />
                  {t(
                    'Înlocuiește maparea categoriei; motivul este obligatoriu în decont',
                    'Override category mapping; report change reason is required',
                  )}
                </label>
                {d.accounting && (
                  <div className="form-grid">
                    {['mainAccount', 'localAccount', 'subAccount'].map((key) => (
                      <Field
                        key={key}
                        name={t(
                          (
                            {
                              mainAccount: 'Cont GL',
                              localAccount: 'Cont local',
                              subAccount: 'Subcont',
                            } as Any
                          )[key],
                          key,
                        )}
                      >
                        <input
                          required={key !== 'subAccount'}
                          value={d.accounting[key]}
                          onChange={(e) =>
                            set('accounting', { ...d.accounting, [key]: e.target.value })
                          }
                        />
                      </Field>
                    ))}
                  </div>
                )}
              </section>
            )}
            {expense.ocrCandidate && (
              <label className="confirmation">
                <input type="checkbox" required checked={ocrVerified}
                  onChange={(event) => OcrVerified(event.target.checked)} />
                {t(
                  'Am verificat gruparea paginilor, valorile și TVA-ul pe documentul original.',
                  'I verified the page grouping, values and VAT against the original document.',
                )}
              </label>
            )}
            <div className="modal-footer">
              {saveDraft && (
                <button
                  type="button"
                  className="secondary"
                  disabled={busy}
                  onClick={async () => {
                    DraftError('');
                    saving.current = true;
                    if (draftTimer.current) clearTimeout(draftTimer.current);
                    await draftQueue.current.catch(() => {});
                    try {
                      await saveDraft({
                        ...d,
                        amount: String(d.amount) || 0,
                        taxes: used.map(({ rateId, base, vat }) => ({
                          rateId,
                          base: String(base || '0'),
                          vat: String(vat || '0'),
                        })),
                      });
                    } catch (e) {
                      DraftError((e as Error).message);
                    } finally {
                      saving.current = false;
                    }
                  }}
                >
                  {t('Salvează ciorna pentru verificare', 'Save review draft')}
                </button>
              )}
              <button type="button" className="secondary" onClick={requestClose}>
                {t('Anulează', 'Cancel')}
              </button>
              <button
                className="primary"
                type="submit"
                onClick={() => { saveNextRequested.current = false; }}
                disabled={
                  busy ||
                  !d.country ||
                  (taxError && !finance) ||
                  supplierError ||
                  (domestic && d.hasCompanyCui === null)
                }
              >
                {busy ? t('Se salvează…', 'Saving…') : t('Salvează bonul', 'Save receipt')}
              </button>
              <button
                type="submit" className="secondary"
                onClick={() => { saveNextRequested.current = true; }}
                disabled={busy || !d.country || (taxError && !finance) || supplierError
                  || (domestic && d.hasCompanyCui === null)}
              >
                {t('Salvează și următorul', 'Save and next')}
              </button>
            </div>
          </form>
        </div>
      </section>
    </div>
  );
}
