package pl.kennel.app.reservations

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.TestDispatcher
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
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

    private class FakeReservationRepository(
        private val result: ListReservationsResult
    ) : ReservationRepository {
        override suspend fun listReservations(): ListReservationsResult = result
    }
}
