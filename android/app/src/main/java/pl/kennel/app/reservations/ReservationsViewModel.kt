package pl.kennel.app.reservations

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

data class ReservationsUiState(
    val isLoading: Boolean = false,
    val hasLoadError: Boolean = false,
    val reservations: List<ReservationRowUiState> = emptyList()
)

data class ReservationRowUiState(
    val id: String,
    val dogName: String,
    val dateRange: String,
    val sourceLabel: String
)

class ReservationsViewModel(
    private val repository: ReservationRepository
) : ViewModel() {
    private val _uiState = MutableStateFlow(ReservationsUiState())
    val uiState: StateFlow<ReservationsUiState> = _uiState.asStateFlow()

    fun refresh() {
        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true, hasLoadError = false) }

            when (val result = repository.listReservations()) {
                is ListReservationsResult.Success -> {
                    _uiState.update {
                        it.copy(
                            isLoading = false,
                            hasLoadError = false,
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
}

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
