# Faza 2 — Plan de reproiectare

**Stare:** plan de implementare; funcționalitățile descrise aici nu sunt încă livrate. Specificația detaliată în engleză se află în [PHASE-2-DESIGN.md](PHASE-2-DESIGN.md). Matricea actualizată este [PHASE-2-MATRIX.csv](PHASE-2-MATRIX.csv).

## Experții implicați

Designerul de produs, cercetătorul UX, specialiștii front-office și back-office și redactorul RO/EN definesc interfața și fluxurile. Arhitectul, inginerul OCR, responsabilul QA, specialistul în conformitate și Product Owner verifică integritatea, implementarea și criteriile de acceptare.

## Deciziile principale

Platforma rămâne un instrument intern, cu limba română implicită și engleză disponibilă permanent. Păstrăm React/TypeScript, ASP.NET Core și PostgreSQL. Nu înlocuim arhitectura funcțională și nu eliminăm câmpuri contabile pentru a simplifica ecranul.

Interfața folosește fundal gri foarte deschis, suprafețe albe, text închis și verde pentru acțiunile principale. Textul de bază are 16 px, controalele au suprafețe de interacțiune de minimum 44 × 44 px, iar focalizarea tastaturii este vizibilă. Culorile, spațierile, componentele și stările sunt definite în specificația detaliată. Tema deschisă este standardul inițial; tema întunecată rămâne opțională.

## Completarea decontului

Angajatul pornește un decont, completează scopul, adaugă bonurile și verifică rezumatul înainte de trimitere. Un decont poate avea până la 50 de bonuri. Fiecare bon poate fi modificat, duplicat sau arhivat, în limitele stării și drepturilor utilizatorului.

Editorul bonului ocupă o pagină dedicată. Pe desktop, documentul original este în stânga, iar câmpurile sunt în dreapta. Pe mobil, formularul are patru pași: **Document → Date bon → Sume și TVA → Verificare**. Progresul și starea salvării sunt vizibile.

| Situație | Comportamentul formularului |
|---|---|
| Furnizor străin | Nu solicită CUI românesc sau detaliere obligatorie de TVA internă. Păstrează descrierea, moneda, suma și câmpurile comune aplicabile. |
| Furnizor român, fără CUI-ul companiei pe bon | Continuă pe ramura simplificată, fără a inventa TVA deductibilă. |
| Furnizor român, cu CUI-ul companiei pe bon | Solicită CUI-ul furnizorului și caută în nomenclator. Afișează furnizorul identificat. |
| CUI valid, furnizor necunoscut | Solicită confirmarea corectitudinii. Permite continuarea aprobării; exportul rămâne blocat până la completarea contului ERP. |
| TVA aplicabilă | Introduce baza la 0% și baza/TVA la 11% și 21%, în funcție de data documentului și configurație. Recompune totalul și arată diferența. |
| Plată cu cardul companiei | Solicită card activ. Persoana care a efectuat cheltuiala rămâne Worker în export, indiferent de titularul cardului. |

Toleranța aritmetică de ±1 RON și toleranța pentru numărarea cotelor de TVA de ±2 RON sunt reguli distincte. Sumele mici nu sunt eliminate din totalul contabil. Regula de deductibilitate de 50% pentru cheltuielile auto rămâne aplicabilă conform cerințelor confirmate.

Încărcarea oferă butonul **Alege fișiere**, cameră și tragere în zona de încărcare. Sunt acceptate PDF, JPG, PNG și conversia HEIC existentă. La final există opțiunea pentru un singur fișier cu toate bonurile. Utilizatorul confirmă bonurile acoperite de acesta. Bonurile adăugate ulterior necesită verificarea acoperirii. Trimiterea este blocată dacă există bonuri fără documente validate.

Salvarea automată arată clar dacă datele sunt salvate, în curs de salvare sau nesalvate. Un conflict de versiune nu suprascrie modificările altui utilizator. Datele incomplete ale unei ciorne nu relaxează validările pentru trimitere sau export.

## OCR și documente

