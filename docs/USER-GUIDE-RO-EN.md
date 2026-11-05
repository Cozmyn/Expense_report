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

În **Grupuri și acoperirea paginilor**, compară fiecare document cu originalul. Poți separa/uni pagini, delimita două bonuri pe aceeași pagină și marca pagini justificative cu motiv. Paginile neatribuite și documentele neverificate blochează trimiterea. Înlăturarea propunerii păstrează originalul și istoricul.

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

Review groups beside the original. Split/merge pages, crop independent receipts and mark supporting pages with a reason. Unassigned pages and unverified groups block submission. Removing a proposal keeps the original and history. Field-source highlights, zoom, rotation and page navigation help comparison; image recognition confidence is not fiscal validation and native PDF text has no OCR score.

Choose **Review and create receipt**. Explicitly select a category; suggestions are optional. Check itemization and controlled allocation defaults. **Save review draft** retains an audited, unverified draft. Confirm all values on the original before saving the verified receipt. Rates 5% and 19% are disabled and are never automatically remapped.

**Correction history** retains original, previous and verified values; **Original extraction history** retains provider versions. Retrying OCR preserves verified receipts and IDs. Finance retains upload/re-extraction rights in financial review, with the existing reason and state checks.

Managers review the assigned revision and evidence, then approve or return with a reason. Corrections and resubmission create a new approval context. Finance resolves vendor/VAT/card exceptions, records reasons for edits and confirms reconciliation allocations without exceeding available amounts. Increased reimbursement can require reapproval.

Complete Finance review and preflight, inspect the ERP preview, and generate the immutable XLSX. Preserve the **Import** sheet and exact 24-column schema. Record the actual external posting reference only after import. Authorized reopening records an external reversal; the application does not perform that reversal.

### Administration and remaining limits

Admins manage users, assignments and delegation. Review effective dates and import errors before confirming master-data changes. Auditors have scoped read access. Complete audit export and several premium administration capabilities remain open in the matrix. Share the report identifier and error message when requesting support, never passwords.

## Previzualizare / Preview

**RO:** În editor, originalul apare în stânga pe desktop și deasupra câmpurilor pe mobil. Pentru imagini, folosește **Mărește**, **Micșorează**, **Rotește** și **Resetează**. Derulează zona imaginii pentru detalii. Aceste acțiuni nu modifică fișierul atașat. Dacă imaginea nu se încarcă, alege **Reîncearcă** sau **Deschide originalul**. Un HEIC nou se poate previzualiza după salvare și scanare. PDF-urile încărcate și scanate au navigare pe pagini și evidențiere; un PDF local nesalvat folosește temporar browserul.

**EN:** In the editor, the original appears on the left on desktop and above the fields on mobile. For images, use **Zoom in**, **Zoom out**, **Rotate** and **Reset**. Scroll within the image for details. These actions never modify the attachment. If loading fails, choose **Retry** or **Open original**. A newly selected HEIC becomes previewable after saving and scanning. Uploaded, scanned PDFs support page navigation and source highlighting; an unsaved local PDF temporarily uses browser controls.

