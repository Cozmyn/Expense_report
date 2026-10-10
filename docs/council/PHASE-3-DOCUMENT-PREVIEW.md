# Previzualizare documente / Document preview

Experts: Product Designer, Front Office, OCR Engineer, QA.

Originalul apare în stânga formularului pe desktop și deasupra pe mobil. Imaginile au mărire 50–300%, rotire, resetare, stare de încărcare, eroare și reîncercare. Originalul păstrat nu este modificat. Linkul deschide originalul separat. PDF-ul folosește încă vizualizatorul browserului; navigarea PDF unificată și evidențierea câmpurilor OCR rămân deschise.

Desktop: original left, fields right. Mobile: original above fields. Image zoom 50–300%, rotate/reset, loading/error/retry and separate original link. These transformations do not modify evidence. Native PDF controls remain browser-dependent; this does not close the PDF navigation or OCR geometry requirements.

Files: DocumentViewer.tsx, ReceiptModal.tsx, style.css, platform.spec.ts. No endpoint, schema or migration changes. Existing authorization, malware scanning and original integrity handling remain in force.

Validation: TypeScript and production build passed. Desktop/mobile browser assertions added for zoom, rotation and reset; CI pending for this batch. PREM-016 is Partial, not Done. Full Phase 1/2 acceptance and all remaining batches are still open.
