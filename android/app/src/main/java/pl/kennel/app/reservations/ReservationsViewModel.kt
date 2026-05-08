package pl.kennel.app.reservations

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import java.time.LocalDate

data class ReservationsUiState(
    val isLoading: Boolean = false,
    val hasLoadError: Boolean = false,
    val googleSourceBanner: SourceStatusBannerUiState? = null,
    val reservations: List<ReservationRowUiState> = emptyList(),
    val form: ReservationFormUiState = ReservationFormUiState()
)

data class SourceStatusBannerUiState(
    val message: String,
    val tone: SourceStatusBannerTone
)

enum class SourceStatusBannerTone {
    Info,
    Warning
}

data class ReservationFormUiState(
    val dogName: String = "",
    val startDate: String = "",
    val endDate: String = "",
    val dogNameError: String? = null,
    val startDateError: String? = null,
    val endDateError: String? = null,
    val isSubmitting: Boolean = false,
    val submitError: String? = null
)

data class ReservationRowUiState(
    val id: String,
    val dogName: String,
    val dateRange: String,
    val sourceLabel: String
)

class ReservationsViewModel(
    private val repository: ReservationRepository,
    private val todayProvider: () -> String = { LocalDate.now().toString() }
) : ViewModel() {
    private val _uiState = MutableStateFlow(ReservationsUiState())
    val uiState: StateFlow<ReservationsUiState> = _uiState.asStateFlow()

    fun refresh() {
        viewModelScope.launch {
            loadReservations()
        }
    }

    fun updateDogName(dogName: String) {
        _uiState.update {
            it.copy(form = it.form.copy(dogName = dogName, dogNameError = null))
        }
    }

    fun updateStartDate(startDate: String) {
        _uiState.update {
            it.copy(form = it.form.copy(startDate = startDate, startDateError = null))
        }
    }

    fun updateEndDate(endDate: String) {
        _uiState.update {
            it.copy(form = it.form.copy(endDate = endDate, endDateError = null))
        }
    }

    fun submitReservation() {
        val form = _uiState.value.form
        val dogNameError = when {
            form.dogName.isBlank() -> "Imie psa jest wymagane."
            form.dogName.length > 50 -> "Imie psa moze miec maksymalnie 50 znakow."
            else -> null
        }
        val startDate = form.startDate.toLocalDateOrNull()
        val endDate = form.endDate.toLocalDateOrNull()
        val today = todayProvider().toLocalDateOrNull()
        val startDateError = when {
            startDate == null -> "Wpisz date rozpoczecia."
            today != null && startDate < today -> "Data rozpoczecia nie moze byc w przeszlosci."
            else -> null
        }
        val endDateError = when {
            endDate == null -> "Wpisz date zakonczenia."
            startDate != null && endDate <= startDate -> "Data zakonczenia musi byc po dacie rozpoczecia."
            else -> null
        }

        if (dogNameError != null || startDateError != null || endDateError != null) {
            _uiState.update {
                it.copy(
                    form = it.form.copy(
                        dogNameError = dogNameError,
                        startDateError = startDateError,
                        endDateError = endDateError
                    )
                )
            }
            return
        }

        viewModelScope.launch {
            _uiState.update {
                it.copy(form = it.form.copy(isSubmitting = true, submitError = null))
            }

            when (val result = repository.createReservation(
                CreateReservationRequest(
                    dogName = form.dogName,
                    startDate = form.startDate,
                    endDate = form.endDate
                )
            )) {
                is CreateReservationResult.Success -> {
                    _uiState.update {
                        it.copy(form = ReservationFormUiState())
                    }
                    loadReservations()
                }

                is CreateReservationResult.ValidationError -> {
                    _uiState.update {
                        it.copy(
                            form = it.form.copy(
                                isSubmitting = false
                            ).withFieldErrors(result.fieldErrors)
                        )
                    }
                }

                CreateReservationResult.NetworkError -> {
                    _uiState.update {
                        it.copy(form = it.form.copy(isSubmitting = false, submitError = "Nie udalo sie zapisac rezerwacji."))
                    }
                }
            }
        }
    }

    private suspend fun loadReservations() {
        _uiState.update { it.copy(isLoading = true, hasLoadError = false) }

        when (val result = repository.listReservations()) {
            is ListReservationsResult.Success -> {
                _uiState.update {
                    it.copy(
                        isLoading = false,
                        hasLoadError = false,
                        googleSourceBanner = result.sources.google.toGoogleSourceBannerUiState(),
                        reservations = result.reservations.map { reservation ->
                            reservation.toRowUiState()
                        }
                    )
                }
            }

            ListReservationsResult.NetworkError -> {
                _uiState.update {
                    it.copy(isLoading = false, hasLoadError = true)
                }
            }
        }
    }
}

private fun String.toLocalDateOrNull(): LocalDate? =
    runCatching { LocalDate.parse(this) }.getOrNull()

private fun ReservationFormUiState.withFieldErrors(fieldErrors: Map<String, String>): ReservationFormUiState =
    copy(
        dogNameError = fieldErrors["dogName"] ?: dogNameError,
        startDateError = fieldErrors["startDate"] ?: startDateError,
        endDateError = fieldErrors["endDate"] ?: endDateError
    )

private fun Reservation.toRowUiState(): ReservationRowUiState =
    ReservationRowUiState(
        id = id,
        dogName = dogName,
        dateRange = "$startDate - $endDate",
        sourceLabel = source.toSourceLabel()
    )

private fun String.toSourceLabel(): String =
    when (this) {
        "local" -> "Lokalna"
        "google" -> "Google"
        else -> this
    }

private fun SourceStatus.toGoogleSourceBannerUiState(): SourceStatusBannerUiState? =
    when (status) {
        "not_connected" -> SourceStatusBannerUiState(
            message = "Polacz Kalendarz Google w aplikacji webowej, aby zobaczyc rezerwacje z kalendarza.",
            tone = SourceStatusBannerTone.Info
        )

        "unauthorized" -> SourceStatusBannerUiState(
            message = "Polacz ponownie Kalendarz Google w aplikacji webowej.",
            tone = SourceStatusBannerTone.Info
        )

        "error" -> SourceStatusBannerUiState(
            message = "Nie udalo sie pobrac rezerwacji z Kalendarza Google.",
            tone = SourceStatusBannerTone.Warning
        )

        else -> null
    }
