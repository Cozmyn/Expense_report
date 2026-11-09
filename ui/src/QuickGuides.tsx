type Translator = (ro: string, en: string) => string;

export function ReportHelp({ t }: { t: Translator }) {
  return (
    <details className="quick-help">
      <summary>{t('Ghid scurt: cum trimit un decont?', 'Quick guide: submitting a report')}</summary>
      <ol>
        <li>{t('Creează decontul și alege metoda de plată.', 'Create a report and choose how you paid.')}</li>
        <li>
          {t(
            'Adaugă bonurile: prin fotografie, PDF sau manual.',
            'Add receipts using photos, PDFs or manual entry.',
          )}
        </li>
        <li>
          {t(
            'Verifică datele, corectează problemele și trimite la aprobare.',
            'Review the details, fix any issues and submit for approval.',
          )}
        </li>
      </ol>
    </details>
  );
}

export function OcrHelp({ t }: { t: Translator }) {
  return (
    <details className="quick-help">
      <summary>{t('Cum citește aplicația un bon?', 'How does receipt scanning work?')}</summary>
      <ol>
        <li>
          {t(
            'Fotografiază bonul întreg, clar și fără reflexii sau încarcă un PDF.',
            'Photograph the whole receipt clearly, without glare, or upload a PDF.',
          )}
        </li>
        <li>
          {t(
            'Aplicația citește textul și caută data, furnizorul, CUI-ul, totalul, moneda și TVA-ul afișat.',
            'The app reads the text and looks for the date, supplier, VAT ID, total, currency and printed VAT.',
          )}
        </li>
        <li>
          {t(
            'Compară sugestiile cu originalul, corectează ce lipsește și confirmă fiecare bon.',
            'Compare suggestions with the original, correct missing details and confirm each receipt.',
          )}
        </li>
      </ol>
      <p className="small muted">
        {t(
          'OCR poate greși sau poate rata câmpuri. Nu completează sigur informațiile lipsă și nu trimite singur decontul.',
          'OCR may misread or miss fields. It cannot reliably fill in missing details and never submits your report.',
        )}
      </p>
    </details>
  );
}
