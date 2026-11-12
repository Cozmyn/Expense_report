# DECONT — P0 release rehearsal / Procedură de acceptanță Production

**Status:** acest document descrie un proces executabil și verificabil, nu un certificat de Production. Raportul de audit din 11.11.2026 cere DB-01, OPS-01, OPS-02 și OPS-03. OCR-05 (CUI hardcodat) a fost remediat anterior în PR #18. Valorile RPO/RTO și acceptanța ERP aparțin Finance/Operations; nu sunt implicit aprobate.

> **REGULĂ:** Nu rulați comenzi de restaurare ori teste destructive pe PROD. Nu rulați `docker compose down -v` pe date reale. Testele de login repetat, resetare, export, downgrade/rollback și OCR se efectuează exclusiv într-un UAT izolat.

## 1. DB-01 — migrații explicite, versionate și reversibilitate operațională

Schema țintă este release **14**. Scripturile verificate sunt stocate în `server/Migrations/` și incluse în assembly, cu SHA-256 fixat în `SchemaUpgrades.cs`. Generatorul EF (`--schema`) este disponibil numai pentru **compararea modelului**, nu pentru a construi upgrade-ul la runtime. `SchemaUpgrades` nu mai extrage DDL prin expresii regulate.

- `001-baseline.postgres.sql` / `.sqlite.sql`: instalare nouă într-o bază **goală**, în tranzacție.
- `013a-legacy-tables.*` + coloanele enumerate explicit + `013b-legacy-indexes.*`: traseu aditiv pentru instalațiile istorice fără baseline-ul 13. Scripturile sunt statice și verifică ulterior întregul model.
- Release **13 → 14**: numai verificare read-only a schemei existente și înregistrarea contractului de migrație; niciun DDL ascuns.
- Release **14 → 14**: read-only și idempotent; o schemă modificată, un index/trigger lipsă sau o versiune viitoare duc la oprire.
- Triggerele imutabile sunt introduse în tranzacția migrației, iar versiunea se înregistrează **numai după** verificare.

`--migrate` este singura comandă pentru DDL în Production. Folosiți un cont PostgreSQL separat, cu drepturi de migrare, într-un job one-shot; nu păstrați acea identitate în aplicația web.

```bash
# Imagine aprobată prin digest, niciodată rebuild în ziua lansării
export IMAGE='registry.example.invalid/decont@sha256:REPLACE_WITH_APPROVED_DIGEST'

# Folosiți managerul de secrete al platformei pentru variabilele de conectare,
# inclusiv ConnectionStrings__Database cu SSL Mode=VerifyFull și CA de încredere.
# Mai întâi repetați pe o CLONĂ RESTAURATĂ, nu pe sursa de producție.
docker run --rm --network decont-private --env-file /secure/migrator.env \
  "$IMAGE" --migrate
```

**Permisiuni PostgreSQL:** runtime primește numai USAGE pe schema sa și SELECT/INSERT/UPDATE/DELETE pe tabelele necesare, plus drepturi pe secvențe. Revocați CREATE/ALTER/DROP/TRUNCATE/TRIGGER pentru runtime. Migratorul și administratorul backup sunt identități distincte. Pentru instalările PostgreSQL folosiți o schemă dedicată; nu acordați drepturi DDL identității HTTP.

**Verificări obligatorii pe o copie de bază 3–12/13:** fotografiați numărul de deconturi, linii, utilizatori, snapshots, audits, batches, hashurile obiectelor și ID-urile cheie înainte de upgrade; rulați migrația de două ori; comparați rândurile și constrângerile fizice (coloane, chei străine, indecși, checks, triggere) cu fresh install. Includeți teste de întrerupere și două joburi concurente. CI verifică automat instalarea nouă, un scenariu legacy cu tabel/coloană/index lipsă și respingerea unei scheme incomplete pe SQLite și PostgreSQL. **Nu presupuneți că testul sintetic reprezintă fiecare schemă istorică reală:** arhivele bazelor folosite efectiv trebuie testate separat.

