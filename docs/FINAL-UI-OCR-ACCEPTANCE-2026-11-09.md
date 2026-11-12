# Etapa 5/5 — acceptanța UI și OCR / UI and OCR release acceptance

**Data:** 9 noiembrie 2026  
**Domeniu:** Expense_report — angajat, Manager, Finance, OCR multi-bon, selecție manuală și sugestii pe furnizor  
**Stare:** cod disponibil pentru testare și lansare controlată **după** îndeplinirea verificărilor de mediu și business. **NU echivalează cu aprobare de producție.**

## 1. Ce este implementat / Shipped in source

| Zonă | Comportament |
| --- | --- |
| Angajat / Employee | Dashboard cu acțiune de continuare, listă adaptivă de deconturi, căutare și stare, îndrumare scurtă |
| Bon / Receipt | Original lângă date pe desktop, câmpuri obligatorii păstrate, avertizare la ieșire cu modificări nesalvate |
| OCR | Serviciu PaddleOCR local RO/EN, text PDF nativ păstrat, separare manuală/automată, trasare regiuni și sugestii pe furnizor; niciun import fără confirmare umană |
| Manager | Aprobări prioritizate după dreptul real furnizat de server; fără schimbare de rol |
| Finance | Rezumat excepții / deconturi pregătite pentru ERP, filtre păstrate și clare în RO/EN |
| Accesibilitate | Focus în editor, etichete pentru acțiuni, controale accesibile cu tastatura, mesaj de sesiune expirată în ambele limbi |

**Nu s-au schimbat:** regulile TVA/FX, rotunjirile, permisiunile, tranzițiile de stare, exportul ERP de 24 de coloane, istoricul documentelor și schema bazei de date. Niciun bon sau fișier istoric nu a fost eliminat.

## 2. Ce poate demonstra CI / What automated CI proves

- [ ] Pe commitul final din `main`: teste frontend și compilare TypeScript
- [ ] Pe commitul final: backend pe SQLite **și** PostgreSQL
- [ ] Pe commitul final: Playwright desktop (Chromium), mobil, documente, bonuri, manager, Finance și ERP
- [ ] Pe commitul final: container web pornește și API respinge accesul anonim
- [ ] Pe commitul final: `paddle-live-smoke.yml` descarcă/încarcă modelul real și recunoaște un bon **sintetic**
- [ ] `production-preflight.yml`: respinge setări lipsă, OCR dezactivat, URL public sau localhost; nu imprimă secrete
- [ ] Diff final: niciun fișier șters sau redenumit; toate PR-urile consolidate au review

**Important:** testele automate sunt pe documente sintetice. Nu reprezintă măsurarea calității OCR în condiții reale.

## 3. Scenarii UAT obligatorii cu utilizatori reali (RO și EN)

Probați pe desktop și mobil și documentați pentru fiecare: rezultatul, durata, eroarea și captura fără date personale.

1. Angajat nou: autentificare, schimbare parolă, creare decont, bon manual, atașare, verificare, trimitere și urmărire.
2. OCR: fotografie clară, bon șifonat/termic, reflexii, lumină redusă, rotire 90°, PDF cu text selectabil.
3. PDF cu **2 bonuri pe aceeași pagină**, PDF cu mai multe bonuri pe pagini diferite și factură care ocupă mai multe pagini: exact o cheltuială per bon confirmat; fără pagini pierdute.
4. Lipsă total/CUI/datǎ: selectare cu mouse-ul, asociere la câmp, rezultat corectabil, editare manuală și istoric.
5. Furnizor: minimum două bonuri reale confirmate cu selecții din aceeași machetă, apoi bon nou similar. Confirmați că sugestia apare **doar** unde parserul nu avea valoare și că nu se autovalidează.
6. Alt furnizor ori machetă schimbată: nu trebuie reutilizată greșit o sumă, o cotă TVA, un CUI sau o dată.
7. Bon cu TVA 0%/11%/21%, mai multe cote, EUR și plăți personale/card; comparație cu valorile Finance aprobate.
8. Închidere cu `Escape`, `Anulează`, reîncărcare, eșec upload și expirare sesiune: niciun ecran nu trebuie să sugereze fals că datele s-au salvat.
9. Manager/Finance: vizibilitate pe rol, returnare/confirmare, corecții și reaprobare fără acțiuni neautorizate.
10. Keyboard-only (Tab/Shift+Tab/Enter/Escape), VoiceOver/NVDA, contrast, text la 200% zoom, mobil 390px și texte fără chei tehnice în RO/EN.

## 4. Evaluarea preciziei OCR / Real-world OCR benchmark

Creați **minimum 30–50 documente reprezentative aprobate**, cu date de referință verificate de Finance. NU le încărcați în CI public și NU le trimiteți la antrenare externă fără un review de confidențialitate.

Măsurați pentru fiecare furnizor / machetă:
- **Segmentare:** numărul corect de bonuri, paginile și regiunile atribuite; rata de combinări și separări false.
- **Câmpuri:** furnizor, CUI, data, număr, moneda, total și sumele TVA/cote; exactitate pe fiecare și procentul de documente care necesită corecție.
- **Învățare:** rata sugestiilor utile după două confirmări; falsuri între furnizori sau machete; întotdeauna verificare umană.
- **Operare:** timpul de import și de corecție, pornire la rece, consum maxim CPU/RAM, volum de stocare și comportament la indisponibilitatea OCR/antivirusului.

Stabiliți **pragurile de acceptare împreună cu Finance**, nu în cod fără acord. **Orice valoare monetară greșită confirmată sau postată fără verificare = NO-GO.**

## 5. Condiții de producție neînchise de acest PR / Mandatory external gates

- [ ] Ingress HTTPS/TLS, reverse proxy cu listă explicită de proxy-uri de încredere, ratelimit validat pe IP real; pen-test și IDOR.
- [ ] PostgreSQL pe TLS, backup/PITR și restore testat; plan de upgrade al schemei și rollback pentru date reale; **nu** rulați `--init` pe date de producție fără backup și aprobare.
- [ ] S3 privat, ClamAV, secrete și Data Protection keys protejate, proces de rotație și monitorizare.
- [ ] Endpoint PaddleOCR **privat**, modele/versiuni preîncărcate, fără port public, capacitate măsurată și alerte pentru eșecuri.
- [ ] Restore pe mediu izolat cu documente, chei și DB; test la restart și schimbare de versiune; proprietar operațional desemnat.
- [ ] Finance aprobă fișiere ERP etalon și corectitudinea TVA/FX; proprietarul aplicației aprobă matricea de roluri și utilizabilitatea în RO/EN.
- [ ] Responsabili desemnați, incident/rollback, RPO/RTO și aprobare explicită a lansării.

## 6. Decizie finală / Go-no-go

**GO pentru review și pilot/UAT în mediu controlat** după ce CI este verde.  
**NO-GO pentru producție live** până când probele reale, infrastructura, backup/rollback și aprobările responsabile sunt completate și semnate.

Dacă un test eșuează, păstrați imaginile/aplicația anterioară și readuceți serviciul din release-ul aprobat, pe baza unui plan de rollback testat. Nicio ștergere a bazei sau a documentelor; trecerea de la un motor OCR la altul nu autorizează modificări ale datelor istorice.

**Referințe tehnice:** `ocr/README.md`, `docs/DEPLOYMENT.md`, `docs/PRODUCTION-SECURITY-READINESS.md`, `docs/USER-GUIDE-RO-EN.md`.
