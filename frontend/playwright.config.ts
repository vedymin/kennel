import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
	testDir: './tests/e2e',
	use: {
		baseURL: 'http://localhost:5173'
	},
	webServer: [
		{
			command: 'dotnet run --project ../backend/Kennel.csproj --urls http://localhost:5174',
			url: 'http://localhost:5174/api/reservations',
			reuseExistingServer: !process.env.CI,
			timeout: 120_000
		},
		{
			command: 'npm run dev -- --host localhost --port 5173',
			url: 'http://localhost:5173',
			reuseExistingServer: !process.env.CI,
			timeout: 120_000
		}
	],
	projects: [
		{
			name: 'chromium',
			use: { ...devices['Desktop Chrome'] }
		}
	]
});
