package pl.kennel.app.reservations

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.TestDispatcher
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class ReservationsViewModelTest {
    private val dispatcher = StandardTestDispatcher()

    @Before
    fun setUp() {
        Dispatchers.setMain(dispatcher)
    }

    @After
    fun tearDown() {
        Dispatchers.resetMain()
    }

    @Test
    fun refreshExposesReservationRowsForDisplay() = runTest(dispatcher) {
        val repository = FakeReservationRepository(
            ListReservationsResult.Success(
                reservations = listOf(
                    Reservation(
                        id = "local:1",
                        source = "local",
                        dogName = "Burek",
                        startDate = "2026-05-10",
                        endDate = "2026-05-12",
                        createdAt = "2026-05-06T10:00:00Z",
                        canDelete = true
                    )
                ),
                sources = ReservationSources(
                    local = SourceStatus("ok"),
                    google = SourceStatus("not_connected")
                )
            )
        )
        val viewModel = ReservationsViewModel(repository)

        viewModel.refresh()
        advanceUntilIdle()

        val state = viewModel.uiState.value
        assertFalse(state.isLoading)
        assertFalse(state.hasLoadError)
        assertEquals("Burek", state.reservations.single().dogName)
        assertEquals("2026-05-10 - 2026-05-12", state.reservations.single().dateRange)
        assertEquals("Lokalna", state.reservations.single().sourceLabel)
    }

    @Test
    fun refreshShowsGoogleConnectionBannerWhenGoogleSourceIsNotConnected() = runTest(dispatcher) {
        val repository = FakeReservationRepository(successWithGoogleStatus("not_connected"))
        val viewModel = ReservationsViewModel(repository)

        viewModel.refresh()
        advanceUntilIdle()

        assertEquals(
            SourceStatusBannerUiState(
                message = "Polacz Kalendarz Google w aplikacji webowej, aby zobaczyc rezerwacje z kalendarza.",
                tone = SourceStatusBannerTone.Info
            ),
            viewModel.uiState.value.googleSourceBanner
        )
    }

    @Test
    fun refreshShowsGoogleReconnectBannerWhenGoogleSourceIsUnauthorized() = runTest(dispatcher) {
        val repository = FakeReservationRepository(successWithGoogleStatus("unauthorized"))
        val viewModel = ReservationsViewModel(repository)

        viewModel.refresh()
        advanceUntilIdle()

        assertEquals(
            SourceStatusBannerUiState(
                message = "Polacz ponownie Kalendarz Google w aplikacji webowej.",
                tone = SourceStatusBannerTone.Info
            ),
            viewModel.uiState.value.googleSourceBanner
        )
    }

    @Test
    fun refreshShowsWarningBannerWhenGoogleSourceHasError() = runTest(dispatcher) {
        val repository = FakeReservationRepository(successWithGoogleStatus("error"))
        val viewModel = ReservationsViewModel(repository)

        viewModel.refresh()
        advanceUntilIdle()

        assertEquals(
            SourceStatusBannerUiState(
                message = "Nie udalo sie pobrac rezerwacji z Kalendarza Google.",
                tone = SourceStatusBannerTone.Warning
            ),
            viewModel.uiState.value.googleSourceBanner
        )
    }

    @Test
    fun refreshDoesNotShowGoogleSourceBannerWhenGoogleSourceIsOk() = runTest(dispatcher) {
        val repository = FakeReservationRepository(successWithGoogleStatus("ok"))
        val viewModel = ReservationsViewModel(repository)

        viewModel.refresh()
        advanceUntilIdle()

        assertEquals(null, viewModel.uiState.value.googleSourceBanner)
    }

    @Test
    fun refreshDoesNotShowGoogleSourceBannerWhenGoogleSourceIsNotConfigured() = runTest(dispatcher) {
        val repository = FakeReservationRepository(successWithGoogleStatus("not_configured"))
        val viewModel = ReservationsViewModel(repository)

        viewModel.refresh()
        advanceUntilIdle()

        assertEquals(null, viewModel.uiState.value.googleSourceBanner)
    }

    @Test
    fun submitReservationWithEmptyDogNameShowsFieldErrorAndDoesNotCreateReservation() = runTest(dispatcher) {
        val repository = FakeReservationRepository(
            listResult = ListReservationsResult.Success(
                reservations = emptyList(),
                sources = ReservationSources(
                    local = SourceStatus("ok"),
                    google = SourceStatus("not_connected")
                )
            )
        )
        val viewModel = ReservationsViewModel(repository, todayProvider = { "2026-05-07" })
        viewModel.updateDogName("   ")
        viewModel.updateStartDate("2026-05-08")
        viewModel.updateEndDate("2026-05-09")

        viewModel.submitReservation()
        advanceUntilIdle()

        assertEquals("Imie psa jest wymagane.", viewModel.uiState.value.form.dogNameError)
        assertEquals(emptyList<CreateReservationRequest>(), repository.createRequests)
    }

    @Test
    fun submitReservationWithTooLongDogNameShowsFieldErrorAndDoesNotCreateReservation() = runTest(dispatcher) {
        val repository = FakeReservationRepository(
            listResult = ListReservationsResult.Success(
                reservations = emptyList(),
                sources = ReservationSources(
                    local = SourceStatus("ok"),
                    google = SourceStatus("not_connected")
                )
            )
        )
        val viewModel = ReservationsViewModel(repository, todayProvider = { "2026-05-07" })
        viewModel.updateDogName("A".repeat(51))
        viewModel.updateStartDate("2026-05-08")
        viewModel.updateEndDate("2026-05-09")

        viewModel.submitReservation()
        advanceUntilIdle()

        assertEquals("Imie psa moze miec maksymalnie 50 znakow.", viewModel.uiState.value.form.dogNameError)
        assertEquals(emptyList<CreateReservationRequest>(), repository.createRequests)
    }

    @Test
    fun submitReservationWithPastStartDateShowsFieldErrorAndDoesNotCreateReservation() = runTest(dispatcher) {
        val repository = FakeReservationRepository(
            listResult = ListReservationsResult.Success(
                reservations = emptyList(),
                sources = ReservationSources(
                    local = SourceStatus("ok"),
                    google = SourceStatus("not_connected")
                )
            )
        )
        val viewModel = ReservationsViewModel(repository, todayProvider = { "2026-05-07" })
        viewModel.updateDogName("Burek")
        viewModel.updateStartDate("2026-05-06")
        viewModel.updateEndDate("2026-05-08")

        viewModel.submitReservation()
        advanceUntilIdle()

        assertEquals("Data rozpoczecia nie moze byc w przeszlosci.", viewModel.uiState.value.form.startDateError)
        assertEquals(emptyList<CreateReservationRequest>(), repository.createRequests)
    }

    @Test
    fun submitReservationWithEndDateNotAfterStartDateShowsFieldErrorAndDoesNotCreateReservation() = runTest(dispatcher) {
        val repository = FakeReservationRepository(
            listResult = ListReservationsResult.Success(
                reservations = emptyList(),
                sources = ReservationSources(
                    local = SourceStatus("ok"),
                    google = SourceStatus("not_connected")
                )
            )
        )
        val viewModel = ReservationsViewModel(repository, todayProvider = { "2026-05-07" })
        viewModel.updateDogName("Burek")
        viewModel.updateStartDate("2026-05-08")
        viewModel.updateEndDate("2026-05-08")

        viewModel.submitReservation()
        advanceUntilIdle()

        assertEquals("Data zakonczenia musi byc po dacie rozpoczecia.", viewModel.uiState.value.form.endDateError)
        assertEquals(emptyList<CreateReservationRequest>(), repository.createRequests)
    }

    @Test
    fun submitReservationSuccessClearsFormAndReloadsReservations() = runTest(dispatcher) {
        val repository = FakeReservationRepository(
            listResult = ListReservationsResult.Success(
                reservations = listOf(
                    Reservation(
                        id = "local:2",
                        source = "local",
                        dogName = "Azor",
                        startDate = "2026-05-11",
                        endDate = "2026-05-13",
                        createdAt = "2026-05-06T10:00:00Z",
                        canDelete = true
                    )
                ),
                sources = ReservationSources(
                    local = SourceStatus("ok"),
                    google = SourceStatus("not_connected")
                )
            ),
            createResult = CreateReservationResult.Success(
                Reservation(
                    id = "local:1",
                    source = "local",
                    dogName = "Burek",
                    startDate = "2026-05-08",
                    endDate = "2026-05-09",
                    createdAt = "2026-05-06T10:00:00Z",
                    canDelete = true
                )
            )
        )
        val viewModel = ReservationsViewModel(repository, todayProvider = { "2026-05-07" })
        viewModel.updateDogName("Burek")
        viewModel.updateStartDate("2026-05-08")
        viewModel.updateEndDate("2026-05-09")

        viewModel.submitReservation()
        advanceUntilIdle()

        assertEquals(
            listOf(CreateReservationRequest("Burek", "2026-05-08", "2026-05-09")),
            repository.createRequests
        )
        assertEquals(1, repository.listCalls)
        assertEquals(ReservationFormUiState(), viewModel.uiState.value.form)
        assertEquals("Azor", viewModel.uiState.value.reservations.single().dogName)
    }

    @Test
    fun submitReservationServerValidationErrorsPropagateToFormFields() = runTest(dispatcher) {
        val repository = FakeReservationRepository(
            listResult = ListReservationsResult.Success(
                reservations = emptyList(),
                sources = ReservationSources(
                    local = SourceStatus("ok"),
                    google = SourceStatus("not_connected")
                )
            ),
            createResult = CreateReservationResult.ValidationError(
                mapOf(
                    "dogName" to "Serwer: imie psa jest wymagane.",
                    "startDate" to "Serwer: data rozpoczecia jest niepoprawna.",
                    "endDate" to "Serwer: data zakonczenia jest niepoprawna."
                )
            )
        )
        val viewModel = ReservationsViewModel(repository, todayProvider = { "2026-05-07" })
        viewModel.updateDogName("Burek")
        viewModel.updateStartDate("2026-05-08")
        viewModel.updateEndDate("2026-05-09")

        viewModel.submitReservation()
        advanceUntilIdle()

        val form = viewModel.uiState.value.form
        assertEquals("Serwer: imie psa jest wymagane.", form.dogNameError)
        assertEquals("Serwer: data rozpoczecia jest niepoprawna.", form.startDateError)
        assertEquals("Serwer: data zakonczenia jest niepoprawna.", form.endDateError)
        assertFalse(form.isSubmitting)
    }

    @Test
    fun submitReservationShowsSubmittingIndicatorWhileRequestIsInFlight() = runTest(dispatcher) {
        val createResult = CompletableDeferred<CreateReservationResult>()
        val repository = FakeReservationRepository(
            listResult = ListReservationsResult.Success(
                reservations = emptyList(),
                sources = ReservationSources(
                    local = SourceStatus("ok"),
                    google = SourceStatus("not_connected")
                )
            ),
            createResultProvider = { createResult.await() }
        )
        val viewModel = ReservationsViewModel(repository, todayProvider = { "2026-05-07" })
        viewModel.updateDogName("Burek")
        viewModel.updateStartDate("2026-05-08")
        viewModel.updateEndDate("2026-05-09")

        viewModel.submitReservation()
        runCurrent()

        assertTrue(viewModel.uiState.value.form.isSubmitting)

        createResult.complete(CreateReservationResult.NetworkError)
        advanceUntilIdle()

        assertFalse(viewModel.uiState.value.form.isSubmitting)
    }

    private class FakeReservationRepository(
        private val listResult: ListReservationsResult,
        private val createResult: CreateReservationResult = CreateReservationResult.NetworkError,
        private val createResultProvider: (suspend () -> CreateReservationResult)? = null
    ) : ReservationRepository {
        val createRequests = mutableListOf<CreateReservationRequest>()
        var listCalls = 0

        override suspend fun listReservations(): ListReservationsResult {
            listCalls += 1
            return listResult
        }

        override suspend fun createReservation(request: CreateReservationRequest): CreateReservationResult {
            createRequests += request
            return createResultProvider?.invoke() ?: createResult
        }
    }
}

private fun successWithGoogleStatus(status: String): ListReservationsResult.Success =
    ListReservationsResult.Success(
        reservations = emptyList(),
        sources = ReservationSources(
            local = SourceStatus("ok"),
            google = SourceStatus(status)
        )
    )
