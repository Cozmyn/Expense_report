import { test, expect, request, APIRequestContext } from '@playwright/test';
const initial = 'Valid-test-Password1!',
  changed = 'Changed-test-Password2!';
async function csrf(c: APIRequestContext) {
  const r = await c.get('/api/auth/csrf');
  expect(r.ok()).toBeTruthy();
  return (await r.json()).token;
}
async function mutation(c: APIRequestContext, path: string, data: any, method = 'POST') {
  const r = await c.fetch(path, {
    method,
    data,
    headers: { 'X-CSRF-TOKEN': await csrf(c), 'Idempotency-Key': crypto.randomUUID() },
  });
  expect(r.ok(), await r.text()).toBeTruthy();
  return r.status() === 204 ? null : r.json();
}
async function postLogin(c: APIRequestContext, username: string, password: string) {
  const send = async () =>
    c.post('/api/auth/login', {
      data: { username, password },
      headers: { 'X-CSRF-TOKEN': await csrf(c) },
    });
  let r = await send();
  if (r.status() === 429) {
    await new Promise((resolve) => setTimeout(resolve, 60000));
    r = await send();
  }
  return r;
}
async function login(c: APIRequestContext, username: string, password: string) {
  const r = await postLogin(c, username, password);
  expect(r.ok(), await r.text()).toBeTruthy();
  return r.json();
}
test('Three receipt branches, mixed VAT and evidence enforcement through ERP', async ({
  page,
  browser,
}, info) => {
  const shot = async (p: any, name: string) => {
    await p.evaluate(() => window.scrollTo(0, 0));
    if (info.project.name === 'chromium')
      await p.screenshot({ path: 'test-results/frontend-' + name + '.png', fullPage: true });
  };
  page.on('response', async (r) => {
    if (r.url().includes('/api/') && r.status() >= 400)
      console.log('API failure', r.url(), r.status(), await r.text());
  });
  const suffix = info.project.name;
  const admin = await request.newContext({ baseURL: 'http://127.0.0.1:5080' });
  const password = process.env.E2E_ADMIN_PASSWORD!;
  let attempt = await postLogin(admin, 'ci-admin', suffix === 'chromium' ? password : changed);
  if (attempt.status() === 401) attempt = await postLogin(admin, 'ci-admin', changed);
  expect(attempt.ok(), await attempt.text()).toBeTruthy();
  const auth = await attempt.json();
  if (auth.changePassword)
    await mutation(admin, '/api/auth/change-password', { current: password, new: changed });
  const create = async (username: string, roles: string[], managerId: string | null = null) =>
    mutation(admin, '/api/admin/users', {
      username,
      name: username,
      email: username + '@example.invalid',
      worker: 'TEST-WORKER',
      managerId,
      active: true,
      roles,
      password: initial,
      reason: 'CI isolated test setup',
    });
  const manager = await create('manager-' + suffix, ['Manager']);
  const employeeUser = await create('employee-' + suffix, ['Employee'], manager.id);
  await create('finance-' + suffix, ['Finance']);
  const master = async (kind: string, code: string, data: any) =>
    mutation(admin, '/api/configuration/masters', {
      kind,
      code,
      nameRo: code,
      nameEn: code,
      json: JSON.stringify(data),
      from: '2020-01-01',
      to: null,
      active: true,
      reason: 'CI synthetic mappings',
    });
  const cat = 'GENERAL-' + suffix,
    cc = 'CC-' + suffix;
  await master('expenseTypes', cat, {});
  await master('costCenters', cc, { erpCode: cc });
  await mutation(admin, '/api/admin/cost-center-owners/' + encodeURIComponent(cc), {
    costCenterCode: cc, managerId: manager.id, reason: 'CI manager ownership',
  }, 'PUT');
  await mutation(admin, '/api/admin/users/' + employeeUser.id, {
    username: 'employee-' + suffix,
    name: 'employee-' + suffix,
    email: 'employee-' + suffix + '@example.invalid',
    worker: 'TEST-WORKER',
    managerId: manager.id,
    costCenter: cc,
    active: true,
    roles: ['Employee'],
    password: null,
    reason: 'CI cost centre allocation',
  }, 'PUT');
  await master('accounting', cat, { mainAccount: 'TEST-GL', localAccount: 'TEST-LOCAL' });
  let settings = (await (await admin.get('/api/configuration/masters')).json()).filter(
    (m: any) => m.kind === 'erpSettings',
  );
  if (!settings.length)
    await master('erpSettings', 'default', {
      approvalReference: 'CI TEST ONLY',
      clearingAccount: 'TEST-CLEARING',
      clearingLocal: 'TEST-CLEARING-LOCAL',
      vatAccount: 'TEST-VAT',
      vatLocal: 'TEST-VAT-LOCAL',
      otherCarNonDedAccount: 'TEST-CAR',
      repairNonDedAccount: 'TEST-REPAIR',
      repairNonDedLocal: 'TEST-REPAIR-LOCAL',
    });
  // Expense entry uses Finance-owned effective tax mapping; avoid creating
  // duplicate mappings that would make the rate ambiguous for employees.
  const existingVat = await (await admin.get('/api/configuration/masters')).json();
  for (const rate of [0, 11, 21])
    if (!existingVat.some((m: any) =>
      m.kind === 'vat' && m.active && JSON.parse(m.json).rate === rate))
      await master('vat', rate + '-' + suffix, {
        rate,
        salesTaxGroup: 'VDOM',
        itemTaxGroup: 'G' + rate,
      });
  const allMasters = await (await admin.get('/api/configuration/masters')).json();
  if (!allMasters.some((m: any) => m.kind === 'fx' && m.code === 'EUR'))
    await (async () => {
      await create('fx-finance-' + suffix, ['Finance']);
      const fxFinance = await request.newContext({ baseURL: 'http://127.0.0.1:5080' });
      await login(fxFinance, 'fx-finance-' + suffix, initial);
      await mutation(fxFinance, '/api/auth/change-password', {
        current: initial,
        new: initial + 'New',
      });
      await mutation(fxFinance, '/api/configuration/masters', {
        kind: 'fx',
        code: 'EUR',
        nameRo: 'EUR',
        nameEn: 'EUR',
        json: JSON.stringify({ ronRate: 5 }),
        from: '2020-01-01',
        to: null,
        active: true,
        reason: 'Finance estimated FX setup',
      });
      await fxFinance.dispose();
    })();
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Autentificare' })).toBeVisible();
  await shot(page, '01-login');
  await page.getByRole('button', { name: 'English', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible();
  await page.getByRole('button', { name: 'Română', exact: true }).click();
  await page.getByLabel('Utilizator', { exact: true }).fill('employee-' + suffix);
  await page.getByLabel('Parolă', { exact: true }).fill(initial);
  await page.getByRole('button', { name: 'Autentificare', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Alege o parolă nouă' })).toBeVisible();
  await page.getByLabel('Parola temporară').fill(initial);
  await page.getByLabel('Parola nouă', { exact: true }).fill(changed);
  await page.getByRole('button', { name: 'Salvează și continuă' }).click();
  await expect(
    page.getByRole('heading', { name: 'Privire de ansamblu', exact: true }),
  ).toBeVisible();
  await shot(page, '02-dashboard');
  const ocrHelp = page.locator('.report-instructions .quick-help').filter({
    hasText: 'Cum citește aplicația un bon?',
  });
  await expect(ocrHelp).toContainText('OCR poate greși sau poate rata câmpuri.');
  await page.getByRole('button', { name: 'RO', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Overview', exact: true })).toBeVisible();
  await expect(page.getByText('How does receipt scanning work?', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'EN', exact: true }).click();
  await page.getByRole('button', { name: 'Decont nou', exact: true }).click();
  await page.getByLabel('Scopul decontului').fill('Client visit ' + suffix);
  await page.getByLabel('Centru de cost al decontului').selectOption(cc);
  await page.getByRole('button', { name: 'Salvează datele generale' }).click();
  await expect(
    page.getByText('Cum citește aplicația un bon?', { exact: true }),
  ).toBeVisible();
  await expect(page.getByLabel('Nume angajat', { exact: true })).toHaveValue('employee-' + suffix);
  await expect(page.getByLabel('LantID', { exact: true })).toHaveValue('TEST-WORKER');
  await expect(
    page.getByRole('heading', { name: 'Client visit ' + suffix, exact: true }),
  ).toBeVisible();
  const dialog = page.getByRole('dialog');
  const receipt = Buffer.from(
    'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a16kAAAAASUVORK5CYII=',
    'base64',
  );
  const begin = async () => {
    await page.getByRole('button', { name: 'Adaugă bon / document', exact: true }).click();
    await expect(dialog.getByRole('heading', { name: 'Adaugă un bon / document' })).toBeFocused();
    await expect(dialog.getByLabel('Metoda de plată', { exact: true })).toHaveCount(0);
    await dialog.getByLabel('Tip cheltuială', { exact: true }).selectOption(cat);
  };
  const save = async () => {
    const confirmed = dialog.getByRole('checkbox', { name: /Am verificat gruparea paginilor/ });
    if (await confirmed.count()) await confirmed.check();
    await dialog.getByRole('button', { name: 'Salvează bonul', exact: true }).click();
    await expect(dialog).not.toBeVisible();
  };
  await begin();
  const dismissedDialogs: string[] = [];
  page.once('dialog', async (prompt) => {
    dismissedDialogs.push(prompt.message());
    await prompt.dismiss();
  });
  await page.keyboard.press('Escape');
  await expect(dialog).toBeVisible();
  expect(dismissedDialogs[0]).toContain('Închizi fără să salvezi bonul?');
  await expect(dialog.getByRole('heading', { name: 'Adaugă un bon / document' })).toBeVisible();
  await dialog
    .getByLabel('Bonul are înscris CUI-ul companiei?', { exact: true })
    .selectOption('yes');
  await dialog.getByLabel('CUI furnizor', { exact: true }).fill('RO18546294');
  await dialog.getByLabel('Număr bon / document', { exact: true }).fill('B-' + suffix);
  if (suffix === 'chromium') {
    const confirm = dialog.getByRole('checkbox', { name: /Am verificat CUI-ul/ });
    await expect(confirm).toBeVisible();
    await dialog.getByLabel('Denumire furnizor', { exact: true }).fill('Synthetic CI supplier');
    await expect(dialog.getByRole('button', { name: 'Salvează bonul' })).toBeDisabled();
    await confirm.check();
  } else {
    await expect(dialog.getByLabel('Denumire furnizor', { exact: true })).toHaveValue(
      'Synthetic CI supplier',
    );
    await expect(dialog.getByLabel('Denumire furnizor', { exact: true })).toHaveAttribute(
      'readonly',
      '',
    );
  }
  await dialog
    .getByLabel('Descriere / justificare', { exact: true })
    .fill('Mixed-rate synthetic receipt');
  await dialog.getByLabel('Total bon în moneda documentului', { exact: true }).fill('335');
  for (const rate of [0, 11, 21])
    await dialog.getByLabel('Bază ' + rate + '%', { exact: true }).fill('100');
  await dialog.getByLabel('TVA 11%', { exact: true }).fill('11');
  await dialog.getByLabel('TVA 21%', { exact: true }).fill('21');
  await dialog.getByLabel('Bază 0%', { exact: true }).clear();
  await expect(dialog.getByLabel('Bază 0%', { exact: true })).toHaveValue('');
  await expect(dialog.getByRole('button', { name: 'Salvează bonul' })).toBeDisabled();
  await dialog.getByLabel('Bază 0%', { exact: true }).fill('100');
  await expect(dialog.getByRole('button', { name: 'Salvează bonul' })).toBeDisabled();
  await dialog.getByLabel('Total bon în moneda documentului', { exact: true }).fill('332');
  await expect(dialog.getByText(/Încarcă documentul din secțiunea unică/)).toBeVisible();
  await shot(page, '03-receipt-vat-editor');
  await save();
  await begin();
  await dialog
    .getByLabel('Bonul are înscris CUI-ul companiei?', { exact: true })
    .selectOption('no');
  await expect(dialog.getByLabel('CUI furnizor', { exact: true })).toHaveCount(0);
  await expect(dialog.getByLabel('Bază 11%', { exact: true })).toHaveCount(0);
  await dialog
    .getByLabel('Descriere / justificare', { exact: true })
    .fill('Romanian receipt without company CUI');
  await dialog.getByLabel('Total bon în moneda documentului', { exact: true }).fill('100');
  await shot(page, '03b-receipt-without-cui');
  await save();
  await begin();
  await dialog.getByLabel('Furnizorul este din România?', { exact: true }).selectOption('no');
  await expect(
    dialog.getByLabel('Bonul are înscris CUI-ul companiei?', { exact: true }),
  ).toHaveCount(0);
  await expect(dialog.getByLabel('CUI furnizor', { exact: true })).toHaveCount(0);
  await dialog
    .getByLabel('Descriere / justificare', { exact: true })
    .fill('Foreign receipt in EUR');
  await dialog.getByLabel('Moneda bonului', { exact: true }).fill('EUR');
  await dialog.getByLabel('Total bon în moneda documentului', { exact: true }).fill('10');
  await shot(page, '03c-foreign-receipt');
  await save();
  await expect(page.getByRole('button', { name: 'Trimite la aprobare' })).toBeDisabled();
  // The new employee UI uses a single OCR uploader. Keep historical combined
  // evidence compatibility under API test; other browser tests cover new UI intake.
  const evidenceOwner = await request.newContext({ baseURL: 'http://127.0.0.1:5080' });
  await login(evidenceOwner, 'employee-' + suffix, changed);
  const existingReports = await (await evidenceOwner.get('/api/reports')).json();
  const evidenceReport = existingReports.find((r: any) => r.purpose === 'Client visit ' + suffix);
  const versionedReport = (await (await evidenceOwner.get('/api/reports/' + evidenceReport.id)).json()).report;
  const sharedFile = await evidenceOwner.post('/api/reports/' + evidenceReport.id + '/attachments', {
    multipart: {
      version: String(versionedReport.version),
      coversAll: 'true',
      reason: 'Legacy combined evidence coverage',
      file: { name: 'combined-' + suffix + '.png', mimeType: 'image/png', buffer: receipt },
    },
    headers: { 'X-CSRF-TOKEN': await csrf(evidenceOwner), 'Idempotency-Key': crypto.randomUUID() },
  });
  expect(sharedFile.ok(), await sharedFile.text()).toBeTruthy();
  await page.reload();
  await expect(page.getByRole('button', { name: 'Trimite la aprobare' })).toBeEnabled({
    timeout: 90000,
  });
  await shot(page, '04-report');
  await page.getByRole('button', { name: 'Trimite la aprobare' }).click();
  await expect(page.getByText('La manager', { exact: true }).first()).toBeVisible();
  const employee = await request.newContext({ baseURL: 'http://127.0.0.1:5080' });
  await login(employee, 'employee-' + suffix, changed);
  const reports = await (await employee.get('/api/reports')).json();
  const report = reports[0];
  const saved = (await (await employee.get('/api/reports/' + report.id)).json()).report;
  expect(saved.lines).toHaveLength(3);
  expect(saved.lines[0].data.taxes).toHaveLength(3);
  expect(saved.lines[0].data.hasCompanyCui).toBe(true);
  expect(saved.lines[1].data.taxes).toHaveLength(0);
  expect(saved.lines[1].data.hasCompanyCui).toBe(false);
  expect(saved.lines[2].data.country).toBe('ZZ');
  expect(saved.lines[2].estimatedRon).toBe(50);
  expect(saved.attachments).toHaveLength(1);
  const managerClient = await request.newContext({ baseURL: 'http://127.0.0.1:5080' });
  await login(managerClient, 'manager-' + suffix, initial);
  await mutation(managerClient, '/api/auth/change-password', { current: initial, new: changed });
  let detail = (await (await managerClient.get('/api/reports/' + report.id)).json()).report;
  if (suffix === 'chromium') {
    const ctx = await browser.newContext({ storageState: await managerClient.storageState() });
    const mp = await ctx.newPage();
    await mp.goto('/');
    await mp.getByRole('button', { name: 'Aprobări', exact: true }).click();
    await mp.getByRole('button', { name: report.purpose }).click();
    await expect(mp.getByRole('button', { name: 'Aprobă revizia', exact: true })).toBeVisible();
    await shot(mp, '05-manager-review');
    await ctx.close();
  }
  await mutation(managerClient, '/api/reports/' + report.id + '/actions/approve', {
    version: detail.version,
  });
  const fin = await request.newContext({ baseURL: 'http://127.0.0.1:5080' });
  await login(fin, 'finance-' + suffix, initial);
  await mutation(fin, '/api/auth/change-password', { current: initial, new: changed });
  const vendors = await (await fin.get('/api/finance/vendors')).json();
  for (const v of vendors.filter((v: any) => v.reportId === report.id && v.state === 'Pending'))
    await mutation(fin, '/api/finance/vendors/' + v.id + '/resolve', {
      account: 'TEST-VENDOR',
      from: '2020-01-01',
      reason: 'CI confirmed supplier registration',
    });
  let review = await (await fin.get('/api/reports/' + report.id)).json();
  detail = review.report;
  for (const issue of review.issues.filter((i: any) => i.code === 'duplicate_exact'))
    detail = await mutation(
      fin,
      `/api/finance/reports/${report.id}/lines/${issue.lineId}/disposition`,
      {
        version: detail.version,
        kind: 'duplicateAllowed',
        reason: 'CI intentionally reuses a synthetic receipt; approved test exception',
      },
    );
  const financeContext = await browser.newContext({ storageState: await fin.storageState() });
  const financePage = await financeContext.newPage();
  await financePage.goto('/');
  await expect(
    financePage.getByRole('heading', { name: 'Privire de ansamblu', exact: true }),
  ).toBeVisible();
  const financeMenu = financePage.getByRole('button', { name: 'Deschide meniul', exact: true });
  if (await financeMenu.isVisible()) await financeMenu.click();
  await financePage.getByRole('button', { name: 'Finance', exact: true }).click();
  await financePage.getByRole('button', { name: report.purpose }).click();
  await financePage.getByRole('button', { name: 'Calcul și FX', exact: true }).click();
  await expect(
    financePage.getByRole('heading', { name: 'Calcul și curs valutar', exact: true }),
  ).toBeVisible();
  await financePage.getByRole('button', { name: 'RO', exact: true }).click();
  await expect(
    financePage.getByRole('heading', { name: 'Calculations & exchange rates', exact: true }),
  ).toBeVisible();
  await financePage.getByRole('button', { name: 'EN', exact: true }).click();
  if (suffix === 'mobile') await financePage.setViewportSize({ width: 390, height: 844 });
  const fxRow = financePage
    .locator('article.expense')
    .filter({
      has: financePage.getByRole('button', { name: 'Confirmă estimarea RON', exact: true }),
    });
  await fxRow
    .getByLabel('Motiv verificare', { exact: true })
    .fill('Finance verified original and final FX');
  await fxRow.getByRole('button', { name: 'Confirmă estimarea RON', exact: true }).click();
  await expect(fxRow.locator('h4').filter({ hasText: 'Istoric calcul și FX' })).toContainText(
    '(1)',
  );
  await fxRow.getByLabel('Curs propus', { exact: true }).fill('5.1');
  await fxRow.getByRole('button', { name: 'Propune ajustarea', exact: true }).click();
  await expect(
    fxRow.getByRole('button', { name: 'Aplică valoarea Finance', exact: true }).first(),
  ).toBeEnabled();
  await fxRow.getByRole('button', { name: 'Aplică valoarea Finance', exact: true }).first().click();
  await expect(fxRow.getByText('51,00 RON', { exact: true })).toBeVisible();
  await fxRow.getByRole('button', { name: 'Confirmă baza și totalul', exact: true }).click();
  await expect(fxRow.locator('h4').filter({ hasText: 'Istoric calcul și FX' })).toContainText(
    '(3)',
  );
  await expect
    .poll(() => financePage.evaluate(() => document.documentElement.scrollWidth))
    .toBeLessThanOrEqual(financePage.viewportSize()!.width);
  await shot(financePage, '07b-finance-calculations-' + suffix);
  await financeContext.close();
  detail = (await (await fin.get('/api/reports/' + report.id)).json()).report;
  expect(detail.revision).toBe(saved.revision);
  expect(detail.state).toBe('FINANCE_REVIEW');
  expect(detail.lines.find((l: any) => l.data.currency === 'EUR').estimatedRon).toBe(50);
  expect(detail.lines.find((l: any) => l.data.currency === 'EUR').financeRon).toBe(51);
  await mutation(fin, '/api/reports/' + report.id + '/actions/complete', {
    version: detail.version,
  });
  detail = (await (await fin.get('/api/reports/' + report.id)).json()).report;
  const exported = await mutation(fin, '/api/finance/reports/' + report.id + '/export', {
    version: detail.version,
  });
  if (suffix === 'chromium') {
    const ctx = await browser.newContext({ storageState: await fin.storageState() });
    const fp = await ctx.newPage();
    await fp.goto('/');
    await fp.getByRole('button', { name: 'Finance', exact: true }).click();
    await expect(fp.getByRole('heading', { name: 'Verificare financiară' })).toBeVisible();
    await shot(fp, '06-finance-queue');
    await fp.getByRole('button', { name: report.purpose }).click();
    await expect(fp.getByRole('heading', { name: report.purpose, exact: true })).toBeVisible();
    await shot(fp, '07-finance-report');
    await fp.getByRole('button', { name: 'ERP', exact: true }).click();
    await fp.getByText('Fișierul Import — toate cele 24 de coloane', { exact: true }).click();
    await expect(fp.getByRole('cell', { name: 'TEST-GL', exact: true }).first()).toBeVisible();
    await shot(fp, '08-erp-preview');
    await fp.getByRole('button', { name: 'Carduri și tranzacții', exact: true }).click();
    await expect(
      fp.getByRole('heading', { name: 'Carduri și tranzacții', exact: true }),
    ).toBeVisible();
    await shot(fp, '09-cards');
    await ctx.close();
  }
  const replay = await mutation(fin, '/api/finance/reports/' + report.id + '/export', {
    version: detail.version,
  });
  expect(replay.batchId).toBe(exported.batchId);
  const postingContext = await browser.newContext({ storageState: await fin.storageState() });
  const pp = await postingContext.newPage();
  await pp.goto('/');
  await expect(pp.getByRole('heading', { name: 'Privire de ansamblu', exact: true })).toBeVisible();
  const postingNav = async (label: string) => {
    const menu = pp.getByRole('button', { name: 'Deschide meniul', exact: true });
    if (await menu.isVisible()) await menu.click();
    await pp.getByRole('button', { name: label, exact: true }).click();
  };
  await postingNav('Situație contabilă');
  await expect(pp.getByRole('heading', { name: 'Situație contabilă', exact: true })).toBeVisible();
  const batchRow = pp
    .getByRole('row')
    .filter({ has: pp.getByRole('button', { name: report.number, exact: true }) });
  await batchRow.getByRole('button', { name: 'Vezi rândurile salvate', exact: true }).click();
  await expect(
    pp.getByRole('heading', { name: 'Rânduri contabile salvate', exact: true }),
  ).toBeVisible();
  await pp.screenshot({ path: info.outputPath('pdf04-accounting-overview.png'), fullPage: true });
  await batchRow.getByRole('button', { name: report.number, exact: true }).click();
  await pp.getByRole('button', { name: 'ERP', exact: true }).click();
  await expect(
    pp.getByText(
      'Export salvat: se afișează valorile istorice ale fișierului, nu mapările curente.',
      { exact: true },
    ),
  ).toBeVisible();
  const trace = pp.locator('.posting-lines article').first();
  await trace.getByText('Sursă și mapare', { exact: true }).click();
  await trace.getByRole('button', { name: 'Vezi documentul sursă', exact: true }).click();
  await expect(
    pp.getByRole('region', { name: 'Previzualizare document', exact: true }),
  ).toBeVisible();
  await pp.screenshot({ path: info.outputPath('pdf04-source-linked-posting.png'), fullPage: true });
  const overflow = await pp.evaluate(() =>
    Array.from(document.querySelectorAll('main *'))
      .map((el) => ({
        tag: el.tagName,
        classes: String(el.className),
        right: el.getBoundingClientRect().right,
        text: el.textContent?.slice(0, 70),
      }))
      .filter((el) => el.right > window.innerWidth + 1)
      .slice(0, 20),
  );
  if (overflow.length) console.log('Posting overflow diagnostics', JSON.stringify(overflow));
  await expect
    .poll(() => pp.evaluate(() => document.documentElement.scrollWidth))
    .toBeLessThanOrEqual(pp.viewportSize()!.width);
  await postingNav('Configurare Finance');
  const vehicle = pp
    .locator('section')
    .filter({ has: pp.locator('h4').filter({ hasText: 'Deductibilitate auto' }) })
    .first();
  await expect(vehicle.locator('h4').first()).toBeVisible();
  await expect(vehicle.getByLabel('Procent deductibil auto', { exact: true })).toHaveValue('50');
  await vehicle.getByLabel('Procent deductibil auto', { exact: true }).fill('75');
  await vehicle.getByLabel('Se aplică din', { exact: true }).fill('2027-02-01');
  await vehicle.getByLabel('Cod deductibil — TVA 21%', { exact: true }).fill('TEST-FUTURE-75');
  await vehicle.getByLabel('Cod nedeductibil — TVA 21%', { exact: true }).fill('TEST-FUTURE-25');
  await vehicle
    .getByLabel('Motiv schimbare procent', { exact: true })
    .fill('Synthetic future vehicle policy');
  await vehicle.getByRole('button', { name: 'Salvează procentul auto', exact: true }).click();
  await expect(vehicle.locator('.simple-row').first()).toContainText(/75(?:[,.]0+)?%/);
  await pp.screenshot({ path: info.outputPath('pdf04-vehicle-percentage.png'), fullPage: true });
  await postingContext.close();
  const accounting = await fin.get('/api/reports/' + report.id + '/accounting-details');
  expect(accounting.ok()).toBeTruthy();
  expect((await accounting.body()).subarray(0, 2).toString()).toBe('PK');
  const download = await fin.get('/api/batches/' + exported.batchId + '/xlsx');
  expect(download.ok()).toBeTruthy();
  expect((await download.body()).subarray(0, 2).toString()).toBe('PK');
  await mutation(fin, '/api/finance/batches/' + exported.batchId + '/posted', {
    version: exported.report.version,
    reference: 'CI-' + suffix,
  });
  const pdf = await employee.get('/api/reports/' + report.id + '/pdf');
  expect(pdf.ok(), await pdf.text()).toBeTruthy();
  expect((await pdf.body()).subarray(0, 5).toString()).toBe('%PDF-');
  const pack = await employee.get('/api/reports/' + report.id + '/pack');
  expect(pack.ok()).toBeTruthy();
  await page.reload();
  if (suffix === 'chromium') {
    await expect(page.getByRole('cell', { name: 'Postat', exact: true })).toBeVisible();
  } else {
    const reportCard = page.locator('.report-cards .report-card').filter({ hasText: report.purpose });
    await expect(reportCard).toBeVisible();
    await expect(reportCard.locator('.badge')).toHaveText('Postat');
    await expect(reportCard).toContainText('RON');
  }
  await expect
    .poll(() => page.evaluate(() => document.documentElement.scrollWidth))
    .toBeLessThanOrEqual(page.viewportSize()!.width);
  await shot(page, '02-dashboard');
  await page.screenshot({ path: 'test-results/' + suffix + '-report.png', fullPage: true });
  const ocrReport = await mutation(employee, '/api/reports', {
    purpose: 'OCR multipage ' + suffix,
  });
  await page.reload();
  await page.getByRole('button', { name: 'OCR multipage ' + suffix }).click();
  await page.getByLabel('Centru de cost al decontului').selectOption(cc);
  await page.getByRole('button', { name: 'Salvează datele generale' }).click();
  const pdfFixture = (() => {
    const objects = [
      '<< /Type /Catalog /Pages 2 0 R >>',
      '<< /Type /Pages /Kids [3 0 R 5 0 R] /Count 2 >>',
      '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 7 0 R >> >> /Contents 4 0 R',
      '',
      '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 7 0 R >> >> /Contents 6 0 R',
      '',
      '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>',
    ];
    for (const i of [0, 1]) {
      objects[2 + i * 2] += ' >>';
      const text = [
        'TEST VENDOR ' + (i + 1),
        'INVOICE NO OCR00' + (i + 1) + '-' + suffix,
        '01.01.2026',
        'TOTAL ' + (i + 1) * 10 + '.00 RON',
      ];
      const stream =
        'BT /F1 20 Tf 50 750 Td ' +
        text.map((v, n) => (n ? '0 -40 Td ' : '') + '(' + v + ') Tj').join(' ') +
        ' ET';
      objects[3 + i * 2] = '<< /Length ' + stream.length + ' >>\nstream\n' + stream + '\nendstream';
    }
    let pdf = '%PDF-1.4\n';
    const offsets = [0];
    objects.forEach((v, i) => {
      offsets.push(Buffer.byteLength(pdf));
      pdf += i + 1 + ' 0 obj\n' + v + '\nendobj\n';
    });
    const offset = Buffer.byteLength(pdf);
    pdf +=
      'xref\n0 ' +
      (objects.length + 1) +
      '\n0000000000 65535 f \n' +
      offsets
        .slice(1)
        .map((v) => String(v).padStart(10, '0') + ' 00000 n \n')
        .join('') +
      'trailer\n<< /Size ' +
      (objects.length + 1) +
      ' /Root 1 0 R >>\nstartxref\n' +
      offset +
      '\n%%EOF';
    return Buffer.from(pdf);
  })();
  await page.getByRole('radio', { name: /Mai multe bonuri/ }).check();
  await page
    .getByLabel('Adaugă PDF sau fotografie', { exact: true })
    .setInputFiles({
      name: 'two-invoices-' + suffix + '.pdf',
      mimeType: 'application/pdf',
      buffer: pdfFixture,
    });
  await expect(dialog).toBeVisible({ timeout: 90000 });
  await page.screenshot({ path: 'test-results/' + suffix + '-ocr-review.png', fullPage: true });
  for (let n = 0; n < 2; n++) {
    await expect(dialog).toBeVisible({ timeout: 90000 });
    await expect(
      dialog.getByLabel('Total bon în moneda documentului', { exact: true }),
    ).toHaveValue(String((n + 1) * 10) + '.00');
    await dialog.getByLabel('Furnizorul este din România?', { exact: true }).selectOption('no');
    await dialog.getByLabel('Tip cheltuială', { exact: true }).selectOption(cat);
    await dialog
      .getByLabel('Descriere / justificare', { exact: true })
      .fill('Confirmed OCR page ' + (n + 1));
    await save();
  }
  const captured = (await (await employee.get('/api/reports/' + ocrReport.id)).json()).report;
  expect(captured.lines).toHaveLength(2);
  const extraction = JSON.parse(captured.attachments[0].ocr);
  expect(extraction.pages).toHaveLength(2);
  expect(extraction.candidates).toHaveLength(2);
  expect(JSON.parse(captured.attachments[0].coveredLines)).toHaveLength(2);
  expect(captured.total).toBe(30);
  await expect(page.getByRole('button', { name: 'Trimite la aprobare' })).toBeEnabled();
  await page.getByRole('button', { name: 'Trimite la aprobare' }).click();
  await expect(page.getByText('La manager', { exact: true }).first()).toBeVisible();
  const scannedReport = await mutation(employee, '/api/reports', {
    purpose: 'OCR scanned ' + suffix,
  });
  await page.reload();
  await page.getByRole('button', { name: 'OCR scanned ' + suffix }).click();
  await page.getByLabel('Centru de cost al decontului').selectOption(cc);
  await page.getByRole('button', { name: 'Salvează datele generale' }).click();
  const scanData = await page.evaluate((tag) => {
    const canvas = document.createElement('canvas');
    canvas.width = 1200;
    canvas.height = 700;
    const ctx = canvas.getContext('2d')!;
    ctx.fillStyle = 'white';
    ctx.fillRect(0, 0, 1200, 700);
    ctx.fillStyle = 'black';
    ctx.font = 'bold 48px Arial';
    [
      'TEST VENDOR',
      'INVOICE NO SCAN-' + tag.toUpperCase(),
      '01.01.2026',
      'TOTAL 10.00 RON',
    ].forEach((line, n) => ctx.fillText(line, 80, 100 + n * 120));
    return canvas.toDataURL('image/png').split(',')[1];
  }, suffix);
  await page
    .getByLabel('Adaugă PDF sau fotografie', { exact: true })
    .setInputFiles({
      name: 'scan-' + suffix + '.png',
      mimeType: 'image/png',
      buffer: Buffer.from(scanData, 'base64'),
    });
  await expect(dialog).toBeVisible({ timeout: 90000 });
  const scanSaved = (await (await employee.get('/api/reports/' + scannedReport.id)).json()).report;
  const scanResult = JSON.parse(scanSaved.attachments[0].ocr);
  const scannedTotal = scanResult.candidates[0].fields.amount;
  expect(scannedTotal.value).toBe('10.00');
  expect(scannedTotal.source).toBe('ImageOcr');
  expect(scannedTotal.confidence).toBeGreaterThanOrEqual(0);
  expect(scannedTotal.confidence).toBeLessThanOrEqual(1);
  expect(scannedTotal.box.width).toBeGreaterThan(0);
  await page.screenshot({
    path: 'test-results/' + suffix + '-ocr-scan-geometry.png',
    fullPage: true,
  });
  // Exercise real OCR orientation and low-contrast intake, not a mocked provider.
  for (const mode of ['rotated', 'low-contrast']) {
    const extraReport = await mutation(employee, '/api/reports', {
      purpose: 'OCR ' + mode + ' ' + suffix,
    });
    await page.reload();
    await page.getByRole('button', { name: extraReport.purpose }).click();
    await page.getByLabel('Centru de cost al decontului').selectOption(cc);
  await page.getByRole('button', { name: 'Salvează datele generale' }).click();
    const data = await page.evaluate(
      ({ mode, tag }) => {
        const source = document.createElement('canvas');
        source.width = 1200;
        source.height = 900;
        const ctx = source.getContext('2d')!;
        ctx.fillStyle = 'white';
        ctx.fillRect(0, 0, 1200, 900);
        ctx.fillStyle = mode === 'low-contrast' ? '#aaaaaa' : 'black';
        ctx.font = 'bold 46px Arial';
        [
          'TEST VENDOR',
          'INVOICE NO ' + mode.toUpperCase() + '-' + tag.toUpperCase(),
          '15.10.2026',
          'DESCRIPTION: OFFICE SUPPLIES',
          'TOTAL 123.45 RON',
        ].forEach((line, n) => ctx.fillText(line, 60, 100 + n * 140));
        if (mode === 'rotated') {
          const rotated = document.createElement('canvas');
          rotated.width = 900;
          rotated.height = 1200;
          const rotate = rotated.getContext('2d')!;
          rotate.translate(0, 1200);
          rotate.rotate(-Math.PI / 2);
          rotate.drawImage(source, 0, 0);
          return rotated.toDataURL('image/png').split(',')[1];
        }
        return source.toDataURL('image/png').split(',')[1];
      },
      { mode, tag: suffix },
    );
    await page
      .getByLabel('Adaugă PDF sau fotografie', { exact: true })
      .setInputFiles({
        name: mode + '-' + suffix + '.png',
        mimeType: 'image/png',
        buffer: Buffer.from(data, 'base64'),
      });
    await expect(dialog).toBeVisible({ timeout: 90000 });
    const result = (await (await employee.get('/api/reports/' + extraReport.id)).json()).report;
    const extracted = JSON.parse(result.attachments[0].ocr);
    expect(extracted.candidates[0].fields.amount.value).toBe('123.45');
    expect(extracted.candidates[0].fields.amount.box.width).toBeGreaterThan(0);
    await page.screenshot({
      path: 'test-results/' + suffix + '-ocr-' + mode + '.png',
      fullPage: true,
    });
  }
  // Self-service profile and password change must work on desktop and mobile.
  await page.getByRole('button', { name: 'Profilul meu', exact: true }).last().click();
  await expect(page.getByRole('heading', { name: 'Profilul meu', exact: true })).toBeVisible();
  await page.getByLabel('Limba interfeței', { exact: true }).selectOption('en');
  await expect(page.getByRole('heading', { name: 'My profile', exact: true })).toBeVisible();
  await page.getByLabel('Interface language', { exact: true }).selectOption('ro');
  await page.getByLabel('Parola actuală', { exact: true }).fill(changed);
  await page.getByLabel('Parola nouă', { exact: true }).fill('Profile-updated-Password3!');
  await page.getByLabel('Confirmă parola nouă', { exact: true }).fill('Different-password-123!');
  await page.getByRole('button', { name: 'Schimbă parola', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('Parolele noi nu coincid');
  await page.getByLabel('Confirmă parola nouă', { exact: true }).fill('Profile-updated-Password3!');
  await page.getByRole('button', { name: 'Schimbă parola', exact: true }).click();
  await expect(
    page.getByText('Parola a fost schimbată. Celelalte sesiuni au fost închise.', { exact: true }),
  ).toBeVisible();
  expect((await employee.get('/api/me')).status()).toBe(401);
  await shot(page, '12-profile');
  await page.evaluate(() => window.dispatchEvent(new Event('decont:session-expired')));
  const expiredBanner = page.locator('.session-expired-banner');
  await expect(expiredBanner).toContainText('Sesiunea a expirat');
  await page.getByLabel('Limba interfeței', { exact: true }).selectOption('en');
  await expect(expiredBanner).toContainText('Your session has expired');
  await page.getByLabel('Interface language', { exact: true }).selectOption('ro');
  await expect(expiredBanner).toContainText('Sesiunea a expirat');
  page.once('dialog', (prompt) => prompt.accept());
  await page.getByRole('button', { name: 'Autentifică-te din nou' }).click();
  await expect(page.getByRole('heading', { name: 'Autentificare', exact: true })).toBeVisible();
  if (suffix === 'chromium') {
    const ctx = await browser.newContext({ storageState: await admin.storageState() });
    const ap = await ctx.newPage();
    await ap.goto('/');
    await ap.getByRole('button', { name: 'Utilizatori și acces', exact: true }).click();
    await expect(
      ap.getByRole('heading', { name: 'Utilizatori și acces', exact: true }),
    ).toBeVisible();
    await shot(ap, '10-users');
    await ap.getByRole('button', { name: 'Nomenclatoare', exact: true }).click();
    await expect(ap.getByRole('heading', { name: 'Nomenclatoare și reguli' })).toBeVisible();
    await shot(ap, '11-master-data');
    await ctx.close();
  }
  await admin.dispose();
  await employee.dispose();
  await managerClient.dispose();
  await fin.dispose();
});
