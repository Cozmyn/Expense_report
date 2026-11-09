# Ghid de utilizare / User guide

Acest ghid descrie funcțiile existente și primele corecții ale consiliului. Nu declară implementarea completă a planului premium. / This guide covers existing functionality and the first council fixes, not completion of the premium plan.

## Română

### Acces și profil

Autentifică-te cu utilizatorul și parola primite de la administrator. La prima utilizare sau după resetare, schimbă parola temporară. Selectorul RO/EN este disponibil și în acest pas.

Deschide **Profilul meu** din meniu sau prin butonul cu inițialele tale. Poți vedea datele contului și rolurile, selecta limba și schimba parola. Rolurile și datele de angajat se modifică numai prin administrator. La schimbarea parolei, confirmă aceeași parolă nouă în ambele câmpuri; celelalte sesiuni sunt închise.

### Decont și bonuri

1. Alege **Decont nou**, completează scopul și alege o singură dată plata/cardul în antet. Numele, LantID și departamentul vin din profil.
2. Adaugă câte un bon/document pentru fiecare cheltuială, până la 50. Selectează tipul cheltuielii și data documentului.
3. Precizează dacă furnizorul este din România. Pentru un furnizor român, indică dacă pe bon apare CUI-ul companiei care decontează.
4. Dacă apare CUI-ul companiei, completează CUI-ul furnizorului. Verifică denumirea găsită; dacă furnizorul lipsește, confirmă CUI-ul numai după verificarea documentului.
5. Completează descrierea, moneda, totalul și alocările. Plata/cardul se preiau din antet; folosește decont separat pentru alte date de plată.
6. Pentru ramura cu TVA, copiază baza și TVA pentru fiecare cotă aplicabilă. La 0%, introdu baza; TVA rămâne zero. Totalul recompus trebuie să corespundă bonului. Verificarea aritmetică folosește toleranța în RON, inclusiv pentru valută.
7. Atașează documentul bonului sau folosește secțiunea pentru fișierul comun. Confirmă acoperirea tuturor bonurilor; dacă adaugi bonuri ulterior, verifică din nou acoperirea.
8. Așteaptă scanarea și rezolvă erorile înainte de **Trimite la aprobare**. Un furnizor fără cont ERP poate continua prin aprobare, dar exportul contabil rămâne blocat.

Dacă verificarea TVA nu poate contacta serverul, folosește **Reîncearcă**. Nu interpreta lipsa rezultatului ca validare reușită. Datele incomplete se păstrează în ciornă conform fluxului existent; verifică mesajul de salvare înainte de a închide pagina.

### OCR

Încarcă PDF-ul sau imaginile, ori fotografiază bonul. Originalele sunt păstrate și scanate înainte de OCR. Dacă încărcarea a fost întreruptă după transfer, folosește **Finalizează încărcarea**. Urmărește progresul pe pagini.

În **Documente de verificat**, compară fiecare document cu originalul. Pentru mai multe bonuri într-un PDF, deschide **Mai multe bonuri într-un PDF?**, apoi **Delimitează un bon din PDF** și trasează o regiune pentru fiecare bon. Creează câte un grup separat și confirmă-l ca o cheltuială distinctă. Separarea/reunirea paginilor și documentele justificative rămân disponibile. Paginile neatribuite și documentele neverificate blochează trimiterea.

Dacă un câmp nu a fost recunoscut, alege **Selectează un câmp din bon**. Indică tipul informației (dată, număr, furnizor, CUI, monedă, total sau descriere), activează **Delimitează regiunea** și selectează textul cu mouse-ul (pe mobil, cu degetul). Apasă **Folosește textul selectat**, apoi **Revizuiește și creează bonul** și verifică valoarea. Nu orice selecție poate fi interpretată automat; poți completa manual.

După cel puțin două bonuri confirmate de la același furnizor, cu același aspect și selecții explicite, aplicația *poate* propune poziția unui câmp lipsă pe un bon viitor. Nu învață cote TVA sau sume fixe și nu confirmă automat câmpurile.

Alege **Arată sursa câmpului** pentru evidențiere; folosește navigarea, zoom și rotația. Scorul afișat pentru imagini reprezintă recunoașterea textului, nu validarea fiscală. Textul nativ PDF nu are scor OCR. Lipsurile și ambiguitățile necesită verificare manuală.

