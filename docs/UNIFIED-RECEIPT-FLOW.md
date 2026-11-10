# Decont simplificat — unified receipt intake (RO / EN)

**Status:** Implementation on `feature/simplified-ocr-expense-flow`; merge only after CI, user acceptance and Finance checks. This document describes intended behaviour, not certification or guaranteed OCR accuracy.

## RO — fluxul angajatului

### 1. Datele decontului — o singură dată
- Deschide **Decont nou** și salvează **scopul activității**.
- **Centrul de cost implicit** este atribuit angajatului în **Admin → Utilizatori și acces**, în baza de date; pentru utilizatorii istorici neconfigurați se păstrează temporar regulile de sugestie existente. Centrul de cost este precompletat în **antet** și poate fi modificat independent, fără să rescrie bonurile existente.
- **Fiecare bon/cheltuială** are propriul câmp **Centru de cost al bonului**, precompletat cu centrul de cost al angajatului din DB. Utilizatorul îl poate schimba pentru acel bon; schimbarea nu afectează antetul sau alte bonuri.
- **Managerul de aprobare** nu provine din managerul organizațional al angajatului: administratorul atribuie fiecare centru de cost unui **manager responsabil** în **Admin → Alocări și responsabili pe centre de cost**. Însumăm în RON cheltuielile active pentru fiecare centru și alegem proprietarul centrului cu cel mai mare total. Valoarea RON este cea confirmată de Finance, dacă există, altfel estimarea înregistrată. Dacă există egalitate la maximum cu manageri diferiți, decontul nu se trimite până la clarificare. Dacă lipsește un responsabil activ pentru oricare centru folosit, trimiterea este blocată.
- **Metoda de plată** (personală sau card companie), **cardul** și **scopul decontului** se stabilesc la nivel de decont. Pentru modalități de plată diferite sunt necesare deconturi separate.
- Salvează datele generale înainte de primul bon.

### 2. Un singur loc de încărcare
- În **Cheltuieli → Încarcă documentele și extrage datele**, selectează explicit **Un singur bon / o factură** sau **Mai multe bonuri**.
- Încarcă PDF/JPG/PNG/HEIC din **Adaugă PDF sau fotografie**. Sistemul validează fișierul, îl scanează cu ClamAV, citește textul și creează **propuneri** de bon. Un PDF de factură cu 3 pagini poate fi tot **un singur bon**.
- Când OCR-ul este gata, se deschide automat **formularul primei propuneri**, cu originalul în previzualizare. **Nu se postează nicio cheltuială fără verificarea și salvarea de către utilizator.**
- Pentru fișiere cu mai multe bonuri, verifică fiecare propunere pe rând, folosind selectorul **Bon 1 / Bon 2 / ...**. Regiunea asociată apare evidențiată.
- Dacă lipsește un bon, alege **Nu este detectat un bon? Selectează-l din PDF** și trasează regiunea, fără meniuri de configurare ascunse.
- Dacă textul este citit, dar parserul nu găsește câmpuri, ai totuși un formular de verificare pentru **Un singur bon**, cu introducere manuală.

### 3. Ce știe OCR și ce nu știe
- Sunt **propuneri**: data, furnizorul, CUI furnizor, numărul documentului, moneda, totalul, descrierea dacă este tipărită explicit și baza/TVA când le identifică.
- Valorile recunoscute și folosite în formular sunt evidențiate pe original. Pentru datele lipsă sau greșite, apasă **Selectează pe bon** lângă câmp, apoi **Delimitează regiunea → Folosește selecția**; sau scrie valoarea direct în formular.
- „Textul citit din document” este transcriere brută, **nu** dovada că toate câmpurile au fost identificate.
- **Tipul cheltuielii** rămâne alegerea utilizatorului; sugestiile OCR nu postează categorii automat.

