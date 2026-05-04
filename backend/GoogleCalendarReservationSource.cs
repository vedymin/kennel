using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

public interface IGoogleCalendarReservationSource
{
    Task<GoogleReservationSourceResult> GetReservationsAsync(CancellationToken cancellationToken = default);
}

public record GoogleReservationSourceResult(IReadOnlyList<ReservationResponse> Items, SourceStatus Status)
{
    public static GoogleReservationSourceResult Empty(string status) =>
        new([], new SourceStatus(status));
}

public class GoogleCalendarReservationSource(
    KennelDb db,
    IDataProtectionProvider dataProtectionProvider,
    HttpClient http,
    IConfiguration config,
    GoogleCalendarEventMapper mapper,
    TimeProvider timeProvider) : IGoogleCalendarReservationSource
{
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string CalendarApiBaseUrl = "https://www.googleapis.com/calendar/v3";

    public async Task<GoogleReservationSourceResult> GetReservationsAsync(CancellationToken cancellationToken = default)
    {
        var calendarId = config["Google:CalendarId"];
        if (string.IsNullOrWhiteSpace(calendarId))
            return GoogleReservationSourceResult.Empty("not_configured");

        var connection = await db.GoogleConnections
            .OrderByDescending(c => c.ConnectedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (connection is null)
            return GoogleReservationSourceResult.Empty("not_connected");

        try
        {
            var protector = dataProtectionProvider.CreateProtector("GoogleOAuth");
            var refreshToken = protector.Unprotect(connection.EncryptedRefreshToken);
            var accessToken = await RefreshAccessTokenAsync(refreshToken, cancellationToken);
            if (string.IsNullOrWhiteSpace(accessToken))
                return GoogleReservationSourceResult.Empty("unauthorized");

            var events = await FetchEventsAsync(calendarId, accessToken, cancellationToken);
            return new GoogleReservationSourceResult(
                events.Select(mapper.Map).ToList(),
                new SourceStatus("ok"));
        }
        catch (GoogleCalendarUnauthorizedException)
        {
            return GoogleReservationSourceResult.Empty("unauthorized");
        }
        catch (Exception)
        {
            return GoogleReservationSourceResult.Empty("error");
        }
    }

    private async Task<string?> RefreshAccessTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsync(TokenEndpoint,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = config["Google:ClientId"] ?? "",
                ["client_secret"] = config["Google:ClientSecret"] ?? "",
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token"
            }),
            cancellationToken);

        if (response.StatusCode is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.Unauthorized)
            throw new GoogleCalendarUnauthorizedException();

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Google token refresh failed with {(int)response.StatusCode}.");

        var token = await response.Content.ReadFromJsonAsync<GoogleRefreshTokenResponse>(cancellationToken);
        return token?.AccessToken;
    }

    private async Task<IReadOnlyList<GoogleCalendarEvent>> FetchEventsAsync(
        string calendarId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var timeMin = now.AddDays(-30).ToString("o");
        var timeMax = now.AddMonths(12).ToString("o");
        var uri = $"{CalendarApiBaseUrl}/calendars/{Uri.EscapeDataString(calendarId)}/events" +
            $"?singleEvents=true" +
            $"&orderBy=startTime" +
            $"&timeMin={Uri.EscapeDataString(timeMin)}" +
            $"&timeMax={Uri.EscapeDataString(timeMax)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            throw new GoogleCalendarUnauthorizedException();

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Google calendar fetch failed with {(int)response.StatusCode}.");

        var calendar = await response.Content.ReadFromJsonAsync<GoogleCalendarEventsResponse>(cancellationToken);
        return calendar?.Items ?? [];
    }

    private sealed record GoogleRefreshTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }
    }

    private sealed record GoogleCalendarEventsResponse(IReadOnlyList<GoogleCalendarEvent> Items);

    private sealed class GoogleCalendarUnauthorizedException : Exception;
}
