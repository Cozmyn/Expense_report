# Modificări / Changelog

## 2026-10-10 — primele corecții ale consiliului / first council fixes

- Corrected tolerance-aware vehicle VAT branch selection without dropping zero-rate base amounts; effective VAT policy participates in the posting fingerprint.
- Added read-only authenticated `/api/vat-validation` for client/server RON arithmetic parity and visible retry feedback.
- Protected zero-amount VAT validation and excluded non-applicable projects from validation/export; project label reflects required policy.
- Added Romanian/English self-service profile, password change and language selection, including a language switch during forced password change.
- Applied focus, touch, reduced-motion, helper/status contrast and scrollable-navigation foundations.
- Added 18 backend regression cases and desktop/mobile profile/password checks.
- Preserved all 1,579 traceability IDs; documented unimplemented requirements and corrected completion claims.

No production deployment, database migration, full OCR redesign, full WCAG certification or 100% requirements acceptance is claimed. Exact verified commit and CI results are in `council/VERIFICATION-CURRENT.json`.

### Document preview / Previzualizare documente

- Original-left desktop layout and original-first mobile reading order.
- Bilingual image zoom/rotation/reset, loading/error/retry and original link.
- Desktop/mobile regression assertions for the image controls.
- PDF navigation is still native; OCR field geometry remains open. See council/PHASE-3-DOCUMENT-PREVIEW.md.
