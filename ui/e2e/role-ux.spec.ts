import { test, expect, request, APIRequestContext, Page } from '@playwright/test';

const baseURL = 'http://127.0.0.1:5080';
const initial = 'Valid-test-Password1!';
const changed = 'Changed-test-Password2!';

async function csrf(client: APIRequestContext) {
  const response = await client.get('/api/auth/csrf');
  expect(response.ok()).toBeTruthy();
  return (await response.json()).token;
}

async function postLogin(client: APIRequestContext, username: string, password: string) {
  const send = async () => client.post('/api/auth/login', {
    data: { username, password },
    headers: { 'X-CSRF-TOKEN': await csrf(client) },
  });
  let result = await send();
  if (result.status() === 429) {
    await new Promise((resolve) => setTimeout(resolve, 60000));
    result = await send();
  }
  return result;
}

async function mutate(client: APIRequestContext, path: string, data: unknown) {
  const response = await client.post('/api' + path, {
    data,
    headers: {
      'X-CSRF-TOKEN': await csrf(client),
      'Idempotency-Key': crypto.randomUUID(),
    },
  });
  expect(response.ok(), await response.text()).toBeTruthy();
  return response.json();
}

async function navigate(page: Page, label: string) {
  const openMenu = page.getByRole('button', { name: 'Deschide meniul', exact: true });
  if (await openMenu.isVisible()) await openMenu.click();
  await page.getByRole('button', { name: label, exact: true }).click();
}

