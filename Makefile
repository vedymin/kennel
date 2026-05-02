.PHONY: dev-backend dev-frontend test-backend test-frontend test-e2e test

dev-backend:
	dotnet run --project backend/Kennel.csproj --urls http://localhost:5174

dev-frontend:
	cd frontend && npm run dev -- --host localhost --port 5173

test-backend:
	dotnet test backend/tests/Kennel.Tests.csproj

test-frontend:
	cd frontend && npm test

test-e2e:
	cd frontend && npm run test:e2e

test: test-backend test-frontend test-e2e