Deschide **Revizuiește și creează bonul**, selectează categoria (sugestiile sunt opționale), verifică descrierea și alocările din nomenclatoare. Poți salva o **ciornă pentru verificare** și reveni. Confirmă fiecare valoare pe original înainte de **Salvează bonul**. TVA 5% și 19% sunt dezactivate; nu transforma o cotă citită în alta.

**Istoric valori corectate** păstrează valoarea OCR, valoarea anterioară și cea verificată. **Istoric extrageri originale** păstrează încercările. Reîncercarea OCR nu înlocuiește bonurile verificate. Finance poate încărca/reextrage în etapa de verificare financiară, cu drepturile și motivul necesare.

### Manager

Deschide **Aprobări**, verifică revizia, bonurile și documentele. Aprobă sau returnează cu motiv. După corectarea și retrimiterea decontului, aprobarea se aplică noii revizii. Delegarea trebuie configurată în perioada corectă; accesul este verificat și pe server.

### Financiar

Deschide decontul, verifică excepțiile și completează furnizorii lipsă. Reconcilierea cardului trebuie să respecte sumele disponibile; confirmă alocările și justifică excepțiile. Modificările financiare cer motiv și sunt auditate. O creștere a sumei de rambursat poate necesita reaprobare.

Finalizează verificarea, examinează previzualizarea ERP și generează XLSX numai după rezolvarea blocajelor. Fișierul păstrează foaia **Import** și cele 24 de coloane. După importul efectiv în ERP, înregistrează referința postării. Redeschiderea necesită drepturi și referința reversării externe; platforma nu execută reversarea în ERP.

### Administrare și audit

Administratorul gestionează conturile, rolurile, alocările și delegările. Nomenclatoarele financiare au perioade de valabilitate; verifică previzualizarea importului și erorile înainte de confirmare. Nu introduce parole sau secrete în descrieri și motive.

Auditorii folosesc vizualizările autorizate și descărcările disponibile. Exportul complet, fără limita actuală, și unele operațiuni de administrare fac încă parte din lucrările deschise din matrice. Pentru probleme, păstrează identificatorul decontului și mesajul de eroare; nu trimite parole.

## English

### Access and profile

Sign in with the account supplied by your administrator. Change a temporary password when prompted; RO/EN is available on that screen. Open **My profile** from navigation or your initials. View account details and roles, select a language or change your password. Administrators control employee details and roles. New passwords must match; a successful change signs out other sessions.

### Reports and receipts

1. Create a report, enter its purpose and select payment/card once in the header. Employee name, LantID and department come from the profile.
2. Add each receipt separately, up to 50, with its category and document date.
3. Select domestic or foreign supplier. For a Romanian supplier, specify whether the reimbursing company's tax ID is printed on the receipt.
4. Where that ID is printed, enter the supplier tax ID and check the lookup. Confirm an unknown valid ID only after checking the original.
5. Enter description, currency, gross amount and applicable dimensions. Payment/card come from the header; use a separate report for different payment details.
6. For applicable VAT, copy base and paid VAT per rate. At 0%, enter the base with zero VAT. Reconcile the gross total. Arithmetic tolerance is evaluated in RON, including foreign-currency receipts.
7. Attach each receipt or confirm shared-file coverage. Newly added receipts require coverage review.
8. Wait for scanning and resolve blocking issues before submitting. A missing ERP supplier account may pass manager approval but prevents accounting export.

Use **Retry** if live VAT validation fails. A missing response is not successful validation. Check draft-save feedback before leaving.

### OCR, approval and Finance

Upload PDFs/images or photograph a receipt. Originals are retained and security-scanned before OCR. If transfer was interrupted after the file was retained, choose **Complete upload**. Track per-page progress.

In **Documents to review**, open **Multiple receipts in one PDF?** if you need help. Choose **Select another receipt from PDF**, draw one region per receipt and review each group separately before creating its expense. Split/merge and supporting-page tools are still available. Unassigned pages and unverified groups block submission. Removing a proposal preserves the original and history. Highlights, zoom, rotation and page navigation help comparison; image-recognition confidence is not a tax or amount guarantee.

If OCR misses a field, select **Select a field from the receipt**. Pick the field type, activate **Select source region**, draw around the text, then choose **Use selected text**. Review the resulting suggestion before saving. Manual entry is always available.

After at least two verified receipts with explicit source selections from the same supplier and a consistent layout, the system *may* suggest the location of missing fields next time. It never memorizes an amount or VAT rate as a fixed value, and it never confirms a field on your behalf.

