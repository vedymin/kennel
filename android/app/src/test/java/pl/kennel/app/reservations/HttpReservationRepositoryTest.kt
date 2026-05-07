package pl.kennel.app.reservations

import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Response

class HttpReservationRepositoryTest {
    @Test
    fun listReservationsReturnsReservationsAndSourceStatuses() = runTest {
        val api = FakeReservationApi(
            response = Response.success(
                ReservationListResponseDto(
                    items = listOf(
                        ReservationDto(
                            id = "local:1",
                            source = "local",
                            dogName = "Burek",
                            startDate = "2026-05-10",
                            endDate = "2026-05-12",
                            createdAt = "2026-05-06T10:00:00Z",
                            canDelete = true
                        )
                    ),
                    sources = ReservationSourcesDto(
                        local = SourceStatusDto("ok"),
                        google = SourceStatusDto("not_connected")
                    )
                )
            )
        )

        val result = HttpReservationRepository(api).listReservations()

        assertTrue(result is ListReservationsResult.Success)
        val success = result as ListReservationsResult.Success
        assertEquals("Burek", success.reservations.single().dogName)
        assertEquals("local", success.reservations.single().source)
        assertEquals("ok", success.sources.local.status)
        assertEquals("not_connected", success.sources.google.status)
    }

    private class FakeReservationApi(
        private val response: Response<ReservationListResponseDto>
    ) : ReservationApi {
        override suspend fun listReservations(): Response<ReservationListResponseDto> = response
    }
}
