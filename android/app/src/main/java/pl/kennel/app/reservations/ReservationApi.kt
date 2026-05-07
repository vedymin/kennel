package pl.kennel.app.reservations

import retrofit2.Response
import retrofit2.http.GET

interface ReservationApi {
    @GET("/api/reservations")
    suspend fun listReservations(): Response<ReservationListResponseDto>
}
