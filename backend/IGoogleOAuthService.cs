public interface IGoogleOAuthService
{
    string BuildAuthorizationUrl(string state, string redirectUri);
    Task<GoogleTokenResponse?> ExchangeCodeAsync(string code, string redirectUri);
}

public record GoogleTokenResponse(string AccessToken, string RefreshToken, int ExpiresIn);
