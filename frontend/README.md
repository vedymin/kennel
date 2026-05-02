# Kennel frontend

SvelteKit SPA for the kennel reservation form. The app talks to the backend API at `http://localhost:5174/api/reservations`.

## Development

Install dependencies:

```sh
npm install
```

Start the frontend dev server:

```sh
npm run dev
```

Open `http://localhost:5173`. For the form to work, also run the backend from `../backend`:

```sh
dotnet run
```

## Tests

Run component tests:

```sh
npm test
```

Run type and Svelte checks:

```sh
npm run check
```

Run the Playwright happy path test:

```sh
npm run test:e2e
```