Extragerea rulează după verificarea de securitate a fișierului. Utilizatorul vede câmpurile extrase, nivelul de încredere disponibil, pagina și avertizările de consistență. Lipsa unui scor este afișată ca atare; nu se generează un procent fictiv.

Selectarea unui câmp deschide pagina și evidențiază zona corespunzătoare. Vizualizatorul oferă mărire, micșorare, încadrare, rotire și navigare între pagini. Rotirea nu modifică originalul. Corecțiile păstrează sugestia inițială, valoarea confirmată, autorul, data și versiunea extractorului. Aceste date permit evaluarea și îmbunătățirea ulterioară; nu pretindem că sistemul învață automat fără un proces implementat și testat.

Dacă OCR eșuează, utilizatorul poate completa manual un document care a trecut verificările de securitate. Un fișier respins de scanare nu poate fi folosit pentru a ocoli cerința de document justificativ.

## Fluxurile operaționale

| Rol | Flux principal | Recuperare |
|---|---|---|
| Manager | Lista de aprobări → revizie și documente → aprobare | Returnare cu motiv; o revizie modificată necesită reîncărcare și, după caz, reaprobare. |
| Financiar | Lista filtrată → verificare contabilă → rezolvarea excepțiilor → pregătire pentru export | Modificări motivate și auditate; problemele de furnizor, TVA sau card au acțiuni dedicate. |
| Administrator | Căutare → editare versionată sau previzualizare import → confirmare | Importul invalid nu se aplică parțial; înregistrările utilizate nu se șterg fără controlul referințelor. |
| Auditor | Filtre → cronologie și diferențe → export complet | Acces de citire; fără modificarea documentelor, deciziilor sau istoricului financiar. |

Financiarul primește o singură listă de lucru, cu paginare pe server, filtre salvate, sortare, coloane configurabile și acțiuni în masă controlate. Indicatorii din tabloul de bord deschid exact lista filtrată corespunzătoare. Aprobarea și înregistrarea contabilă nu sunt ascunse într-o acțiune generică în masă.

Reconcilierea păstrează lotul extrasului, fișierul și configurația importului. Sunt acceptate asocieri parțiale și relații unu-la-mai-multe sau mai-multe-la-unu. Exportul contabil are verificare blocantă, previzualizare cu cele 24 de coloane exacte și fișier nemodificabil după generare. Reexportul după înregistrare necesită redeschidere autorizată și motivată.

Nomenclatoarele oferă căutare, perioade de valabilitate, responsabil, istoric și import/export. Comasarea duplicatelor arată efectul asupra referințelor înainte de confirmare și nu rescrie exporturile istorice. Auditul prezintă modificările înainte/după într-o formă lizibilă, cu export complet, fără trunchiere ascunsă.

## Ordinea implementării

1. Corectitudinea financiară: TVA, curs valutar, dimensiuni neaplicabile și export.
2. Componentele comune: navigare, traduceri, profil, accesibilitate și stări.
3. Completarea decontului: editor ghidat, ciorne, autosalvare și documente.
4. OCR și previzualizare: încredere, coordonate, corecții și pagini multiple.
5. Operațiuni: aprobări, Financiar, carduri, rapoarte și notificări.
6. Administrare și integritate: nomenclatoare, audit, retenție și operațiuni de fundal.
7. Verificare finală: teste complete, ghiduri RO/EN, capturi reale și verificările de producție.

## Verificări și riscuri rămase

Matricea păstrează toate cele 1.579 de identificatoare și stările inițiale. Fiecărui rând i se adaugă un lot de lucru, responsabil și criteriu de verificare; repartizarea inițială necesită verificare semantică individuală. Calculul contrastului pentru opt perechi de culori trece pragurile propuse, dar nu înlocuiește verificarea interfeței implementate.

Nu s-au modificat componente de producție, endpointuri sau baze de date în această fază. Rămân necesare exemplele ERP aprobate, formatele bancare, configurația SMTP, găzduirea și dimensionarea. Continuăm cu adaptoare configurabile; nu inventăm datele de producție și nu declarăm acceptare de 100% înainte de dovezi.
