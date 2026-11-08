# Aprobări și administrare / Approvals and administration

## Reguli confirmate / Confirmed rules

| Domeniu / Area | Română | English |
|---|---|---|
| Companie / Company | O singură companie; aplicație internă. | One company; internal application. |
| Identitate / Identity | LantID: exact șase litere ASCII și două cifre. Unic fără diferențiere între majuscule și minuscule. | LantID: exactly six ASCII letters and two digits, unique irrespective of case. |
| Manager | Admin stabilește alocări cu perioadă de valabilitate. Managerul se determină la trimitere. | Admin maintains dated assignments. Manager is resolved at submission. |
| Reasignare / Reassignment | Traseul trimis nu se schimbă automat. Admin poate reasigna cu motiv; fostul responsabil pierde accesul la aprobarea în curs. | Submitted routes do not move automatically. Admin can reassign with a reason; the former assignee loses pending-report access. |
| Corecții / Corrections | Corecțiile contabile nu cer reaprobare dacă diferența cumulată este de cel mult 50 RON. Peste 50 RON, în plus sau în minus, Finance confirmă avertizarea înainte de salvare și se cere reaprobare. | Accounting corrections need no reapproval for a cumulative difference up to 50 RON. Above 50 RON in either direction, Finance confirms the warning before saving and manager reapproval is requested. |
| FX | O modificare exclusiv de curs nu cere reaprobare. Diferențele din document se compară folosind evaluarea aprobată, pentru a exclude variația de curs. | FX-only changes never require reapproval. Source corrections use the approved valuation to exclude exchange-rate movements. |
| Configurare / Configuration | GL și mapările ERP nu cer un al doilea aprobator. Controlul independent existent pentru politicile TVA rămâne separat. | GL and ERP mappings need no second approver. Configuration drafts can be saved without changing live data, then validated and published by the same authorized editor. Existing independent VAT-policy approval remains separate. |
| Export | Numai Finance descarcă fișierele contabile XLSX. Formatul A:X și valorile sursă sunt păstrate. | Only Finance downloads accounting XLSX files. A:X structure and source amounts are preserved. |

Nu se adaugă OIDC enterprise sau titular de card. Limita rămâne 50 de bonuri active/decont, cu deconturi separate pentru plăți personale și card de companie. Cotele 5% și 19% rămân indisponibile. / No enterprise OIDC or card-owner module is added. The limit remains 50 active receipts/report, with separate personal/company-card reports. 5% and 19% remain unavailable.

## Angajat / Employee

1. Creează decontul. Identitatea și departamentul se preiau din cont/alocare; metoda de plată și cardul se aleg în antet.
2. Încarcă bonurile individual sau într-un fișier comun. Verifică grupările, toate paginile și propunerile OCR pe original.
3. Completează fiecare cheltuială, bazele/TVA și atașamentele necesare. Salvează și trimite.
4. Dacă lipsește managerul sau LantID este invalid, contactează Admin. Nu există aprobare automată sau manager ales arbitrar.
5. Pentru „Returnat pentru corecții”, citește motivul/comentariile, corectează și retrimite. Revizia anterioară se păstrează.

Create → complete header → upload/review OCR → verify receipt data/evidence → submit. Returned reports can be corrected and resubmitted as a new revision. Permanently rejected reports are read-only. Cancelling an editable report preserves its documents/history.

## Manager

Folosește **Aprobări**. Caută angajatul/departamentul sau filtrează după stare, vechime și sumă. Deschide documentele și revizia afișată. **Aprobă revizia**, **Returnează pentru corecții** sau **Respinge definitiv**. Returnarea/respingerea necesită motiv. Confirmarea indică revizia și suma exactă.

Use **Approvals** to review assigned tasks, original documents and the submitted revision. Approve, return for correction, or reject permanently. Return/rejection needs a reason. A concurrent or stale decision receives a conflict instead of overwriting another decision. Delegation is finite and must remain valid when acting; the claimant cannot self-approve.

## Finance / Accounting

Corectează datele cu motiv. Pentru o modificare care depășește cumulativ 50 RON, avertizarea apare **înainte de salvare**. La exact 50 RON nu se cere reaprobare. Modificările succesive se compară cu aceeași revizie aprobată, nu doar cu ultimul câmp modificat.

Exemplu: 100 → 130 RON nu cere reaprobare. Apoi 130 → 160 RON înseamnă +60 RON față de aprobare și cere reaprobare. 100 → 49,99 RON cere, de asemenea, reaprobare. O corecție exclusiv a distribuției TVA cu total neschimbat nu cere reaprobare.

Finance may correct source data with a reason. Confirmation is required before a material correction is saved. The server also enforces confirmation for direct API calls; dismissing the warning does not save the change. FX-only changes remain exempt. Complete the financial review and posting preflight before export. GL/ERP configuration changes create effective-dated versions without a second approver.

O corecție a angajatului rămâne un flux Finance controlat și cere reaprobare; managerul se preia din alocarea stabilită de Admin, nu se alege liber. / Correcting a report's employee remains a reasoned Finance action requiring reapproval; its manager is resolved from Admin's assignment.

## Admin

**Utilizatori și acces:** gestionează conturi și LantID. Pentru un cont cu manager, se creează și alocarea datată. Departamentul se selectează o dată în profil, din nomenclator, apoi se preia automat în decont. Contul tehnic de bootstrap poate să nu aibă LantID; înainte de a depune deconturi trebuie completată o identitate validă.

**Alocări organizaționale:** verifică arborele, angajații fără manager și aprobările vechi care necesită confirmare. Creează/închide perioade; nu suprapune alocările. Pentru schimbări viitoare existente, actualizează explicit perioadele în locul unei suprascrieri generale din cont.