### 4. CUI-ul companiei, țara și TVA
- Pentru document românesc, se caută **RO35898037** sau **RO 35898037**; un CUI al companiei configurat în politicile Finance are prioritate față de acest fallback.
- **România + CUI companie tipărit**: completezi informațiile furnizorului și, pentru cotele aplicabile, **baza fără TVA** și **TVA-ul efectiv**. Parserul le propune dacă sunt recognoscibile; absența valorii nu echivalează cu zero. Sistemul verifică aritmetic:
  `total recomputat = Σ (bază fără TVA pe cotă + TVA efectiv pe cotă)`.
  `diferență = total recomputat - total tipărit`.
  La discrepanțe, utilizatorul verifică originalul; regulile Finance verifică TVA, cursul și toleranțele. O explicație liberă nu înlocuiește validarea.
- **România fără CUI companie tipărit**: introduci totalul bonului, moneda și descrierea; **nu** se cer bazele/TVA, CUI furnizor sau numărul de bon obligatoriu pentru ramura cu CUI. Confirmi dacă nu a fost citit corect CUI-ul.
- **Furnizor din afara României**: introduci suma în moneda originală și descrierea; nu apare tabelul de TVA românesc. Țara se poate **sugera** numai dacă documentul arată indicii explicite (nume de țară/locuri cunoscute); moneda străină, singură, nu stabilește țara.
- **Valoarea în RON**: suma originală este păstrată; dacă moneda este RON nu există conversie. Pentru valută, estimarea folosește cursul intern Finance disponibil la introducere. Finance poate confirma ulterior suma finală conform politicilor. Suma originală, estimarea și suma confirmată nu se confundă.

### 5. Finalizare
- Verifică formularul lângă original, corectează datele, confirmă gruparea și TVA, apoi apasă **Salvează bonul**.
- Pentru mai multe bonuri, repetă până la verificarea tuturor grupurilor și paginilor.
- Revizuiește lista de cheltuieli, mesajele de validare și apasă **Trimite la aprobare**.
- Originalele și corecțiile rămân în istoricul de audit. Proprietarul centrului dominant aprobă; Finance verifică și decide contabilizarea. La modificări Finance care schimbă responsabilul, se solicită o nouă aprobare; la schimbarea proprietarului de către Admin, aprobările încă în așteptare sunt rerutate cu o nouă revizie.

### Situații de eroare și siguranță
- OCR neclar, pagini lipsă, documente dublate, CUI neclar, TVA lipsă și valute neidentificate se rezolvă prin **confirmare umană**, nu prin valori inventate.
- Selectarea manuală a unei regiuni este o **sugestie cu sursă**, nu o aprobare financiară.
- Fișierele sunt validate pe server, păstrate privat și scanate; încărcarea nu permite evitarea verificării umane.
- **Nu** elimina baza de date sau volumele Docker pentru a aplica versiuni noi.

---

## EN — employee flow

### 1. Report header once
The employee's **default cost centre** is configured in the database under **Admin → Users & access**. It prefills the report header, but changing the header never bulk-updates any existing expense. **Each new receipt has its own independently editable cost centre**, initially populated from the employee's database allocation (with legacy policy defaults used only for older employee records lacking an assignment). The **payment method and company card**, if applicable, belong to the report, not to each receipt.

An administrator assigns a **responsible Manager owner** to each cost centre in **Admin → Assignments and cost-centre owners**. The approval manager is **not** taken from the employee's organisational manager. Instead, add up all active expenses per cost centre in RON (Finance-confirmed value when present, otherwise the recorded estimate), and route the report to the owner of the centre with the **highest combined total**. Equal highest totals owned by different managers are an error that must be resolved. A missing/inactive owner for any used centre blocks submission. A changed dominant owner after Finance editing triggers renewed approval; changing ownership reroutes in-flight manager reviews with an audit snapshot.

### 2. One upload location
Open **Expenses → Upload documents and extract details**, choose **One receipt / one invoice** or **Multiple receipts**, and upload a PDF or photo in **Add a PDF or photo**. Following authenticated upload, malware scanning and OCR, the first **draft review** opens automatically beside the original. No expense is posted until a person reviews and saves it. For multiple receipts, use the **Receipt 1 / Receipt 2 / ...** selector; the active receipt is highlighted on the document. Select any missed source region in the same screen.