Choose **Review and create receipt**. Explicitly select a category; suggestions are optional. Check itemization and controlled allocation defaults. **Save review draft** retains an audited, unverified draft. Confirm all values on the original before saving the verified receipt. Rates 5% and 19% are disabled and are never automatically remapped.

**Correction history** retains original, previous and verified values; **Original extraction history** retains provider versions. Retrying OCR preserves verified receipts and IDs. Finance retains upload/re-extraction rights in financial review, with the existing reason and state checks.

Managers review the assigned revision and evidence, then approve or return with a reason. Corrections and resubmission create a new approval context. Finance resolves vendor/VAT/card exceptions, records reasons for edits and confirms reconciliation allocations without exceeding available amounts. Increased reimbursement can require reapproval.

Complete Finance review and preflight, inspect the ERP preview, and generate the immutable XLSX. Preserve the **Import** sheet and exact 24-column schema. Record the actual external posting reference only after import. Authorized reopening records an external reversal; the application does not perform that reversal.

### Administration and remaining limits

Admins manage users, assignments and delegation. Review effective dates and import errors before confirming master-data changes. Auditors have scoped read access. Complete audit export and several premium administration capabilities remain open in the matrix. Share the report identifier and error message when requesting support, never passwords.

## Previzualizare / Preview

**RO:** În editor, originalul apare în stânga pe desktop și deasupra câmpurilor pe mobil. Pentru imagini, folosește **Mărește**, **Micșorează**, **Rotește** și **Resetează**. Derulează zona imaginii pentru detalii. Aceste acțiuni nu modifică fișierul atașat. Dacă imaginea nu se încarcă, alege **Reîncearcă** sau **Deschide originalul**. Un HEIC nou se poate previzualiza după salvare și scanare. PDF-urile încărcate și scanate au navigare pe pagini și evidențiere; un PDF local nesalvat folosește temporar browserul.

**EN:** In the editor, the original appears on the left on desktop and above the fields on mobile. For images, use **Zoom in**, **Zoom out**, **Rotate** and **Reset**. Scroll within the image for details. These actions never modify the attachment. If loading fails, choose **Retry** or **Open original**. A newly selected HEIC becomes previewable after saving and scanning. Uploaded, scanned PDFs support page navigation and source highlighting; an unsaved local PDF temporarily uses browser controls.


## Terminologie / Glossary

| Română | English | Sens / Meaning |
|---|---|---|
| Decont | Expense report | Antet și bonuri pentru un flux de aprobare / Header and expenses for one approval workflow |
| Bon / document | Receipt / document | Document justificativ individual / Individual financial evidence |
| Grup de documente | Document group | Pagini/regiuni pentru un singur document / Pages/regions belonging to one document |
| Pagină justificativă | Supporting page | Pagină păstrată fără cheltuială separată / Retained page without a separate expense |
| Ciornă pentru verificare | Review draft | Date neconfirmate, fără acces la contabilizare / Unverified data, ineligible for posting |
| Bază TVA | VAT base | Valoarea fără TVA pentru o cotă / Net taxable amount for one rate |
| TVA plătit | Paid VAT | Taxa efectiv înscrisă pe document / Tax explicitly printed on the document |
| Centru de cost | Cost centre | Alocare din nomenclator / Controlled allocation dimension |
| Valoare extrasă | Extracted value | Propunere OCR cu sursă / Source-linked OCR proposal |
| Valoare verificată | Verified value | Valoare confirmată de utilizator și validată de server / Human-confirmed, server-validated value |

## Actualizare Finance / Finance update — 05.11.2026

RO: Deschide un decont aprobat de manager și fila **Calcul și FX**. Compară baza introdusă cu baza calculată și verifică totalul pe original. Pentru valută, completează motivul și confirmă estimarea, suma bancară reconciliată sau propune un curs/o sumă RON. Aplicarea valorii Finance nu retrimite decontul managerului. Istoricul păstrează fiecare decizie. Diferențele de rotunjire de cel mult 2 RON nu cer un atașament suplimentar și nu creează bază 0%. Documentul original rămâne obligatoriu. Finance administrează versiunile monedei din aceeași filă; conturile reale de rotunjire se configurează în setările ERP. Sumele negative au avertizare și urmează fluxul obișnuit.