test('Manager, Finance and Admin navigation and labels stay scoped on desktop and mobile', async ({
  browser,
}, info) => {
  test.setTimeout(300000);
  const tag = 'ux-' + info.project.name;
  const admin = await request.newContext({ baseURL });
  try {
    let auth = await postLogin(admin, 'ci-admin', changed);
    if (auth.status() === 401)
      auth = await postLogin(admin, 'ci-admin', process.env.E2E_ADMIN_PASSWORD!);
    expect(auth.ok(), await auth.text()).toBeTruthy();
    if ((await auth.json()).changePassword)
      await mutate(admin, '/auth/change-password', {
        current: process.env.E2E_ADMIN_PASSWORD,
        new: changed,
      });

    const createUser = async (role: 'Manager' | 'Finance' | 'Employee') => {
      const username = tag + '-' + role.toLowerCase();
      await mutate(admin, '/admin/users', {
        username,
        name: username,
        email: username + '@example.invalid',
        worker: username,
        managerId: null,
        active: true,
        roles: [role],
        password: initial,
        reason: 'Isolated role UX regression',
      });
      const client = await request.newContext({ baseURL });
      const login = await postLogin(client, username, initial);
      expect(login.ok(), await login.text()).toBeTruthy();
      await mutate(client, '/auth/change-password', { current: initial, new: changed });
      return client;
    };

    const manager = await createUser('Manager');
    const finance = await createUser('Finance');
    const employee = await createUser('Employee');

    try {
      const managerContext = await browser.newContext({ storageState: await manager.storageState() });
      try {
        const page = await managerContext.newPage();
        await page.goto('/');
        await expect(page.getByRole('heading', { name: 'Privire de ansamblu' })).toBeVisible();
        await expect(page.getByRole('heading', { name: 'Deconturi care așteaptă decizia ta' })).toBeVisible();
        await expect(page.getByRole('button', { name: 'Utilizatori și acces', exact: true })).toHaveCount(0);
        await navigate(page, 'Aprobări');
        await expect(page.getByRole('heading', { name: 'Aprobări', exact: true })).toBeVisible();
        await page.getByRole('button', { name: 'RO', exact: true }).click();
        await expect(page.getByRole('heading', { name: 'Approvals', exact: true })).toBeVisible();
        await expect(page.getByRole('button', { name: 'Cards & transactions', exact: true })).toHaveCount(0);
      } finally {
        await managerContext.close();
      }

      const employeeContext = await browser.newContext({ storageState: await employee.storageState() });
      try {
        const page = await employeeContext.newPage();
        await page.goto('/');
        await expect(page.getByRole('heading', { name: 'Privire de ansamblu' })).toBeVisible();
        await expect(page.getByRole('button', { name: 'Aprobări', exact: true })).toHaveCount(0);
        await expect(page.getByRole('button', { name: 'Utilizatori și acces', exact: true })).toHaveCount(0);
        await expect(page.getByRole('button', { name: 'Carduri și tranzacții', exact: true })).toHaveCount(0);
      } finally {
        await employeeContext.close();
      }

      const cardCode = 'UXCARD-' + info.project.name.toUpperCase();
      await mutate(admin, '/configuration/masters', {
        kind: 'cards',
        code: cardCode,
        nameRo: cardCode,
        nameEn: cardCode,
        json: JSON.stringify({ lastFour: '1234', issuer: 'Synthetic test', currency: 'RON' }),
        from: '2020-01-01',
        to: null,
        active: true,
        reason: 'Synthetic UX test card',
      });

      const financeContext = await browser.newContext({ storageState: await finance.storageState() });
      try {
        const page = await financeContext.newPage();
        await page.goto('/');
        await expect(page.getByRole('heading', { name: 'Privire de ansamblu' })).toBeVisible();
        await expect(page.getByRole('button', { name: 'Aprobări', exact: true })).toHaveCount(0);
        await navigate(page, 'Finance');
        await expect(page.getByRole('heading', { name: 'Coada Finance și excepții' })).toBeVisible();
        await expect(page.getByRole('button', { name: /Gata pentru ERP/ })).toHaveAttribute('aria-pressed', /true|false/);
        await navigate(page, 'Carduri și tranzacții');
        await expect(page.getByRole('heading', { name: 'Import extras CSV / XLSX' })).toBeVisible();
        await page.getByLabel('Card companie', { exact: true }).selectOption({ label: cardCode + ' · 1234' });
        await page.getByLabel('Selectează extrasul', { exact: true }).setInputFiles({
          name: 'ux-statement.csv',
          mimeType: 'text/csv',
          buffer: Buffer.from(
            'ref,date,merchant,amount,currency,settledRon\nUX-' + tag + ',2026-01-01,Shop,12.34,RON,12.34',
          ),
        });
        await expect(page.getByText('Asociază toate coloanele extrasului înainte de verificare.')).toBeVisible();
        await expect(page.getByRole('button', { name: 'Verifică importul' })).toBeDisabled();
        await page.getByRole('button', { name: 'RO', exact: true }).click();
        await expect(page.getByLabel('Reference', { exact: true })).toBeVisible();
        await page.getByLabel('Reference', { exact: true }).selectOption('ref');
        await expect(page.getByRole('button', { name: 'Preview import' })).toBeEnabled();
        if (info.project.name === 'mobile')
          await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth))
            .toBeLessThanOrEqual(390);
      } finally {
        await financeContext.close();
      }

      const adminContext = await browser.newContext({ storageState: await admin.storageState() });
      try {
        const page = await adminContext.newPage();
        await page.goto('/');
        await navigate(page, 'Utilizatori și acces');
        await expect(page.getByRole('heading', { name: 'Utilizatori și acces' })).toBeVisible();
        const search = page.getByLabel('Caută utilizatori', { exact: true });
        await search.fill(tag + '-manager');
        await expect(page.locator('.queue .row-button').filter({ hasText: tag + '-manager' })).toHaveCount(1);
        await search.fill('no-such-user-' + tag);
        await expect(page.getByText('Niciun utilizator găsit.')).toBeVisible();
        await page.getByRole('button', { name: 'RO', exact: true }).click();
        await expect(page.getByLabel('Search users', { exact: true })).toBeVisible();
        await expect(page.getByText('No matching users.')).toBeVisible();
        if (info.project.name === 'mobile')
          await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth))
            .toBeLessThanOrEqual(390);
      } finally {
        await adminContext.close();
      }
    } finally {
      await Promise.all([manager.dispose(), finance.dispose(), employee.dispose()]);
    }
  } finally {
    await admin.dispose();
  }
});
