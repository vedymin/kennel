# Kennel

Kennel is a small hotel-for-dogs reservation system. The backend is a .NET API
with SQLite persistence, and the frontend is a SvelteKit app for creating and
viewing reservations.

## Prerequisites

- .NET 10 SDK
- Node.js 24
- GNU Make

## Setup

Install frontend dependencies:

```sh
cd frontend
npm install
```

The backend restores NuGet packages automatically when you build, run, or test
it with the .NET CLI.

## Development Servers

Start the API on `http://localhost:5174`:

```sh
make dev-backend
```

Start the SvelteKit dev server on `http://localhost:5173`:

```sh
make dev-frontend
```

Run both commands in separate terminals for local development. The frontend
expects the backend API at `http://localhost:5174/api/reservations`.

## Tests

Run backend xUnit integration tests:

```sh
make test-backend
```

Run frontend Vitest tests:

```sh
make test-frontend
```

Run Playwright end-to-end tests:

```sh
make test-e2e
```

Run all test suites:

```sh
make test
```

## Make Targets

| Target | Description |
| --- | --- |
| `make dev-backend` | Starts the .NET API on `localhost:5174`. |
| `make dev-frontend` | Starts the SvelteKit dev server on `localhost:5173`. |
| `make test-backend` | Runs the backend xUnit integration tests. |
| `make test-frontend` | Runs the frontend Vitest tests. |
| `make test-e2e` | Runs the Playwright end-to-end tests. |
| `make test` | Runs backend, frontend, and end-to-end test suites. |