EN: Open a manager-approved report and **Calculations & FX**. Compare entered and computed bases and verify gross against the original. For foreign currency, enter a reason and confirm the estimate/reconciled bank amount or propose a rate/RON amount. Applying Finance's amount does not return the report to the manager. History retains every decision. Rounding up to RON 2 needs no additional attachment and never creates zero-rated base; original evidence is still mandatory. Finance versions currency settings on this screen and configures real rounding accounts in ERP settings. Negative amounts show warnings and follow the same workflow.

Detailed configuration, API routes, owner decisions, limitations and QA steps: [Finance operating guide](council/FINANCE-DECISIONS-2026-11-05.md).

## Finalizare PDF03 / PDF03 completion

### Română

- **Politici TVA:** deschide „Configurare Finance” din navigarea principală, apoi „Politici TVA și aprobare”. Aceleași controale sunt disponibile contextual în fila „Calcul și FX”. Alege compania, țara, tipul documentului, prezența CUI, categoria și perioada. „Orice tip/categorie” evită introducerea codurilor. Completează referința și motivul, apoi salvează ciorna. Alt utilizator Finance o aprobă. Politicile suprapuse/ambigue sau lipsa unei reguli aplicabile blochează verificarea; o ciornă nu schimbă calculele.
- **Tratament special:** când documentul arată CUI-ul companiei, poți indica scutit/în afara sferei/TVA neprezentat și justificarea. Este necesară o politică aprobată corespunzătoare; „necunoscut” nu înseamnă zero și nu poate fi confirmat. Detaliile TVA permit indicarea stării și paginii din grupul verificat.
- **Istoric:** „Compară versiunile calculate” arată versiunile acceptate și valorile înainte/după. Fișierele și bazele originale rămân disponibile separat de valorile contabile confirmate.
- **Comisioane bancare:** în „Carduri și tranzacții”, deschide „Principal, comisioane și adaos FX”. Selectează tranzacția/decontul și extrasul scanat (atașat unui bon sau documentelor decontului), completează principalul/comisionul/adaosul și categoria/centrul de cost. Folosește „Recompune principalul” și confirmă. Confirmă explicit și cazurile fără comisioane. Costurile devin bonuri RON distincte, cu același card și document justificativ; la FX se alocă numai principalul. Pentru corecție, anulează întâi alocările, apoi clasificarea. Istoricul și originalul se păstrează.
- **Golire reversibilă:** în „Istoric și aprobări”, deschide „Golire și restaurare”. Scrie numărul decontului și motivul. Golirea arhivează bonurile/grupurile și păstrează documentele. Poți restaura cât timp decontul este încă gol și editabil; calculele Finance vor necesita reconfirmare. Nu se modifică exporturile postate.
- **Acceptanță reală:** în „Acceptanță Finance și ERP”, bifează numai verificările efectuate efectiv și înregistrează referința. Exportul de producție cere acceptanța configurării curente. O schimbare de curs pe un bon nu retrimite decontul managerului. Configurarea politicilor/mapărilor poate necesita o nouă acceptanță Finance.

### English

Use **Finance settings → VAT policies & approval** from the main navigation, or the contextual controls under **Calculations & FX**, to scope and draft a policy; a different Finance user approves it. Drafts remain inactive. Policy gaps and equal-priority ties block verification. Special VAT treatment needs an approved matching policy and an explanation; unknown never becomes zero. **Compare calculated versions** shows accepted before/after values and source provenance.

In **Cards & transactions → Principal, fees & FX markup**, choose the transaction/report and scanned statement (attached to a receipt or report), separate and reconcile principal/fees/markup, and select charge category/cost centre. Explicitly confirm no-charge cases too. Charges become separate RON expenses; only principal is allocated for FX. Undo associated allocations before correcting classification; originals/history are retained.

In **History & approvals → Reset & restore**, confirm the report number and reason. Reset archives receipts/groups without deleting sources; restore is allowed only into an empty editable report. Finance confirmations must be repeated. Posted exports remain unchanged.

**Finance & ERP acceptance** records checks actually performed against real configuration/documents/ERP. Production export requires a current acceptance record. Receipt FX changes never request manager reapproval; configuration changes may require fresh Finance acceptance.

Full contracts and migration: [API03](council/API-03.md), [ADR03](council/ADR-03.md), [source-to-rule register](council/PDF03-SOURCE-RULE-REGISTER.md).

### Glosar suplimentar / Additional glossary