### 3. Field-level verification
OCR may suggest **date, supplier, supplier VAT ID, document number, currency, gross amount, description and printed VAT/base**. These are suggestions, never confirmations. Recognised form values are highlighted in the original. Use **Select on receipt → Select source region → Use selected text** for a missing or incorrect field, or type directly. Raw extracted text does not guarantee structured field recognition. Expense category is always confirmed by the employee.

### 4. Calculation branches
- **Romanian supplier, company VAT ID printed:** detect `RO35898037` or `RO 35898037` (effective company configuration takes precedence). Review supplier details and taxable base plus actual paid VAT **for every applicable rate**. The reconstructed total is the **sum of bases plus VAT**, compared with the printed gross. Never guess missing VAT or treat unknown as zero.
- **Romanian supplier, company VAT ID not printed:** record gross amount, currency and description only; no domestic VAT breakdown required.
- **Foreign supplier:** record gross amount, original currency and description; no Romanian VAT breakdown. Explicit country/location evidence may suggest the supplier country, but it remains editable.
- **FX:** original money stays in its original currency. Estimated RON uses an applicable Finance rate; any final FX decision remains in Finance controls and its audit trail.

### 5. Review and submit
Verify every proposed receipt and source page, save each receipt, fix checklist issues and submit for manager approval. Immutable original evidence, audit, OCR corrections and Finance checks remain active.

## Design, security and accessibility principles

- **Ask only for necessary information, once**; use conditional fields, plain labels, contextual hints and actionable errors; no hidden advanced menus.
- **WCAG 2.2**: labelled controls, keyboard focus, status updates, readable contrast and not relying on colour alone.
- **OWASP File Upload Cheat Sheet**: allowlisted file formats, server-side signature and size validation, authorised file access, quarantine/AV scanning and private originals.
- Accounting checks and immutable evidence remain server-side. **The interface cannot bypass validations.**
- Human-reviewed accuracy and finance/ERP acceptance **must be established with real anonymised receipt samples**, not synthetic tests alone.

References: https://www.w3.org/TR/WCAG22/ , https://design-system.service.gov.uk/patterns/question-pages/ , https://cheatsheetseries.owasp.org/cheatsheets/File_Upload_Cheat_Sheet.html .

## Acceptance checklist — sign-off needed before merge/release

- [ ] Single receipt / multi-page PDF opens **one** editable review draft; missing OCR fields do not hide the form.
- [ ] Multi-receipt PDF allows reviewing one source region at a time; missing receipt can be selected manually.
- [ ] Every prefilling suggestion can be validated against the original; highlighted source stays on the correct page.
- [ ] Field selection or manual typing works when OCR misses a value; saved corrections survive reload.
- [ ] Employee default cost centre is saved in the user database record and can be managed by Admin; report header is prefilled but editable.
- [ ] Every receipt has its own cost centre prefilled from that employee's database allocation; changing header or another receipt does not overwrite it.
- [ ] Admin can assign an active Manager owner for each cost centre and every change is audited.
- [ ] Routing sums receipt RON totals by cost centre and selects the largest **combined** amount, not the single largest receipt or report header value.
- [ ] Missing owner, inactive owner, self-approval and equal highest totals with different managers are all blocking.
- [ ] Manager gets recalculated on submission and expense edits; pending reviews and Finance reapprovals go to the correct new owner, with immutable approval evidence.
- [ ] Both `RO35898037` and `RO 35898037` trigger domestic company-CUI branch.
- [ ] Domestic no-CUI/foreign branches do not show misleading domestic VAT inputs.
- [ ] Taxable bases + paid VAT reconcile to gross and block invalid totals under Finance policy.
- [ ] All pages/groups covered, security scan clean, no unverified expense auto-submitted.
- [ ] RO and EN desktop/mobile/keyboard/screen-reader review; unit, integration, browser and migration tests green.
- [ ] Real company/Finance documents and ERP export reviewed by authorised business owners.
