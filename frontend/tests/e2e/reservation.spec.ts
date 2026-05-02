import { expect, test } from '@playwright/test';

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
