# UX Wave 2: functional preservation and acceptance

Wave 2 is a **presentation and workflow-safety pass** on top of Wave 1. It does not change backend validation, financial rules, OCR engine, audit logging, role permissions, import/export formats, or ERP effects.

## Critical scenarios

### Finance worklist
- Filter by state, exception code, employee, manager, supplier, card and age; save/load/reset filter preferences.
- Sorting by amount, employee, age gives the same order as before.
- Open a report from any row; the visible status is translated but its raw state remains available in filters and backend payloads.
- Verify each total is correct in **RON**, with original report line count.
- Any unmapped exception code is still visible in audit form; don't silently hide blockers.

### Manager approvals
- Seed at least **6 pending actionable** and 2 unauthorized reports.
- Dashboard initially shows the 4 oldest actionable ones, and announces the actual total.
- Expand to show **all 6**, not unauthorized rows; collapse back to 4.
- Open and approve/reject from the existing report detail; maintain server-based `canApprove` check and delegated authorization.

### Reconciliation
- Import a CSV/XLSX statement, preview duplicate and mapping errors, save card mapping.
- Pick a suggestion and ensure scrolling/focus moves to the allocation editor with transaction, receipt and amount.
- Switch transaction/receipt and ensure prior amount is cleared to prevent stale posting.
- Try amount exceeding remaining transaction/receipt balance: the UI must block, server must still validate.
- Confirm a permitted partial amount, multiple allocation to same transaction, refunds/negative amounts and bank charges.
- Select among multiple suggestions; visually inspect source report before confirming.
- Test undo on 2 different confirmed matches; separate reasons must not cross over.
- After each action, remaining amounts and confirmed allocations must refresh.

### OCR review
- Single receipt and a multipage PDF with 3+ distinct receipts still work.
- Original preview remains visible while verifying each OCR group.
- Advanced merge/split and supporting-page decisions remain available under the new disclosure.
- When OCR finds unassigned pages, advanced controls are open and focusable.
- Required grouping reasons and per-page visible-receipt coverage attestations remain enforced.
- Existing audit history, raw text and provenance are still reachable.
- Screen reader reads the expandable advanced-tools label; keyboard Enter/Space toggles it.

### Mobile and accessibility
- Employee, Manager and Finance desktop/mobile flows at 320px, 390px, 768px, 200-400% zoom.
- All action buttons can be reached by keyboard, screen reader announces notices, no accidental actions when menu closed.
- Check readable contrast, target size, long item names and French/English/Romanian overflow as relevant.
- Do not use color as the only cue for blocking exceptions.

## CI gate
- `npm test --prefix ui`
- `npm run build --prefix ui`
- `npm run test:e2e --prefix ui` with isolated test users / records
- Existing .NET tests for SQLite and PostgreSQL
- Manual role-based UX checks for Finance, Employee, Manager, Auditor and Admin

This is an acceptance checklist, **not proof all tests passed**. Do not merge the PR into main or deploy until reviewed.
