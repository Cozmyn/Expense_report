# Protocol de modificari sigure / Safe-change protocol

Status: propunere tehnica pentru review; nu modifica regulile aplicatiei.
Baza inspectata: `main` la `5b2f93348b31c9311ecc2757ce44dbbd5c59a3e6`, 2026-11-08.

## Reguli obligatorii pentru acest demers

1. **Nu modificam functionalitatea fara aprobarea explicita a proprietarului.** Aici intra si "bug fixes" care schimba rezultatele financiare, permisiunile, starile, aprobarea, duplicatul, OCR, exportul, data de business, mesajele si contractele API.
2. **Nu stergem nimic fara confirmare.** Fisierele, testele, optiunile de configurare, mecanismele legacy si documentatia raman. Mutarea sau redenumirea unui fisier se trateaza ca o stergere si necesita acord.
3. **Nu rescriem codul pe care nu l-am inteles.** Inaintea oricarei schimbari, documentam scopul, apelantii, preconditiile, efectele, dependintele, testele relevante si compatibilitatea.
4. **Protejam in mod explicit:** calculul exact al banilor, regulile RO TVA/FX, deductibilitatea auto, limitele si state machine-ul de decont, OCR cu verificare umana, audit si imutabilitate, acces pe obiect, CSRF, idempotency, versiuni concurente, reconcilierea cardurilor si exportul ERP de 24 de coloane.
5. **Schimbari mici, separate si reversibile.** Fiecare PR trebuie sa spuna daca modifica sau nu comportamentul. Nu amestecam formatarea cu schimbari de logica.
6. **Verificam inainte de merge:** diff, teste unitare si de integrare SQLite/PostgreSQL, testele frontend, Playwright, container/CI; apoi teste dedicate pentru traseele atinse. Un CI verde nu inlocuieste acceptanta Finance si nici testul pe documente reale.
7. **Nu atingem date reale si nu rulam migrari pe baze existente** fara plan de back-up, restore verificat, scenariu de rollback si acceptanta proprietarului.
8. **Niciun merge automat pe `main`.** Schimbarile raman intr-un branch separat, prin PR draft, pentru aprobare.

## Clase de schimbari

| Clasa | Exemple | Actiune |
| --- | --- | --- |
| A: fara efect de runtime | `.editorconfig`, politica formatter, documentarea constatarilor, instrumente doar de inspectie | Permise ca propuneri pe branch, fara stergeri |
| B: presupus compatibile, dar cod atins | formatare mecanica, extragere functii, cache, `AsNoTracking`, reorganizare componente | Numai dupa intelegerea fluxului, diff verificat si teste; fara merge automat |
| C: posibil efect functional | reguli de aprobare, duplicate, login, retentie, paginare API, DateOnly/time zone, SSO, RBAC, upload, rutare | Mai intai cerinta si acceptanta explicita |
| D: schema / date / infrastructura | EF migrations, inlocuire `SchemaUpgrades`, mutare date JSON, eliminare SQLite, secrete, backup/restore | Plan separat si confirmare explicita inaintea oricarei migrari sau stergeri |

## Checklist pentru fiecare PR ulterior

- [ ] Am consemnat comportamentul actual si de ce exista.
- [ ] Am identificat exact functiile si contractele afectate.
- [ ] Am marcat explicit orice schimbare de comportament si am cerut aprobarea.
- [ ] Nu s-a eliminat nicio functie, ruta, fisier, test sau contract fara confirmare.
- [ ] Diff-ul este limitat la scop; formatarea nu este amestecata cu logica.
- [ ] Am adaugat/mentinut teste de caracterizare pentru rezultatele existente.
- [ ] Am verificat CI; am consemnat orice test imposibil de rulat.
- [ ] Exista o cale sigura de reversie inainte de merge.

## Ce contine primul branch

Acest branch incepe doar cu configuratie de formatare si documentatie de review.
**Nu** include formatarea in masa, schimbari in sursele C#/React, migrari, stergeri, restructurare de API, schimbari de permisiuni sau de business.