**Rollback:** nu implementați `DROP COLUMN` ca strategie standard. O migrație aditivă trebuie să permită revenirea la digestul anterior al **aplicației**; testați aceeași bază 14 cu imaginea anterioară și păstrarea tuturor datelor. Dacă incompatibilitatea este observată, opriți promovarea și folosiți un roll-forward de compatibilitate. Recuperarea DB la un punct anterior se face numai prin procedura OPS-03 și aprobarea Finance.

## 2. OPS-03 — restore coordonat PostgreSQL / obiecte / chei

**Snapshot coerent:** opriți temporar noi încărcări și joburi care produc fișiere, goliți coada, notați data/ora UTC și WAL LSN, faceți backup PostgreSQL (PITR/WAL + arhivă custom pentru exercițiul secundar), backup versionat și criptat al tuturor obiectelor S3 și backup protejat al tuturor fișierelor `key-*.xml` din `Data__Directory/keys`. Verificați că obiectele și exporturile nu au rămas în afara snapshotului. Backupurile și manifestul se păstrează într-o locație separată, cu acces minim.

Manifest minimal (`snapshot/manifest.json`):

```json
{
  "snapshot_utc": "2026-11-12T10:00:00Z",
  "database_sha256": "SHA256_HEX_AL_FISIERULUI_BACKUP",
  "keys": {
    "key-UUID.xml": "SHA256_HEX_AL_FISIERULUI_DE_CHEIE"
  }
}
```

Pentru un dump custom `pg_dump -Fc`, creați manifestul din bytes-ii reali, nu introduceți valori fictive. În cazul PITR, `database_sha256` identifică un fișier de catalog de backup/PITR verificat separat; `--verify-existing` nu cere antetul PGDMP.

```bash
# EXEMPLU: sursa este snapshotul aprobat, nu instalația live
python3 deploy/restore_drill.py \
  --database-dump /secure/snapshot/database.dump \
  --backup-objects /secure/snapshot/objects \
  --backup-keys /secure/snapshot/keys \
  --manifest /secure/snapshot/manifest.json \
  --report /secure/evidence/restore-backup-check.json

# Operatorul creează DEJA o bază PostgreSQL UAT GOALĂ și un bucket S3
# UAT GOL, cu bucket policy non-public, ACL privat și TLS certificat.
# PGHOST/PGPORT/PGUSER/PGPASSWORD și AWS_* vin din managerul de secrete.
python3 deploy/restore_drill.py --execute \
  --database-dump /secure/snapshot/database.dump \
  --backup-objects /secure/snapshot/objects \
  --backup-keys /secure/snapshot/keys \
  --manifest /secure/snapshot/manifest.json \
  --database decont_restore_exercise1 \
  --bucket decont-restore-exercise1 \
  --s3-endpoint https://s3.uat.example.invalid \
  --target-keys /secure/recovered-keyrings/exercise1 \
  --incident-utc 2026-11-12T10:05:00Z \
  --max-rpo-minutes 30 --max-rto-seconds 3600 \
  --confirm RESTORE_TO_ISOLATED_UAT_ONLY \
  --report /secure/evidence/restore-custom-dump.json
```

Scriptul refuză ținte neizolate, DB/bucket cu date preexistente, key-ring preexistent, simbolic links, hashuri greșite și ieșiri fără ținte aprobate; revalidează prin **GET din S3** fiecare original (`Evidence`, `ReportAttachments`, inclusiv arhivate) și fiecare export XLSX (`Batches`). Dacă nu există nici măcar un export pentru proba de acceptanță, exercițiul nu trece.

**Obligatoriu și PITR:** restaurați folosind mecanismul real WAL/PITR al operatorului (ex.: pgBackRest sau mecanismul furnizorului), precum și snapshotul S3 în bucket UAT și key-ringul în directorul UAT. După finalizarea restaurării, rulați **verificarea independentă**, fără reîncărcare ori rescriere:

