ifeq ($(OS),Windows_NT)
ANDROID_GRADLEW = gradlew.bat
else
ANDROID_GRADLEW = ./gradlew
endif

.PHONY: dev-backend dev-frontend dev-android-device test-backend test-frontend test-android test-e2e test

dev-backend:
	dotnet run --project backend/Kennel.csproj --urls http://localhost:5174

dev-frontend:
	cd frontend && npm run dev -- --host localhost --port 5173

test-backend:
	dotnet test backend/tests/Kennel.Tests.csproj

test-frontend:
	cd frontend && npm test

dev-android-device:
	adb reverse tcp:5174 tcp:5174
	cd android && $(ANDROID_GRADLEW) installDebug -PapiBaseUrl=http://localhost:5174

test-android:
	cd android && $(ANDROID_GRADLEW) testDebugUnitTest

test-e2e:
	cd frontend && npm run test:e2e

test: test-backend test-frontend test-android test-e2e
