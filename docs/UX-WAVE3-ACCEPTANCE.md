# UX Wave 3 – navigation, notifications and report-list acceptance

Wave 3 changes only presentation, navigation structure and the interactions around work already supported by the backend. No roles, validation rules, accounting calculations, OCR data, or documents may be deleted.

## Navigation
- Verify all existing destinations for each role: Employee, Manager, Finance, Admin, Auditor.
- Sidebar groups are *visual organization*, not additional permissions.
- Confirm there are no duplicated entries or inaccessible Admin-only destinations for other roles.
- At 320px and 390px, closed sidebar must not be focusable; opening receives focus, Escape closes and returns focus.
- Group headings are legible without becoming inert buttons.

## Notifications
- Report notifications fetch the authorized current report before display. Unauthorized/missing report must present a recoverable error, not stale data.
- Non-report notifications route by role: Admin → Background jobs; Finance → Finance queue; Manager → Approvals; everyone else → Overview.
- A notice should only be marked read once the destination was reached; API failure remains reported.
- With 20+ notifications, popup stays scrollable and action buttons remain reachable by keyboard.
- Contrast, event name, date, unread state and explicit close button work in RO/EN.

## Report discovery
- Default order is unchanged.
- Search by report number, purpose, manager and employee; filter by status; sort by most/least recently updated and total amount.
- Reset restores default search, filter and order.
- Result count updates for filters and is announced accessibly.
- Desktop table and mobile cards represent the same filtered records. Clicking rows still opens the authorized report.
- 320px, 390px, 768px and desktop, 200–400% zoom; control targets and focus styles visible.

## Test gate
- Frontend unit tests + TypeScript/Vite build
- Existing backend tests on SQLite and PostgreSQL
- Chromium desktop and touch-mobile Playwright regression
- Manual assistive-technology review and responsive checks, without claiming certification
- Production deployment only after Wave 1, Wave 2 and Wave 3 PR review and acceptance

## Deferred consciously
- Shareable, authenticated deep links and full browser back/forward routing: this needs an explicit authorization-preserving route design, not a local-state-only URL decoration.
- Full WCAG 2.2 assessment needs actual assistive-technology users and verification of all feature areas.

The code is a proposal until the checks and human review pass; all PRs remain draft.