```bash
python3 deploy/restore_drill.py --execute --verify-existing \
  --database-dump /secure/snapshot/pitr-catalog-anchor \
  --backup-objects /secure/snapshot/objects \
  --backup-keys /secure/snapshot/keys \
  --manifest /secure/snapshot/pitr-manifest.json \
  --database decont_restore_pitr01 --bucket decont-restore-pitr01 \
  --s3-endpoint https://s3.uat.example.invalid \
  --target-keys /secure/recovered-keyrings/pitr01 \
  --restore-start-utc 2026-11-12T10:06:00Z \
  --incident-utc 2026-11-12T10:05:00Z \
  --max-rpo-minutes 30 --max-rto-seconds 3600 \
  --confirm RESTORE_TO_ISOLATED_UAT_ONLY \
  --report /secure/evidence/restore.json
```

RPO se calculează din `incident_utc - snapshot_utc`; RTO include în modul PITR intervalul dintre `restore-start-utc` și sfârșitul verificării. **Valorile de mai sus sunt exemple, nu SLA asumate.** Limitele reale trebuie aprobate înainte de probă.

Continuați cu aplicația Production-mode pe baza restaurată și **exact același key-ring restaurat**. Verificați login cu sesiune păstrată după restart, deschiderea documentelor originale și PDF/XLSX, auditul și integritatea soldurilor. Scriptul de restore nu certifică independent că login/Finance funcționează.

## 3. OPS-01 și OPS-02 — UAT Production cu ingress și trafic real

Folosiți infrastructură izolată cu `ASPNETCORE_ENVIRONMENT=Production`, PostgreSQL `SSL Mode=VerifyFull`, bucket S3 privat+criptat+versionat, ClamAV pe rețea privată, serviciul OCR efectiv, SMTP de test cu STARTTLS și chei Data Protection persistente criptate prin mecanismul platformei. Aplicația verifică la startup release 14 și schema fizică, **fără a executa DDL**.

Folosiți `deploy/nginx/expense-uat.conf.example` drept referință: certificate HTTPS reale, două instanțe web, `X-Forwarded-For $remote_addr` (nu `$proxy_add_x_forwarded_for`), `ReverseProxy__KnownProxies` cu IP-urile reale ale ingressului și limitarea comună de autentificare/API. Dacă sunt mai multe noduri ingress, aplicați controlul global și la WAF/LB. Testați `nginx -t` cu certificatele reale și demonstrați că trimiterile directe de `X-Forwarded-For/Proto` nu pot falsifica IP/schemă.

```bash
# Doar pe runner UAT cu certificate CA și conectivitate reală.
python3 deploy/production_preflight.py --json

export UAT_USERNAME='dedicated-test-account'  # din vault, nu în loguri
export UAT_PASSWORD='FROM_SECRET_MANAGER'
python3 deploy/production_uat.py \
  --base-url https://uat.expense.example.invalid --mode initial \
  --cookie-jar /secure/evidence/uat-session.cookies \
  --report /secure/evidence/uat-initial.json

# După restart REAL al ambelor instanțe, cu aceleași chei restaurate:
python3 deploy/production_uat.py \
  --base-url https://uat.expense.example.invalid --mode resume \
  --cookie-jar /secure/evidence/uat-session.cookies \
  --report /secure/evidence/uat-resume.json

# Pe hostname izolat (apelează cel mult 15 autentificări cu user inexistent):
python3 deploy/production_uat.py \
  --base-url https://uat.expense.example.invalid --mode ingress \
  --report /secure/evidence/uat-ingress.json

# ÎNTREGUL flux de browser este potențial mutativ: DOAR UAT izolat!
DECONT_E2E_BASE_URL=https://uat.expense.example.invalid \
  npm run test:e2e --prefix ui
```

Testele HTTPS verifică certificat și hostname, CSRF, `Secure/HttpOnly/SameSite=Strict`, login, sesiunea după restart, încercarea de spoofing și răspunsul **429** la trafic de login împărțit/falsificat. Nu dezactivați verificarea TLS, nu folosiți `curl -k`.

