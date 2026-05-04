import { expect, test } from '@playwright/test';

const listResponse = (items: unknown[] = []) => ({
	items,
	sources: { local: { status: 'ok' } }
});

test('empty reservation list shows empty state', async ({ page }) => {
	await page.route('http://localhost:5174/api/reservations', async (route) => {
		if (route.request().method() === 'GET') {
			await route.fulfill({ json: listResponse() });
			return;
		}

		await route.fallback();
	});

	await page.goto('/');

	await expect(page.getByText('Brak rezerwacji. Dodaj pierwszą powyżej.')).toBeVisible();
	await expect(page.getByRole('status')).toHaveCount(0);
});

test('reservation list shows source and hides delete when deletion is not allowed', async ({ page }) => {
	const reservation = {
		id: 'google:1',
		source: 'google',
		dogName: 'Figa',
		startDate: '2026-06-10',
		endDate: '2026-06-12',
		createdAt: null,
		canDelete: false
	};

	await page.route('http://localhost:5174/api/reservations', async (route) => {
		if (route.request().method() === 'GET') {
			await route.fulfill({ json: listResponse([reservation]) });
			return;
		}

		await route.fallback();
	});

	await page.goto('/');

	await expect(page.getByRole('columnheader', { name: 'Źródło' })).toBeVisible();
	await expect(page.getByRole('row', { name: /Figa 2026-06-10 2026-06-12 Google/i })).toBeVisible();
	await expect(page.getByRole('button', { name: 'Usuń rezerwację dla Figa' })).toHaveCount(0);
});

test('source health banner appears when a source is not healthy', async ({ page }) => {
	await page.route('http://localhost:5174/api/reservations', async (route) => {
		if (route.request().method() === 'GET') {
			await route.fulfill({
				json: {
					items: [],
					sources: {
						local: { status: 'ok' },
						google: { status: 'offline' }
					}
				}
			});
			return;
		}

		await route.fallback();
	});

	await page.goto('/');

	await expect(page.getByRole('status')).toHaveText('Źródło Google ma status offline.');
});

test('user can add a reservation and see it in the list', async ({ page }) => {
	const tomorrow = new Date(Date.now() + 86_400_000).toISOString().slice(0, 10);
	const dayAfter = new Date(Date.now() + 172_800_000).toISOString().slice(0, 10);
	const dogName = `Burek ${Date.now()}`;

	await page.goto('/');

	await page.getByLabel('Imię psa').fill(dogName);
	await page.getByLabel('Od').fill(tomorrow);
	await page.getByLabel('Do').fill(dayAfter);
	await page.getByRole('button', { name: 'Dodaj' }).click();

	await expect(page.getByRole('row', { name: new RegExp(dogName) })).toBeVisible();
});

test('user can delete a past reservation through the same workflow', async ({ page }) => {
	const pastReservation = {
		id: 'local:99',
		source: 'local',
		dogName: 'Senior',
		startDate: '2025-01-10',
		endDate: '2025-01-12',
		createdAt: '2025-01-09T10:00:00Z',
		canDelete: true
	};
	let deleted = false;

	await page.route('http://localhost:5174/api/reservations', async (route) => {
		if (route.request().method() === 'GET') {
			await route.fulfill({ json: listResponse(deleted ? [] : [pastReservation]) });
			return;
		}
		await route.fallback();
	});
	await page.route('http://localhost:5174/api/reservations/local:99', async (route) => {
		if (route.request().method() === 'DELETE') {
			deleted = true;
			await route.fulfill({ status: 204 });
			return;
		}
		await route.fallback();
	});

	await page.goto('/');

	const row = page.getByRole('row', { name: /Senior/ });
	await expect(row).toBeVisible();
	await expect(row).toHaveCSS('opacity', '0.5');

	await page.getByRole('button', { name: 'Usuń rezerwację dla Senior' }).click();
	const dialog = page.getByRole('dialog', { name: /usunąć rezerwację/i });
	await expect(dialog).toContainText('Senior');

	await dialog.getByRole('button', { name: 'Usuń' }).click();

	await expect(dialog).toBeHidden();
	await expect(row).toBeHidden();
});

test('deleting the last reservation shows empty state', async ({ page }) => {
	const reservation = {
		id: 'local:1',
		source: 'local',
		dogName: 'Burek',
		startDate: '2026-06-10',
		endDate: '2026-06-12',
		createdAt: '2026-05-02T10:00:00Z',
		canDelete: true
	};
	let deleted = false;

	await page.route('http://localhost:5174/api/reservations', async (route) => {
		if (route.request().method() === 'GET') {
			await route.fulfill({ json: listResponse(deleted ? [] : [reservation]) });
			return;
		}
		await route.fallback();
	});
	await page.route('http://localhost:5174/api/reservations/local:1', async (route) => {
		if (route.request().method() === 'DELETE') {
			deleted = true;
			await route.fulfill({ status: 204 });
			return;
		}
		await route.fallback();
	});

	await page.goto('/');
	await expect(page.getByRole('row', { name: /Burek/ })).toBeVisible();

	await page.getByRole('button', { name: 'Usuń rezerwację dla Burek' }).click();
	await page.getByRole('dialog').getByRole('button', { name: 'Usuń' }).click();

	await expect(page.getByText('Brak rezerwacji. Dodaj pierwszą powyżej.')).toBeVisible();
});

test('treats a 404 delete as successful absence', async ({ page }) => {
	const reservation = {
		id: 'local:42',
		source: 'local',
		dogName: 'Fantom',
		startDate: '2026-06-10',
		endDate: '2026-06-12',
		createdAt: '2026-05-02T10:00:00Z',
		canDelete: true
	};

	await page.route('http://localhost:5174/api/reservations', async (route) => {
		if (route.request().method() === 'GET') {
			await route.fulfill({ json: listResponse([reservation]) });
			return;
		}
		await route.fallback();
	});
	await page.route('http://localhost:5174/api/reservations/local:42', async (route) => {
		if (route.request().method() === 'DELETE') {
			await route.fulfill({ status: 404 });
			return;
		}
		await route.fallback();
	});

	await page.goto('/');

	await page.getByRole('button', { name: 'Usuń rezerwację dla Fantom' }).click();
	await page.getByRole('dialog').getByRole('button', { name: 'Usuń' }).click();

	await expect(page.getByRole('dialog')).toBeHidden();
});

test('user can delete a visible reservation after confirmation', async ({ page }) => {
	const tomorrow = new Date(Date.now() + 86_400_000).toISOString().slice(0, 10);
	const dayAfter = new Date(Date.now() + 172_800_000).toISOString().slice(0, 10);
	const dogName = `Azor ${Date.now()}`;

	const createResponse = await page.request.post('http://localhost:5174/api/reservations', {
		data: { dogName, startDate: tomorrow, endDate: dayAfter }
	});
	expect(createResponse.ok()).toBeTruthy();

	await page.goto('/');

	const row = page.getByRole('row', { name: new RegExp(dogName) });
	await expect(row).toBeVisible();

	await page.getByRole('button', { name: `Usuń rezerwację dla ${dogName}` }).click();
	const dialog = page.getByRole('dialog', { name: /usunąć rezerwację/i });
	await expect(dialog).toContainText(dogName);

	await dialog.getByRole('button', { name: 'Usuń' }).click();

	await expect(dialog).toBeHidden();
	await expect(row).toBeHidden();
});
