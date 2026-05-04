using System.Net.Http.Json;
using System.Text.Json.Serialization;

public class GoogleOAuthService : IGoogleOAuthService
{
    private readonly string _clientId;
    private readonly string _clientSecret;

    public GoogleOAuthService(IConfiguration config)
    {
        _clientId = config["Google:ClientId"] ?? "";
        _clientSecret = config["Google:ClientSecret"] ?? "";
    }

    public string BuildAuthorizationUrl(string state, string redirectUri)
    {
        var scope = "https://www.googleapis.com/auth/calendar.readonly";
        return $"https://accounts.google.com/o/oauth2/v2/auth" +
               $"?client_id={Uri.EscapeDataString(_clientId)}" +
               $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
               $"&response_type=code" +
               $"&scope={Uri.EscapeDataString(scope)}" +
               $"&state={Uri.EscapeDataString(state)}" +
               $"&access_type=offline" +
               $"&prompt=consent";
    }

    public async Task<GoogleTokenResponse?> ExchangeCodeAsync(string code, string redirectUri)
    {
        using var http = new HttpClient();
        var response = await http.PostAsync("https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code"
            }));

        if (!response.IsSuccessStatusCode)
            return null;

        var token = await response.Content.ReadFromJsonAsync<GoogleTokenJsonResponse>();
        if (token is null || string.IsNullOrEmpty(token.RefreshToken))
            return null;

        return new GoogleTokenResponse(token.AccessToken ?? "", token.RefreshToken, token.ExpiresIn);
    }

    private record GoogleTokenJsonResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; init; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }
}
