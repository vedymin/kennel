import { expect, test } from '@playwright/test';

test('empty reservation list shows empty state', async ({ page }) => {
	await page.route('http://localhost:5174/api/reservations', async (route) => {
		if (route.request().method() === 'GET') {
			await route.fulfill({ json: [] });
			return;
		}

		await route.fallback();
	});

	await page.goto('/');

	await expect(page.getByText('Brak rezerwacji. Dodaj pierwszą powyżej.')).toBeVisible();
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
