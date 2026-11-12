# DECONT — începe testarea pe un mediu izolat

> **Scop:** pornirea și verificarea aplicației din `main` pentru testare internă. Acest ghid **nu aprobă producția**. Nu folosi baze, chei, bucketuri sau documente reale de producție în configurația locală.

## 1. Actualizează sursa și pornește mediul DEV

Ai nevoie de Docker Engine/Desktop cu Compose, Linux containers și resurse suficiente pentru PostgreSQL, ClamAV, serviciul OCR PaddleOCR CPU (până la ~5 GB) și aplicație.

```bash
git checkout main
git pull --ff-only
bash ./start-local.sh
docker compose ps
curl --fail http://localhost:5080/health/live
curl --fail http://localhost:5080/health/ready
```

Pe Windows: `./Start-Local.ps1` din PowerShell în directorul repositoryului. Prima lansare creează `.env` local cu secrete aleatorii. Contul de bootstrap și parola sunt acolo. **Nu publica `.env`**, nu partaja parole, nu folosi `docker compose down -v` pentru a repara date existente. Vizitează `http://localhost:5080`, autentifică-te, schimbă parola temporară.

La repornire, folosește `docker compose up -d web`. După modificarea codului, `docker compose up -d --build web` (și `ocr` dacă s-a modificat imaginea OCR). Pentru jurnal, `docker compose logs --tail=150 web scanner ocr db`. Dacă imaginea OCR își descarcă prima dată modelele, verifică `docker compose ps` până când devine healthy.

**Atenție la migrații:** în Development, `--bootstrap` din `start-local.sh` verifică/aplică migrările versionate. În Production, NU folosi `--init`/`--bootstrap` pentru DDL: numai jobul separat `--migrate` și procedura din [runbook](P0_RELEASE_RUNBOOK.md). Nu goli volumele pentru a elimina o eroare de migrare. Pentru baze existente restaurează mai întâi o clonă de test.

## 2. Configurație minimă pentru un flux complet

1. Ca administrator, creează câte un utilizator de test cu rolurile Employee, Manager și Finance. Un cont Employee trebuie să aibă worker/LantID și centru de cost; configurează responsabilul centrului de cost.
2. Configurează în Finance tipurile de cheltuieli, ratele de TVA 0/11/21 și mapările contabile datate; adaugă un curs EUR/RON de test și o configurație ERP de test. Nu folosi conturi contabile false în datele reale.
3. Ca Employee, creează un decont; încarcă un bon RO sintetic și o factură străină de test; verifică pagina originală, numărul de bonuri, valoarea totală, data, moneda și TVA. Bonul nu se deschide automat — alege-l în panoul de verificare.
4. Pentru fiecare pagină încărcată, confirmă explicit **toate bonurile vizibile** (inclusiv pagini cu mai multe bonuri sau pagini justificative) înainte de trimiterea la aprobare. O pagină incompletă trebuie să blocheze submit.
5. Aprobă ca Manager; verifică în Finance și generează PDF, XLSX/ERP și ZIP. Verifică hashurile manifestului și identitatea fiecărui original.
6. Încearcă un retry al unei salvări cu conexiunea întreruptă, două taburi deschise și o revenire după refresh: nu trebuie să apară duplicate, iar un conflict de ciornă trebuie să fie explicit. Recuperarea locală criptată cere alegere din partea utilizatorului.
7. Pe tastatură și mobil, verifică selecția regiunilor OCR, focusul și butonul „Salvează și următorul”. Nu considera afișarea unui procent OCR drept confirmare financiară.

## 3. Verificări automate de pe sursa curentă

Fără Docker, pentru testele de cod sunt necesare Node 22 și .NET SDK 10. Testele backend pe PostgreSQL se rulează și în GitHub Actions; local, setarea `TEST_POSTGRES` este opțională pentru suita separată de integrare PostgreSQL.

```bash
npm ci --prefix ui
npm test --prefix ui
npm run build --prefix ui
dotnet test tests/Decont.Tests.csproj --configuration Release
python3 -m unittest discover -s deploy -p 'test_*.py' -v
```

GitHub Actions [Validate expense platform](https://github.com/Cozmyn/Expense_report/actions/workflows/verify.yml) rulează și matricea SQLite/PostgreSQL, testele reale de browser Chromium + mobil, pornirea containerului și contractul de migrații. Verifică **commit-ul `main`**, nu doar un PR mai vechi. Pentru a reproduce Playwright local trebuie aplicația și serviciile reale active (ClamAV, OCR), apoi `npm run test:e2e --prefix ui`.

## 4. Când consemnezi un defect

Păstrează ID-ul documentului/decontului, pașii, data, tipul browserului, versiunea/commitul, scenariul T01–T22 (dacă se aplică) și rezultatul așteptat/observat. Redactează nume, CUI, sume personale, cookieuri și tokenuri din screenshoturi, trace-uri și loguri înainte să le atașezi unui issue. **Nu publica documente originale reale în GitHub.**

Pentru OCR, notează câmpurile greșite (sumă, dată, monedă, furnizor, TVA), numărul de bonuri omise/unite/separate și timpul până la confirmarea umană; evaluatorul `deploy/ocr_pilot_metrics.py` calculează indicatorii dintr-un set de 100–200 de documente adnotate, dar CI folosește numai date sintetice.

## 5. Ce rămâne obligatoriu înainte de Production

Testarea locală nu demonstrează: restore PostgreSQL + obiecte S3 + chei, TLS/HTTPS real, scanner/OCR/SMTP în configurația Production, load multi-instanță, pilot cu bonuri reale sau semnătura Finance/Operations. Criteriile și dovezile sunt în [issue #24](https://github.com/Cozmyn/Expense_report/issues/24), iar pașii P0 în [runbook](P0_RELEASE_RUNBOOK.md). În lipsa acestor dovezi, nu promova mediul în Production.
