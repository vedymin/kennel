package pl.kennel.app.reservations

import kotlinx.coroutines.test.runTest
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Response

class HttpReservationRepositoryTest {
    @Test
    fun deleteReservationCallsApiAndReturnsSuccess() = runTest {
        val api = FakeReservationApi(deleteResponse = Response.success(Unit))

        val result = HttpReservationRepository(api).deleteReservation("local:1")

        assertEquals(DeleteReservationResult.Success, result)
        assertEquals(listOf("local:1"), api.deletedReservationIds)
    }

    @Test
    fun deleteReservationTreatsNotFoundAsSuccess() = runTest {
        val api = FakeReservationApi(
            deleteResponse = Response.error(
                404,
                "".toResponseBody("application/json".toMediaType())
            )
        )

        val result = HttpReservationRepository(api).deleteReservation("local:1")

        assertEquals(DeleteReservationResult.Success, result)
    }

    @Test
    fun deleteReservationReturnsNetworkErrorWhenRequestFails() = runTest {
        val api = FakeReservationApi(deleteException = IllegalStateException("boom"))

        val result = HttpReservationRepository(api).deleteReservation("local:1")

        assertEquals(DeleteReservationResult.NetworkError, result)
    }

    @Test
    fun createReservationReturnsNetworkErrorWhenRequestFails() = runTest {
        val api = FakeReservationApi(createException = IllegalStateException("boom"))

        val result = HttpReservationRepository(api).createReservation(
            CreateReservationRequest(
                dogName = "Burek",
                startDate = "2026-05-10",
                endDate = "2026-05-12"
            )
        )

        assertEquals(CreateReservationResult.NetworkError, result)
    }

    @Test
    fun createReservationMapsServerValidationErrors() = runTest {
        val api = FakeReservationApi(
            createResponse = Response.error(
                400,
                """{"errors":{"dogName":"Imie psa jest wymagane.","endDate":"Data zakonczenia musi byc po dacie rozpoczecia."}}"""
                    .toResponseBody("application/json".toMediaType())
            )
        )

        val result = HttpReservationRepository(api).createReservation(
            CreateReservationRequest(
                dogName = "",
                startDate = "2026-05-10",
                endDate = "2026-05-10"
            )
        )

        assertTrue(result is CreateReservationResult.ValidationError)
        val validation = result as CreateReservationResult.ValidationError
        assertEquals("Imie psa jest wymagane.", validation.fieldErrors["dogName"])
        assertEquals("Data zakonczenia musi byc po dacie rozpoczecia.", validation.fieldErrors["endDate"])
    }

    @Test
    fun createReservationPostsRequestAndReturnsCreatedReservation() = runTest {
        val api = FakeReservationApi(
            createResponse = Response.success(
                201,
                ReservationDto(
                    id = "local:1",
                    source = "local",
                    dogName = "Burek",
                    startDate = "2026-05-10",
                    endDate = "2026-05-12",
                    createdAt = "2026-05-06T10:00:00Z",
                    canDelete = true
                )
            )
        )

        val result = HttpReservationRepository(api).createReservation(
            CreateReservationRequest(
                dogName = "Burek",
                startDate = "2026-05-10",
                endDate = "2026-05-12"
            )
        )

        assertTrue(result is CreateReservationResult.Success)
        val success = result as CreateReservationResult.Success
        assertEquals("Burek", api.createdRequest?.dogName)
        assertEquals("2026-05-10", api.createdRequest?.startDate)
        assertEquals("2026-05-12", api.createdRequest?.endDate)
        assertEquals("local:1", success.reservation.id)
        assertEquals("Burek", success.reservation.dogName)
    }

    @Test
    fun listReservationsReturnsReservationsAndSourceStatuses() = runTest {
        val api = FakeReservationApi(
            listResponse = Response.success(
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
        private val listResponse: Response<ReservationListResponseDto> = Response.success(
            ReservationListResponseDto(
                items = emptyList(),
                sources = ReservationSourcesDto(local = SourceStatusDto("ok"))
            )
        ),
        private val createResponse: Response<ReservationDto> = Response.success(
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
        private val createException: Exception? = null,
        private val deleteResponse: Response<Unit> = Response.success(Unit),
        private val deleteException: Exception? = null
    ) : ReservationApi {
        var createdRequest: CreateReservationRequest? = null
        val deletedReservationIds = mutableListOf<String>()

        override suspend fun listReservations(): Response<ReservationListResponseDto> = listResponse

        override suspend fun createReservation(request: CreateReservationRequest): Response<ReservationDto> {
            createException?.let { throw it }
            createdRequest = request
            return createResponse
        }

        override suspend fun deleteReservation(id: String): Response<Unit> {
            deleteException?.let { throw it }
            deletedReservationIds += id
            return deleteResponse
        }
    }
}
