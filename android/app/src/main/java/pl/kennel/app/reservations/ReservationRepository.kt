package pl.kennel.app.reservations

import kotlinx.serialization.json.Json

interface ReservationRepository {
    suspend fun listReservations(): ListReservationsResult
    suspend fun createReservation(request: CreateReservationRequest): CreateReservationResult
    suspend fun deleteReservation(id: String): DeleteReservationResult
}

sealed interface ListReservationsResult {
    data class Success(
        val reservations: List<Reservation>,
        val sources: ReservationSources
    ) : ListReservationsResult

    data object NetworkError : ListReservationsResult
}

sealed interface CreateReservationResult {
    data class Success(val reservation: Reservation) : CreateReservationResult
    data class ValidationError(val fieldErrors: Map<String, String>) : CreateReservationResult
    data object NetworkError : CreateReservationResult
}

sealed interface DeleteReservationResult {
    data object Success : DeleteReservationResult
    data object NetworkError : DeleteReservationResult
}

class HttpReservationRepository(
    private val api: ReservationApi,
    private val json: Json = Json { ignoreUnknownKeys = true }
) : ReservationRepository {
    override suspend fun listReservations(): ListReservationsResult {
        return try {
            val response = api.listReservations()
            val body = response.body()

            if (!response.isSuccessful || body == null) {
                return ListReservationsResult.NetworkError
            }

            ListReservationsResult.Success(
                reservations = body.items.map { it.toDomain() },
                sources = body.sources.toDomain()
            )
        } catch (_: Exception) {
            ListReservationsResult.NetworkError
        }
    }

    override suspend fun createReservation(request: CreateReservationRequest): CreateReservationResult {
        return try {
            val response = api.createReservation(request)
            val body = response.body()

            if (response.code() == 400) {
                return response.errorBody()
                    ?.string()
                    ?.let(::decodeValidationErrors)
                    ?: CreateReservationResult.NetworkError
            }

            if (!response.isSuccessful || body == null) {
                return CreateReservationResult.NetworkError
            }

            CreateReservationResult.Success(body.toDomain())
        } catch (_: Exception) {
            CreateReservationResult.NetworkError
        }
    }

    override suspend fun deleteReservation(id: String): DeleteReservationResult {
        return try {
            val response = api.deleteReservation(id)

            if (response.code() == 404) {
                return DeleteReservationResult.Success
            }

            if (!response.isSuccessful) {
                return DeleteReservationResult.NetworkError
            }

            DeleteReservationResult.Success
        } catch (_: Exception) {
            DeleteReservationResult.NetworkError
        }
    }

    private fun decodeValidationErrors(body: String): CreateReservationResult.ValidationError? =
        try {
            CreateReservationResult.ValidationError(
                json.decodeFromString<ValidationErrorsResponseDto>(body).errors
            )
        } catch (_: Exception) {
            null
        }
}