| RO | EN |
|---|---|
| Ciornă de politică | Policy draft |
| Autor / aprobator distinct | Author / independent approver |
| Bază documentată | Documented taxable base |
| Zero explicit / necunoscut | Explicit zero / unknown |
| Scutit / în afara sferei | Exempt / outside scope |
| Principal bancar | Bank principal |
| Comision / adaos valutar | Fee / FX markup |
| Golire reversibilă / restaurare | Reversible reset / restore |
| Versiune acceptată / diferență | Accepted version / difference |
| Acceptanță Finance | Finance acceptance |


**RO:** „Configurare Finance” este disponibilă fără decont deschis. Aici administrezi monedele, politicile TVA și acceptanța Finance/ERP. Ciornele neaprobate nu schimbă regulile active. Cursul estimat folosește ultima valoare validă introdusă de Finance prin oricare dintre cele două căi de configurare acceptate, după data înregistrării.

**EN:** **Finance settings** is available without opening a report. Manage currencies, tax policies and Finance/ERP acceptance here. Unapproved drafts never change active rules. The estimate uses the latest valid Finance entry across both supported rate configuration paths, ordered by recorded entry time.

[Final verification and owner QA / Verificare finală și pași pentru beneficiar](council/PDF03-COMPLETION-VERIFICATION.md).


## PDF04 — contabilizare și export / posting and export

### Română

1. **Configurare Finance → Deductibilitate auto:** implicit50%, pentru toate categoriile marcate auto, indiferent de metoda de plată. Introdu procentul, data aplicării și motivul. Se creează o versiune nouă; versiunile anterioare rămân în istoric. Pentru alt procent, completează codurile TVA contabile necesare; lipsa lor blochează exportul.
2. Verifică documentul, baza și TVA-ul tipărit în **Calcul și FX**. Aplicația compară calculele fără să înlocuiască automat baza copiată de pe bon. O diferență necesită corectare/verificare Finance; nu este ascunsă în credit.
3. Finalizează verificarea Finance, apoi deschide **ERP**. Verifică tabelul document→export și rândurile generate. **Sursă și mapare → Vezi documentul sursă** deschide originalul cu pagini, zoom și rotire. **Fișierul Import — toate cele24 de coloane** arată exact celulele exportului.
4. Finance poate confirma sau respinge previzualizarea cu motiv. O respingere rămâne în verificarea Finance; nu cere automat o nouă aprobare managerială. Modificările FX păstrează regula deja aprobată de beneficiar.
5. **Generează export** salvează fișierul original și istoricul. Reîncercarea aceleiași generări nu creează un al doilea export. După generare, previzualizarea arată datele istorice chiar dacă nomenclatoarele se schimbă ulterior.
6. **Situație contabilă:** filtrează perioada/starea, deschide decontul, rândurile salvate sau XLSX. Totalurile pe departament/furnizor/categorie numără fiecare cheltuială o singură dată; exporturile anulate/reversate sunt excluse din aceste totaluri.
7. Numai Finance/contabilitate descarcă Excelurile contabile. Auditorii pot vedea originalele și PDF-urile în limita accesului acordat. Exportul nu adaugă un calcul TVA al ERP și nu schimbă sumele pentru o egalitate artificială debit/credit. Dacă Excel nu poate păstra exact o sumă numerică, exportul se oprește cu explicație.

### English

In **Finance settings → Vehicle deductibility**, default50 applies to all vehicle mappings regardless of payment. Save a dated percentage and reason; history is preserved. Non50 requires corresponding accounting VAT codes before export.

Check printed bases/paid VAT in **Calculations & FX**, then complete Finance review. **ERP** shows document→export reconciliation and traceable lines. **Source & mapping → View source document** opens original evidence. Expand **Import file — all24 columns** to inspect the exact serialized cells.

Finance can accept/reject a fingerprinted preview with a reason. Export stores an immutable file; equivalent retries reuse the batch. Saved preview/history never adopts later master changes. **Accounting overview** filters batches and shows once-per-expense aggregates excluding void/reversed files. Finance alone downloads accounting Excel; authorised Auditors retain original/PDF access. No additional ERP VAT model or silent amount adjustment is introduced.

[Owner decisions](council/PDF04-OWNER-DECISIONS.md) · [API/migration](council/API-04.md) · [Architecture](council/ADR-04.md).


[PDF04: verificare finală și lista QA / final verification and QA checklist](council/PDF04-VERIFICATION.md).
