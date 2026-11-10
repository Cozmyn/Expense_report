# Ghid de import / Import templates (RO + EN)

> **Status:** PR #15, în verificare. Exemplele folosesc utilizatori fictivi `example.invalid`. Înlocuiește-i cu datele aprobate ale companiei. **Nu importa date personale reale într-un mediu de test.**

## RO — Ordinea recomandată

1. **Centre de cost:** Admin/Finance → Nomenclatoare → selectează *Centre de cost* → **Exemplu CSV pentru nomenclatorul ales**. Coloane: `code,nameRo,nameEn,erpCode,from,to,active`. Trebuie să existe coduri unice, denumiri, cod ERP și data de început `YYYY-MM-DD`.
2. **Manageri:** Admin → Utilizatori → Import angajați → **Exemplu manageri CSV**. Coloane: `username,name,email,worker,managerUsername,active,costCenter,role,approvalMode,fixedApproverUsername`. Pentru manager folosește `role=Manager`; `managerUsername` poate rămâne gol. Managerul trebuie să existe și să fie activ înainte de a deveni responsabil al unui centru.
3. **Responsabili centre de cost:** Admin → Alocări și responsabili → **Descarcă exemplul CSV cu antet**. Coloane: `costCenterCode,managerUsername`; fiecare rând mapează codul unui centru activ la username-ul unui manager activ. Importul are previzualizare, validare integrală, motiv și audit; înlocuiește responsabilul existent pentru acel centru.
4. **Angajați:** Admin → Utilizatori → **Exemplu angajați CSV**. Aceleași antete ca la manageri. `role=Employee`, `costCenter` este centrul implicit folosit în antet și pe fiecare bon nou. `approvalMode=CostCenter` folosește proprietarul centrului cu suma cea mai mare din decont; `approvalMode=Fixed` cere `fixedApproverUsername` al unui manager activ și distinct.
5. **Carduri companie:** Admin/Finance → Nomenclatoare → *Carduri companie* → exemplu CSV. Coloane: `code,nameRo,nameEn,lastFour,issuer,currency,ownerUsername,from,to,active`. `ownerUsername` trebuie să fie utilizator deja importat. Stochează **numai ultimele patru cifre**, nu PAN-ul complet, expirarea sau CVV.
6. **Alte nomenclatoare:** din selectorul tipului de nomenclator se descarcă șablonul exact. Pentru tipuri fără câmpuri specializate, folosește `code,nameRo,nameEn,from,to,active,json`. Câmpul `json` este un obiect JSON cu configurările acceptate și validate pe server.
7. **Extrase bancare:** Finance → Carduri și tranzacții → exemplu extras CSV. Coloane: `reference,date,merchant,amount,currency,settledRon`; data ISO, punct decimal, semn corect al tranzacției. Importă numai după ce ai configurat cardul și moneda.

## Fișiere exemplu din GitHub / Repository examples

| Tip / Type | Fișier / File | Cum se folosește / Usage |
| --- | --- | --- |
| Angajați / Employees | [employees.csv](../examples/imports/employees.csv) | Creare conturi și alocări implicite, excepții de aprobare |
| Manageri / Managers | [managers.csv](../examples/imports/managers.csv) | Conturi cu rol Manager |
| Centre de cost / Cost centres | [cost-centres.csv](../examples/imports/cost-centres.csv) | Nomenclator activ + ERP |
| Responsabili / Owners | [cost-centre-owners.csv](../examples/imports/cost-centre-owners.csv) | Rutare manager după centru |
| Carduri / Cards | [company-cards.csv](../examples/imports/company-cards.csv) | Ultimele 4 cifre și titular |
| Bancă / Bank statement | [bank-statement.csv](../examples/imports/bank-statement.csv) | Reconciliere Finance |
| Generic | [generic-master.csv](../examples/imports/generic-master.csv) | Alte nomenclatoare cu JSON |

Toate exemplele sunt **descărcabile din meniul lor** în format CSV UTF-8. Importatoarele acceptă CSV și XLSX unde este indicat, folosind un singur sheet și numai valori, fără formule.

## EN — Required behaviour

**Import order:** cost centres → managers → cost-centre owners → employees → company cards → bank statements and other master data.

Employee imports support the columns `username,name,email,worker,managerUsername,active,costCenter,role,approvalMode,fixedApproverUsername`. Only `Employee` and `Manager` can be imported through this workflow; Admin/Finance permissions are never granted by a CSV role value. A named exception requires `approvalMode=Fixed` and an existing active Manager username; default approval uses `CostCenter`.

Cost-centre ownership imports require `costCenterCode,managerUsername`, both referencing existing, active records. The server validates all rows before transactional confirmation and records changes in the audit log. Pending manager reviews are routed to the proper owner when the mapping changes.

Cost centre master imports require `code,nameRo,nameEn,erpCode,from,to,active`. Company card imports require `code,nameRo,nameEn,lastFour,issuer,currency,ownerUsername,from,to,active`; **never include a full card number, PIN or CVV**. For other kinds, `code,nameRo,nameEn,from,to,active,json` remains available. Finance bank statements use `reference,date,merchant,amount,currency,settledRon`.

### Validation, error recovery and access

- Downloads have clear headers and fictional examples. **Replace sample rows** before production use.
- CSV UTF-8 BOM supports common spreadsheet tools; XLSX must contain one worksheet with text/numeric values only, no formulas.
- Imports are limited to 5 MB and 10,000 records; uploads are antivirus scanned.
- Preview shows valid rows and line-level errors. Do not confirm until **all errors are zero**.
- Employee/manager and ownership imports are **Admin-only**; Finance/Admin master imports retain their permission checks; bank statements are **Finance-only**.
- Changes are logged with actor and reason. Import retries use the existing idempotency mechanism; original document and financial evidence remain immutable.
- Never delete Docker volumes to apply these changes. Make a PostgreSQL backup before schema migration 12.

## Verification / Acceptanță

- [ ] Every listed sample is accessible from the correct menu and has import-ready headers.
- [ ] Manager creation works before assigning cost centre ownership.
- [ ] CSV and XLSX preview reject missing columns, bad dates, unknown cost centres, inactive managers and duplicate owners.
- [ ] Employee CSV supports CostCenter versus Fixed approval without enabling Admin/Finance roles.
- [ ] Card import refuses data beyond permitted metadata; no PAN/CVV.
- [ ] Manager review is read-only, Finance can edit original-linked fields and receipts with an audit reason.
- [ ] Fixed approver and dominant-centre routing are revalidated on submission and approval.
- [ ] SQLite/PostgreSQL migrations, tests, Playwright and Finance acceptance pass before release.
