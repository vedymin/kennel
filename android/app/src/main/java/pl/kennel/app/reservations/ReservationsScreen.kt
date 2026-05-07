package pl.kennel.app.reservations

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.Button
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import pl.kennel.app.R

@Composable
fun ReservationsScreen(viewModel: ReservationsViewModel) {
    val state by viewModel.uiState.collectAsState()

    LaunchedEffect(viewModel) {
        viewModel.refresh()
    }

    ReservationsScreen(
        state = state,
        onRetry = viewModel::refresh
    )
}

@Composable
fun ReservationsScreen(
    state: ReservationsUiState,
    onRetry: () -> Unit
) {
    Surface(
        modifier = Modifier.fillMaxSize(),
        color = MaterialTheme.colorScheme.background
    ) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(24.dp),
            verticalArrangement = Arrangement.spacedBy(20.dp)
        ) {
            Text(
                text = stringResource(R.string.app_title),
                style = MaterialTheme.typography.headlineSmall,
                fontWeight = FontWeight.Bold
            )

            when {
                state.isLoading -> Text(stringResource(R.string.loading_reservations))
                state.hasLoadError -> LoadError(onRetry = onRetry)
                state.reservations.isEmpty() -> Text(stringResource(R.string.empty_reservations))
                else -> ReservationList(reservations = state.reservations)
            }
        }
    }
}

@Composable
private fun LoadError(onRetry: () -> Unit) {
    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
        Text(stringResource(R.string.load_reservations_error))
        Button(onClick = onRetry) {
            Text(stringResource(R.string.retry))
        }
    }
}

@Composable
private fun ReservationList(reservations: List<ReservationRowUiState>) {
    LazyColumn(
        modifier = Modifier.fillMaxWidth(),
        verticalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        items(reservations, key = { it.id }) { reservation ->
            ReservationRow(reservation = reservation)
            HorizontalDivider()
        }
    }
}

@Preview
@Composable
private fun ReservationRowPreview() {
    ReservationRow(
        reservation = ReservationRowUiState(
            id = "1",
            dogName = "Burek",
            dateRange = "2026-05-07 - 2026-05-10",
            sourceLabel = "Lokalna"
        )
    )
}

@Composable
private fun ReservationRow(reservation: ReservationRowUiState) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 10.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Column(
            modifier = Modifier.weight(1f),
            verticalArrangement = Arrangement.spacedBy(4.dp)
        ) {
            Text(
                text = reservation.dogName,
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.SemiBold
            )
            Text(
                text = reservation.dateRange,
                style = MaterialTheme.typography.bodyMedium
            )
        }

        Column(horizontalAlignment = Alignment.End, modifier = Modifier.padding(10.dp)) {
            Text(
                text = stringResource(R.string.source),
                style = MaterialTheme.typography.labelSmall
            )
            Text(
                text = reservation.sourceLabel,
                style = MaterialTheme.typography.bodyMedium
            )
        }
    }
}