**Probe de dependențe reale (obligatorii pentru P0):** configurați separat un runner cu acces doar la mediul UAT. Scriptul `deploy/uat_dependencies.py` rulează interogare PostgreSQL prin `PGSSLMODE=verify-full` și CA explicit, inspectează politicile de acces public / criptarea / versionarea unui bucket `decont-uat-...`, trimite payloaduri clean și EICAR (semnătura benignă de test antivirus) prin ClamAV INSTREAM, interoghează `/health` al serviciului OCR și verifică SMTP STARTTLS. Numai cu `--confirm-email` trimite un mesaj de test către inboxul explicit `UAT_SMTP_RECIPIENT`. Confirmați recepția în acel inbox printr-un artefact separat.

```bash
export PGHOST=pg.uat.internal PGSSLMODE=verify-full
export PGSSLROOTCERT=/secure/ca/pg-root.pem
# Restul PGUSER/PGPASSWORD/PGDATABASE din secret manager
export UAT_S3_ENDPOINT=https://s3.uat.example.invalid
export UAT_S3_BUCKET=decont-uat-testsnapshot
# AWS_ACCESS_KEY_ID/AWS_SECRET_ACCESS_KEY doar din secret manager
export UAT_SCANNER_HOST=clamav.uat.internal UAT_SCANNER_PORT=3310
export UAT_OCR_URL=http://ocr.uat.internal:8750
export UAT_SMTP_HOST=smtp.uat.internal UAT_SMTP_PORT=587
export UAT_SMTP_FROM=decont-uat@example.invalid
export UAT_SMTP_RECIPIENT=approved-test-inbox@example.invalid
python3 deploy/uat_dependencies.py --uat-origin https://uat.expense.example.invalid \
  --confirm-email --report /secure/evidence/uat-dependencies.json
```

Dacă S3-ul compatibil nu implementează API-urile AWS necesare pentru verificarea politicii private, testul **refuză aprobarea**; trebuie implementat/verificat explicit adaptorul furnizorului, nu dezactivată verificarea. La fel, dacă modelul OCR nu este încărcat, scannerul nu detectează EICAR sau TLS-ul serverului PostgreSQL nu este verificat, release-ul rămâne blocat.

**Matrice de acceptanță cu dovezi atașate** (rezultatul nu poate fi `passed` fără capturi/loguri/trace și responsabil):

- Login/logout, password reset prin **SMTP real**, expirarea sesiunii; browser desktop/mobil prin HTTPS.
- Upload valid, infectat și indisponibil scanner; S3 privat plus checksum, excluderea obiectelor din acces public.
- OCR real, diacritice, fotografii slabe, PDF multipagină; separarea/regrouparea și confirmarea bonurilor.
- Aprobare Manager, verificare Finance, export XLSX/ERP golden file și PDF.
- Restart, două instanțe, menținerea sesiunii/cheilor; failover fără pierdere de documente.
- Rollback la digestul imaginii anterioare după migrație, cu date, indecși și trigger-ele intacte; apoi revenirea la digestul nou.
- **Aceeași imagine OCI digest** pentru build, UAT și promovare. Nu reconstruiți imaginea separat.

Formatul `workflows.json` pentru `deploy/release_gate.py` conține `built_image_digest`, `tested_image_digest`, `promoted_image_digest` (toate identice, `sha256:<64 hex>`) și `scenarios`: fiecare cod din `REQUIRED_SCENARIOS` este `{"status":"passed","artifact":"https://.../proof"}`. Nu includeți parole, tokenuri sau detalii financiare în URL-uri.

```bash
python3 deploy/release_gate.py --evidence-dir /secure/evidence \
  --report /secure/evidence/p0-release-candidate.json
```

Inputurile gate-ului: `restore.json` din PITR, `uat-initial.json`, `uat-resume.json`, `uat-ingress.json`, `uat-dependencies.json` (probe reale) și `workflows.json` cu toate scenariile. Outputul rămâne **`release_approved: false`**: analiza și aprobările separate Finance / Operations / Security sunt obligatorii. Rulați `python3 -m unittest discover -s deploy -p 'test_*.py' -v` pentru testele offline. **CI verde nu înseamnă că probele UAT din acest document au fost executate.**
