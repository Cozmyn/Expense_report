# PDF 02 verification and owner QA

## Automated evidence

- Backend: core business/security/ERP suites plus OcrSafetyTests, OcrReviewTests, DocumentGeometryTests, DocumentSegmentationTests, DocumentSpatialTests, DocumentAssistanceTests, DocumentIntakeTests, DocumentEventTests, OcrHealthTests and OcrCorpusTests. Run on both SQLite and PostgreSQL.
- Parser corpus: tests/Fixtures/ocr-corpus.json, explicitly synthetic and not business-approved. OCR_QUALITY_OUTPUT writes a JSON artifact with boundary recall, invoice/gross/VAT precision and recall, source-page representation and unassigned-page rate. No recognition score or workload count is presented as real-document accuracy.
- Real pipeline: Playwright creates vector PDFs and raster/rotated/low-contrast scans, uploads through real auth + ClamAV + Tesseract/Poppler, and checks field values and geometry on desktop/mobile.
- Recorded demo: ocr-demo.spec.ts uses an eleven-page synthetic batch: the required ten-page/nine-group layout plus an explicit cover page. It verifies grouping, three-page invoice split/merge, wrongly merged receipts split again, cover disposition, VAT 21→2.10/base/gross correction, shared draft recovery, nine verified expense IDs and one retry without new IDs. Video, screenshots and measured elapsed time are browser-results artifacts.
- Security: upload length/signature/hash/token/scope checks; stale version rollback; append-only SQL/EF evidence and outbox; invalid preview grants; no storage-key JSON leak; duplicate source-region prevention; malicious printed instruction fixture; finite job retries; read-only non-root constrained container launch.

## Checklist for the owner / Verificare de către beneficiar

1. RO: Alege o dată plata/cardul în antet. Verifică numele, LantID și departamentul preluate din profil. EN: Choose payment/card once in the header; check captured employee identity.
2. RO: Încarcă un PDF cu mai multe bonuri. Așteaptă scanarea; urmărește paginile și starea. EN: Upload a mixed PDF; follow security scanning and per-page progress.
3. RO: Compară grupurile cu originalul. Separă/unește, ajustează regiunile și justifică o pagină fără cheltuieli. EN: Compare grouping with the original; split/merge/crop and justify a supporting page.
4. RO: Deschide un bon; verifică sursa câmpului și toate valorile. Categoria sugerată rămâne o alegere, nu o aprobare. EN: Review field sources and all values; explicitly select a suggested category.
5. RO: Pentru CUI companie, verifică furnizorul, numărul și baza/TVA 0/11/21. Nu sunt disponibile 5/19. EN: For company-CUI documents check supplier, number and VAT bases/amounts; 5/19 remain disabled.
6. RO: Salvează ciorna, redeschide-o, apoi verifică bonul. Istoricul trebuie să păstreze valoarea OCR și corecția. EN: Save/reopen a draft, verify it, and inspect raw and corrected values.
7. RO: Încearcă trimiterea cu un grup neverificat sau o pagină neatribuită; trebuie blocată. EN: Attempt submission with an unverified group/unassigned page; it must be blocked.
8. RO: Reîncearcă OCR și confirmă că UUID-urile/sumele verificate nu se schimbă și nu apar bonuri duplicate. EN: Retry OCR; verified IDs/amounts must remain unchanged.
9. RO: În Finance, verifică accesul la încărcare/corecție și exportul detaliat TVA/ERP după rezolvarea blocajelor. EN: Check Finance intake/corrections and VAT/ERP output after preflight.
10. RO: Cu alt angajat, încearcă adresa originalului și a paginii; accesul trebuie refuzat. EN: Another employee must be denied original/page access.

## External acceptance still required

Finance must approve representative real-document labels, quality thresholds and Accounting/ERP golden outputs. Operations/security must validate target cloud encryption/IAM/region/retention, restore and load/security acceptance. These cannot be inferred from synthetic regression percentages or signed by an automated coding agent. No requirement is dropped; the matrix tracks these as Partial external acceptance gates.
