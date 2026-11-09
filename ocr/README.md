# PaddleOCR rollout — single active OCR engine

This branch switches **scanned image recognition** to PaddleOCR 3.7 / PP-OCRv6 (Romanian + English). The web API calls an **internal-only**, separate Python OCR service. It never sends receipts to a hosted third-party OCR API. The service has no published host port; its REST endpoint is available only on the Docker Compose network.

The existing native `pdftotext -bbox-layout` path is intentionally retained: a searchable PDF already contains usable text and positional evidence, so there is no reason to degrade it through raster OCR. Rotated image retries call **PaddleOCR** again, not Tesseract. Legacy Tesseract source files/packages are retained as requested, but they are not invoked by active image extraction.

## Starting locally

```sh
docker compose build ocr web
docker compose up -d db scanner ocr web
docker compose ps
```

The PaddleOCR model is downloaded **on initial startup** to the named `paddle-models` volume. This requires a configured download route to Hugging Face, sufficient bandwidth and disk capacity. The app waits for the OCR service's health check. Runtime models are then cached locally. It cannot work in an air-gapped environment until the weights are provisioned into that volume by an approved deployment process. The Python service runs as an unprivileged user, with a read-only root filesystem and a bounded temporary directory.

- Python package: `paddleocr==3.7.0`, which is Apache-2.0 licensed.
- Model: `PP-OCRv6`, `lang="ro"`; the unified Latin model also covers English.
- CPU memory reservation/limit: OCR service 3 GiB; the web container remains unchanged.
- Environment: `Ocr__ServiceUrl=http://ocr:8750`; `Ocr__Enabled=true`.
- Health: `GET http://ocr:8750/health` **within the Compose network only**.
- No second OCR engine runs in parallel. If PaddleOCR is unavailable, processing fails and users can review/enter receipts manually. Do not silently fall back to lower-quality extraction.

## How multiple receipts on a page work

The parser still proposes document boundaries using headings, totals, page numbering and geometrical text positions. In the verification screen a user can explicitly select the region belonging to each receipt, create a new document group for that region, and then review/confirm it as a separate expense. Original bytes, raw extraction, audit events and corrections remain available. Two receipts on one page must not overlap once verified. The parser cannot guarantee correct separation on scanned thermal paper: human review remains required.

## Supplier-specific learning (no migration)

The new `OcrFieldSelection` endpoint maps a user-drawn source region to a specific field and stores the source coordinates on the **unverified** document group. After that receipt has been verified using all existing financial validations, the annotation becomes eligible for learning.

`SupplierOcrLearning` inspects up to 300 most recently verified groups, uses **only explicitly selected** `HumanSelected` regions from at least **two different original documents**, and only suggests a missing value on a new receipt when:
1. The same valid supplier CUI is recognized on the new receipt.
2. The selected source regions on both historical documents are geometrically consistent.
3. The OCR text in the corresponding area parses as the required field.
4. The general parser has **not** already extracted a value for that field.

The new candidate is marked `Source="SupplierTemplate"`, `State="ReviewRequired"` and has **no recognition-confidence claim**. It does not change a previously verified document, does not create or overwrite any accounting posting, and cannot bypass existing Finance controls. This learns a *position template*, not the supplier's amount or VAT rate. Multiple layouts from one supplier are conservatively rejected rather than forced into a single template. Users can continue to enter values manually.

No database migrations or table deletes are required. Historical document groups, existing audit events and data are preserved. The original extraction JSON is written unchanged; only new group **suggestions** may include learned positions.

## Validation / current limitations

```sh
python3 -m unittest discover -s ocr -p 'test_*.py' -v
dotnet test tests/Decont.Tests.csproj
npm test --prefix ui
npm run build --prefix ui
```

The unit tests cover the adapter JSON contract, normalized positions, multiple receipts on one page, typed source selection, explicit training sample thresholds, supplier isolation and no override of existing values. GitHub CI runs these tests and the current SQLite/PostgreSQL/browser/container suites. **This does not yet constitute a real-image OCR performance benchmark.**

Before declaring PaddleOCR better than the previous Tesseract runtime, collect at least 30-50 **approved, representative real documents** (single-page, two receipts on one page, thermal paper, currencies, mixed VAT, tilted photos and searchable PDFs). Measure per-field correctness, segmentation precision/recall, manual correction time, CPU latency and peak memory. Do not use real financial records in public CI or for external training without a privacy review.

PaddleOCR is license-free; CPU, RAM, disk, model-download bandwidth, backup and operations are not free. Production rollout still requires deployment rehearsal, security review, and Finance sign-off.