**Importă organigrama:** descarcă șablonul CSV. Se acceptă CSV/XLSX cu coloanele `username,managerUsername,department,from,to,active`. Datele sunt `YYYY-MM-DD`, iar active este `true/false`. Verificarea arată erorile fără a crea alocări. Confirmarea importă integral lotul valid, cu motiv și proveniență. Limita de 1.000 este pentru un lot de import, nu pentru numărul total de angajați. Exportul păstrează istoricul.

**Reasignare aprobare:** actualizează întâi organigrama; apoi deschide decontul și confirmă noul manager și motivul. Nu se modifică retroactiv revizia financiară sau deciziile anterioare. Nu există escaladare automată către alt manager.

Admin controls employee identities and effective-dated organisation assignments. Cycles, self-management, overlapping periods, inactive/ineligible approvers and invalid delegation are blocked. Updating the chart does not move existing tasks; reassign the pending task explicitly. Background operations show approval age, vendor/FX exceptions, notification backlog and failed jobs without exposing receipts in metrics.

## Actualizare / Upgrade

Back up database and original-document storage together, stop the previous application, build the new image, run `--init` using the documented setup service, then start the web service. Schema release **10** adds persisted configuration drafts, typed approval routes/tasks, audit correlation, case-insensitive LantID uniqueness and a superseded marker preserving same-day assignment history. Upgrades are additive and repeatable. Duplicate historical LantIDs must be resolved by Admin before the unique index can be installed; no identity is silently renamed.

Pending legacy reports without a typed approval task are shown in Admin's issue queue. Admin must confirm the organisation assignment and reassign the task before a manager can decide. Historical decisions and exports are not fabricated, rewritten or removed.

Rollback: stop writes and restore a coordinated pre-upgrade database/object backup with the previous image. Do not run an older image against a newer, actively changing schema and call that a safe rollback. Hosting, RPO/RTO, legal retention, real ERP acceptance and actual production release require the company's operating owners and evidence.

## API

Authenticated same-origin requests use CSRF protection; mutations also use `Idempotency-Key`.

| Method / route (under `/api`) | Contract |
|---|---|
| GET `/org/me/manager?as_of=YYYY-MM-DD` | Current user's dated assignment; no profile-manager fallback. |
| GET `/approvals/inbox` | Assigned pending tasks, scoped to authenticated manager. |
| GET `/approvals/tasks/{id}` | Authorized task, immutable route and report view. |
| POST `/approvals/tasks/{id}/{approve|return|reject}` | `{version,reportVersion,reason}`; required reason for return/reject. |
| POST `/reports/{id}/actions/{action}` | Existing report-based action retained; same task/permission checks. |
| GET `/admin/approval-issues` | Missing/invalid employee routes and legacy tasks requiring confirmation. |
| POST `/admin/reports/{id}/reassign` | `{version,reportVersion,managerId,reason}`; Admin-only, pending reports only. |
| POST `/finance/reports/{id}/change-preview` | `{lineId?,data?,operation}` where operation is replace/add/archive/duplicate. Returns approved/proposed RON, signed difference and threshold decision; no report mutation. |
| PATCH/POST receipt mutation | Existing body plus `confirmReapproval:true` when required; server returns 409 without it. |
| GET `/admin/workflow-metrics` | Aggregated operational counters, Admin only. |
| GET `/admin/organisation-import/{template|export}` | Typed template or dated assignment-history CSV. |
| POST `/admin/organisation-import/preview` | Multipart file; rows/errors/source and input hashes. |
| POST `/admin/organisation-import/confirm` | `{rows,inputHash,sourceHash,reason}`; validates again and imports atomically. |

409 means the version changed, the operation conflicts, or material-change confirmation is missing. Reload, inspect the current revision and repeat deliberately; never automatically approve a changed revision. 403/404 deny unauthorized actions without revealing another employee's document.

## Verificare de către beneficiar / Owner QA

- Create valid `ABCDEF12`; reject `ABCDEF123` and a mixed-case duplicate.
- Submit after a dated manager change; confirm the submission uses the effective assignment.
- Return, correct and resubmit; confirm the old decision/snapshot remains visible.
- Compare +50, +50.01 and -50.01 RON changes; dismiss then accept the material-change warning.
- Change only FX, or VAT distribution with unchanged gross; confirm no manager reapproval.
- Admin changes the manager and reassigns a pending task; the old manager can no longer open it.
- Import an overlapping/cyclic organisation file; confirm no partial import.
- Resolve Finance exceptions, complete review, and verify the unchanged 24-column XLSX against approved expected values.

Automated results and human acceptance are separate. Synthetic test documents are not real Finance/ERP acceptance, a legal assessment, a penetration test or a production recovery drill.

## Ciorne de configurare / Configuration drafts

În Nomenclatoare, **Salvează ciorna** păstrează datele fără să le aplice deconturilor. **Continuă editarea** redeschide o ciornă. **Publică versiunea** verifică din nou câmpurile obligatorii, perioadele, codurile și permisiunile, apoi creează versiunea cu data de efect. GL/ERP nu cer alt aprobator. Politicile TVA păstrează fluxul separat de control independent.

Configuration drafts have version checks, author/change history and cancellation with a reason. Publication is atomic; a failed validation leaves the draft available for correction. Published/cancelled draft history cannot be rewritten. GL/local-account and ERP dimension codes must be completed before publication. Authorized direct master publication remains compatible with existing integrations and uses the same server validation.

New routes: GET/POST `/api/configuration/drafts`, PUT `/api/configuration/drafts/{id}`, POST `/api/configuration/drafts/{id}/publish` and `/cancel`. Save body: `{data: MasterInput, version?}`. Publish/cancel: `{version, reason}`. All are Finance/Admin scoped, with Finance-only FX and vehicle controls. No additional approval task is created for GL/ERP publication.
