package pl.kennel.app.reservations

import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.DELETE
import retrofit2.http.GET
import retrofit2.http.Path
import retrofit2.http.POST

interface ReservationApi {
    @GET("/api/reservations")
    suspend fun listReservations(): Response<ReservationListResponseDto>

    @POST("/api/reservations")
    suspend fun createReservation(@Body request: CreateReservationRequest): Response<ReservationDto>

    @DELETE("/api/reservations/{id}")
    suspend fun deleteReservation(@Path("id") id: String): Response<Unit>
}
