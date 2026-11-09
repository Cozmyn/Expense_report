import { test, expect, request, APIRequestContext } from '@playwright/test';
const original = process.env.E2E_ADMIN_PASSWORD!,
  changed = 'Changed-test-Password2!',
  temporary = 'Valid-test-Password1!';
async function csrf(c: APIRequestContext) {
  return (await (await c.get('/api/auth/csrf')).json()).token;
}
async function mutate(c: APIRequestContext, path: string, data: any, method = 'POST') {
  const response = await c.fetch('/api' + path, {
    method,
    data,
    headers: { 'X-CSRF-TOKEN': await csrf(c), 'Idempotency-Key': crypto.randomUUID() },
  });
  expect(response.ok(), await response.text()).toBeTruthy();
  return response.status() === 204 ? null : response.json();
}
async function login(c: APIRequestContext, name: string, password: string) {
  const send = async () =>
    c.post('/api/auth/login', {
      data: { username: name, password },
      headers: { 'X-CSRF-TOKEN': await csrf(c) },
    });
  let response = await send();
  if (response.status() === 429) {
    await new Promise((resolve) => setTimeout(resolve, 60000));
    response = await send();
  }
  return response;
}
function pdf(pages: string[][]) {
  const objects: string[] = ['<< /Type /Catalog /Pages 2 0 R >>', ''];
  const pageIds: number[] = [];
  pages.forEach((lines) => {
    const page = objects.length + 1;
    pageIds.push(page);
    objects.push('', '');
    const stream =
      'BT /F1 16 Tf 45 780 Td ' +
      lines
        .map((line, n) => (n ? '0 -38 Td ' : '') + '(' + line.replace(/[\\()]/g, '\\$&') + ') Tj')
        .join(' ') +
      ' ET';
    objects[page] =
      '<< /Length ' + Buffer.byteLength(stream) + ' >>\nstream\n' + stream + '\nendstream';
  });
  const font = objects.length + 1;
  objects.push('<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>');
  pageIds.forEach(
    (page) =>
      (objects[page - 1] =
        `<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 ${font} 0 R >> >> /Contents ${page + 1} 0 R >>`),
  );
  objects[1] =
    '<< /Type /Pages /Kids [' +
    pageIds.map((id) => id + ' 0 R').join(' ') +
    '] /Count ' +
    pages.length +
    ' >>';
  let result = '%PDF-1.4\n';
  const offsets = [0];
  objects.forEach((v, i) => {
    offsets.push(Buffer.byteLength(result));
    result += i + 1 + ' 0 obj\n' + v + '\nendobj\n';
  });
  const start = Buffer.byteLength(result);
  result +=
    'xref\n0 ' +
    (objects.length + 1) +
    '\n0000000000 65535 f \n' +
    offsets
      .slice(1)
      .map((n) => String(n).padStart(10, '0') + ' 00000 n \n')
      .join('') +
    'trailer\n<< /Size ' +
    (objects.length + 1) +
    ' /Root 1 0 R >>\nstartxref\n' +
    start +
    '\n%%EOF';
  return Buffer.from(result);
}
test('PDF02 recorded source-to-expense walkthrough', async ({ browser }, info) => {
  test.setTimeout(360000);
  const clock = Date.now(),
    tag = info.project.name;
  const admin = await request.newContext({ baseURL: 'http://127.0.0.1:5080' });
  let sign = await login(admin, 'ci-admin', original);
  if (sign.status() === 401) sign = await login(admin, 'ci-admin', changed);
  expect(sign.ok()).toBeTruthy();
  if ((await sign.json()).changePassword)
    await mutate(admin, '/auth/change-password', { current: original, new: changed });
  const create = async (name: string, roles: string[], managerId: string | null = null) =>
    mutate(admin, '/admin/users', {
      username: name,
      name,
      email: name + '@example.invalid',
      worker: 'DEMO-' + tag,
      managerId,
      active: true,
      roles,
      password: temporary,
      reason: 'Synthetic OCR demo account',
    });
  const manager = await create('ocr-manager-' + tag, ['Manager']);
  await create('ocr-employee-' + tag, ['Employee'], manager.id);
  const master = async (kind: string, code: string, json: any) =>
    mutate(admin, '/configuration/masters', {
      kind,
      code,
      nameRo: code,
      nameEn: code,
      json: JSON.stringify(json),
      from: '2020-01-01',
      to: null,
      active: true,
      reason: 'Synthetic OCR demo data',
    });
  const category = 'DEMO-FUEL-' + tag,
    cc = 'DEMO-CC-' + tag;
  await master('expenseTypes', category, { ocrKeywords: ['motorina'] });
  await master('costCenters', cc, { erpCode: cc });
  await master('accounting', category, { mainAccount: 'DEMO-GL', localAccount: 'DEMO-LOCAL' });
  const configured = await (await admin.get('/api/configuration/masters')).json();
  for (const rate of [0, 11, 21])
    if (!configured.some((m: any) => m.kind === 'vat' && JSON.parse(m.json).rate === rate))
      await master('vat', 'DEMO-' + rate, {
        rate,
        salesTaxGroup: 'TEST',
        itemTaxGroup: 'T' + rate,
      });
  const employee = await request.newContext({ baseURL: 'http://127.0.0.1:5080' });
  expect((await login(employee, 'ocr-employee-' + tag, temporary)).ok()).toBeTruthy();
  await mutate(employee, '/auth/change-password', { current: temporary, new: changed });
  const report = await mutate(employee, '/reports', {
    purpose: 'PDF 02 — verificare documente ' + tag,
  });
  await mutate(
    employee,
    '/reports/' + report.id,
    { version: report.version, purpose: report.purpose, payment: 'Personal', cardId: null, costCenter: cc },
    'PATCH',
  );
  const context = await browser.newContext({
    storageState: await employee.storageState(),
    viewport: tag === 'mobile' ? { width: 390, height: 844 } : { width: 1440, height: 1000 },
    recordVideo: {
      dir: info.outputPath('demo-video'),
      size: tag === 'mobile' ? { width: 390, height: 844 } : { width: 1440, height: 1000 },
    },
  });
  const page = await context.newPage();
  page.on('dialog', (dialog) => dialog.accept());
  await page.goto('/');
  await page.getByRole('button', { name: report.purpose }).click();
  const invoice = (id: string, total = '10.00') => [
    'TEST VENDOR',
    'INVOICE NO ' + id + '-' + tag,
    '15.10.2026',
    'TOTAL ' + total + ' RON',
  ];
  const batch = [
    [
      'TEST VENDOR',
      'INVOICE NO FIRST-' + tag,
      'CUI 18546294',
      '15.10.2026',
      'DESCRIPTION: motorina',
      'TVA 21% 100.00 21.00',
      'TOTAL 121.00 RON',
    ],
    invoice('SECOND'),
    ['TEST VENDOR', 'INVOICE NO MULTI-' + tag, '15.10.2026', 'Page 1 of 3'],
    ['Page 2 of 3', 'Continuation itemization'],
    ['Page 3 of 3', 'TOTAL 30.00 RON'],
    invoice('SIX'),
    [
      'BON FISCAL NR TOP-' + tag,
      '15.10.2026',
      'TOTAL 10.00 RON',
      '',
      '',
      '',
      'BON FISCAL NR BOTTOM-' + tag,
      '15.10.2026',
      'TOTAL 20.00 RON',
    ],
    invoice('EIGHT'),
    invoice('NINE'),
    invoice('TEN'),
    ['COVER SHEET', 'Non-expense supporting paper'],
  ];
  await page.getByRole('radio', { name: /Mai multe bonuri/ }).check();
  await page
    .getByLabel('Adaugă PDF sau fotografie', { exact: true })
    .setInputFiles({
      name: 'synthetic-batch-' + tag + '.pdf',
      mimeType: 'application/pdf',
      buffer: pdf(batch),
    });
  const board = page.getByRole('region', { name: 'Verificarea documentelor', exact: true });
  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible({ timeout: 180000 });
  await dialog.getByRole('button', { name: 'Anulează', exact: true }).click();
  await expect(dialog).not.toBeVisible();
  const picker = board.getByRole('group', { name: 'Selectează bonul pentru verificare' });
  await expect(picker.getByRole('button')).toHaveCount(9, { timeout: 180000 });
  const detail = async () =>
    (await (await employee.get('/api/reports/' + report.id)).json()).report;
  const source = (await detail()).attachments[0];
  const review = async () =>
    (await (await employee.get(`/api/reports/${report.id}/documents/${source.id}/review`)).json())
      .review;
  expect((await review()).unassignedPages).toEqual([11]);
  const screenshot = async (name: string) =>
    page.screenshot({ path: info.outputPath(name + '.png'), fullPage: true });
  await screenshot('01-segmentation-board');
  const reason = board.getByLabel('Motivul modificării grupării', { exact: true });
  await reason.fill('Synthetic cover page verified');
  await board.getByRole('button', { name: 'Marchează justificativă', exact: true }).click();
  await expect.poll(async () => (await review()).unassignedPages.length).toBe(0);
  const select = async (predicate: (g: any) => boolean) => {
    const state = await review();
    await expect(board.locator('fieldset.ocr-review-controls')).toBeEnabled();
    await expect(picker.getByRole('button')).toHaveCount(state.candidates.length);
    for (let n = 0; n < state.candidates.length; n++)
      if (predicate(state.candidates[n])) {
        await picker.getByRole('button').nth(n).click();
        await board
          .getByRole('checkbox', { name: 'Selectează documentul ' + (n + 1), exact: true })
          .check();
      }
  };
  await reason.fill('Demonstrate invoice split and merge');
  await select((g) => g.pages.join(',') === '3,4,5');
  await board.getByLabel('Pagini pentru separare', { exact: true }).fill('3');
  await board.getByRole('button', { name: 'Separă paginile', exact: true }).click();
  await expect.poll(async () => (await review()).candidates.length).toBe(10);
  await select((g) => g.pages.some((n: number) => n >= 3 && n <= 5));
  await board.getByRole('button', { name: 'Unește documentele selectate', exact: true }).click();
  await expect.poll(async () => (await review()).candidates.length).toBe(9);
  await reason.fill('Correct two mistakenly merged receipts');
  await select((g) => g.pages.length === 1 && [1, 2].includes(g.pages[0]));
  await board.getByRole('button', { name: 'Unește documentele selectate', exact: true }).click();
  await expect.poll(async () => (await review()).candidates.length).toBe(8);
  await select((g) => g.pages.join(',') === '1,2');
  await board.getByLabel('Pagini pentru separare', { exact: true }).fill('2');
  await board.getByRole('button', { name: 'Separă paginile', exact: true }).click();
  await expect.poll(async () => (await review()).candidates.length).toBe(9);
  await screenshot('02-regrouped-source-lineage');
  await board
    .locator('.ocr-fields')
    .first()
    .getByRole('button', { name: 'Arată sursa câmpului' })
    .first()
    .click();
  await expect(board.getByRole('img').first()).toBeVisible();
  await screenshot('03-field-source-highlight');
  const sourceGroups = await review();
  await picker.getByRole('button').nth(sourceGroups.candidates.findIndex((g: any) => g.pages.includes(1))).click();
  await board.getByRole('button', { name: 'Revizuiește și creează bonul' }).click();
  await dialog
    .getByLabel('Bonul are înscris CUI-ul companiei?', { exact: true })
    .selectOption('yes');
  await dialog.getByLabel('CUI furnizor', { exact: true }).fill('18546294');
  const confirmation = dialog.getByRole('checkbox', { name: /Am verificat CUI-ul/ });
  await expect
    .poll(
      async () =>
        (await confirmation.isVisible()) ||
        (await dialog.getByLabel('Denumire furnizor', { exact: true }).getAttribute('readonly')) !==
          null,
    )
    .toBeTruthy();
  if (await confirmation.isVisible()) {
    await dialog.getByLabel('Denumire furnizor', { exact: true }).fill('Synthetic vendor');
    await confirmation.check();
  }
  await dialog.getByLabel('Tip cheltuială', { exact: true }).selectOption(category);
  await dialog
    .getByLabel('Descriere / justificare', { exact: true })
    .fill('Synthetic human correction demonstration');
  await dialog.getByLabel('Total bon în moneda documentului', { exact: true }).fill('12.10');
  await dialog.getByLabel('Bază 21%', { exact: true }).fill('10');
  await dialog.getByLabel('TVA 21%', { exact: true }).fill('2.10');
  await screenshot('04-vat-correction');
  await dialog
    .getByRole('button', { name: 'Salvează ciorna pentru verificare', exact: true })
    .click();
  await expect(dialog).not.toBeVisible();
  await board.getByRole('button', { name: 'Revizuiește și creează bonul' }).first().click();
  await expect(dialog.getByLabel('Total bon în moneda documentului', { exact: true })).toHaveValue(
    '12.10',
  );
  await dialog.getByRole('checkbox', { name: /Am verificat gruparea paginilor/ }).check();
  await dialog.getByRole('button', { name: 'Salvează bonul', exact: true }).click();
  await expect(dialog).not.toBeVisible();
  for (let step = 1; step < 9; step++) {
    const state = await review();
    const index = state.candidates.findIndex((g: any) => g.state !== 'Verified');
    await picker.getByRole('button').nth(index).click();
    await board.getByRole('button', { name: 'Revizuiește și creează bonul' }).click();
    await dialog.getByLabel('Furnizorul este din România?', { exact: true }).selectOption('no');
    await dialog.getByLabel('Tip cheltuială', { exact: true }).selectOption(category);
      await dialog
      .getByLabel('Descriere / justificare', { exact: true })
      .fill('Verified synthetic source group ' + step);
    await dialog.getByRole('checkbox', { name: /Am verificat gruparea paginilor/ }).check();
    await dialog.getByRole('button', { name: 'Salvează bonul', exact: true }).click();
    await expect(dialog).not.toBeVisible();
  }
  const verified = await detail();
  expect(verified.lines).toHaveLength(9);
  const ids = verified.lines.map((l: any) => l.id).sort();
  await board.getByRole('button', { name: 'Istoric valori corectate', exact: true }).click();
  const corrections = await (
    await employee.get(`/api/reports/${report.id}/documents/${source.id}/feedback`)
  ).json();
  expect(
    corrections.some((entry: any) =>
      entry.fields.some(
        (f: any) => f.field === 'vat.21.vat' && Number(f.raw) === 21 && Number(f.verified) === 2.1,
      ),
    ),
  ).toBeTruthy();
  await screenshot('05-verified-values-and-history');
  await page.getByRole('button', { name: 'Extrage / reîncearcă OCR', exact: true }).click();
  await expect(
    page.getByRole('button', { name: 'Extrage / reîncearcă OCR', exact: true }),
  ).toBeEnabled({ timeout: 180000 });
  expect((await detail()).lines.map((l: any) => l.id).sort()).toEqual(ids);
  expect((await review()).candidates).toHaveLength(9);
  await screenshot('06-retry-stable-expense-ids');
  expect((await review()).unassignedPages).toEqual([]);
  await expect(
    page.getByRole('button', { name: 'Trimite la aprobare', exact: true }),
  ).toBeEnabled();
  await info.attach('pdf02-measurement', {
    body: JSON.stringify({
      synthetic: true,
      pages: 11,
      groups: 9,
      verified: 9,
      unassigned: 0,
      retryPreservedIds: true,
      elapsedMilliseconds: Date.now() - clock,
    }),
    contentType: 'application/json',
  });
  await context.close();
  await employee.dispose();
  await admin.dispose();
});
