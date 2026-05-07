package pl.kennel.app

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewmodel.compose.viewModel
import com.jakewharton.retrofit2.converter.kotlinx.serialization.asConverterFactory
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import pl.kennel.app.reservations.HttpReservationRepository
import pl.kennel.app.reservations.ReservationApi
import pl.kennel.app.reservations.ReservationRepository
import pl.kennel.app.reservations.ReservationsScreen
import pl.kennel.app.reservations.ReservationsViewModel
import pl.kennel.app.ui.KennelTheme
import retrofit2.Retrofit

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        val repository = createReservationRepository()

        setContent {
            KennelTheme {
                val viewModel: ReservationsViewModel = viewModel(
                    factory = ReservationsViewModelFactory(repository)
                )
                ReservationsScreen(viewModel = viewModel)
            }
        }
    }
}

private fun createReservationRepository(): ReservationRepository {
    val json = Json { ignoreUnknownKeys = true }
    val retrofit = Retrofit.Builder()
        .baseUrl("${BuildConfig.API_BASE_URL}/")
        .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
        .build()

    return HttpReservationRepository(retrofit.create(ReservationApi::class.java))
}

private class ReservationsViewModelFactory(
    private val repository: ReservationRepository
) : ViewModelProvider.Factory {
    @Suppress("UNCHECKED_CAST")
    override fun <T : ViewModel> create(modelClass: Class<T>): T {
        if (modelClass.isAssignableFrom(ReservationsViewModel::class.java)) {
            return ReservationsViewModel(repository) as T
        }

        throw IllegalArgumentException("Unsupported ViewModel: ${modelClass.name}")
    }
}
