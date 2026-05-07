package pl.kennel.app.ui

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color

private val KennelColors = lightColorScheme(
    primary = Color(0xFF2F6F5E),
    secondary = Color(0xFF725B2E),
    tertiary = Color(0xFF5C6380),
    background = Color(0xFFFBFBF8),
    surface = Color(0xFFFFFFFF),
    onPrimary = Color(0xFFFFFFFF),
    onSecondary = Color(0xFFFFFFFF),
    onTertiary = Color(0xFFFFFFFF),
    onBackground = Color(0xFF1E2421),
    onSurface = Color(0xFF1E2421)
)

@Composable
fun KennelTheme(content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = KennelColors,
        content = content
    )
}
