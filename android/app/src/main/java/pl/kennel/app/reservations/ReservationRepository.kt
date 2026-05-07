package pl.kennel.app.reservations

interface ReservationRepository {
    suspend fun listReservations(): ListReservationsResult
}

sealed interface ListReservationsResult {
    data class Success(
        val reservations: List<Reservation>,
        val sources: ReservationSources
    ) : ListReservationsResult

    data object NetworkError : ListReservationsResult
}

class HttpReservationRepository(
    private val api: ReservationApi
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
}
