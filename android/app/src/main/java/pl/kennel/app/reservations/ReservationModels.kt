package pl.kennel.app.reservations

import kotlinx.serialization.Serializable

data class Reservation(
    val id: String,
    val source: String,
    val dogName: String,
    val startDate: String,
    val endDate: String,
    val createdAt: String?,
    val canDelete: Boolean
)

data class ReservationSources(
    val local: SourceStatus,
    val google: SourceStatus
)

data class SourceStatus(val status: String)

@Serializable
data class ReservationListResponseDto(
    val items: List<ReservationDto>,
    val sources: ReservationSourcesDto
)

@Serializable
data class ReservationDto(
    val id: String,
    val source: String,
    val dogName: String,
    val startDate: String,
    val endDate: String,
    val createdAt: String?,
    val canDelete: Boolean
)

@Serializable
data class ReservationSourcesDto(
    val local: SourceStatusDto,
    val google: SourceStatusDto = SourceStatusDto("not_configured")
)

@Serializable
data class SourceStatusDto(val status: String)

fun ReservationDto.toDomain(): Reservation =
    Reservation(
        id = id,
        source = source,
        dogName = dogName,
        startDate = startDate,
        endDate = endDate,
        createdAt = createdAt,
        canDelete = canDelete
    )

fun ReservationSourcesDto.toDomain(): ReservationSources =
    ReservationSources(
        local = SourceStatus(local.status),
        google = SourceStatus(google.status)
    )
