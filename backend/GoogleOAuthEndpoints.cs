using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

public static class GoogleOAuthEndpoints
{
    public static IEndpointRouteBuilder MapGoogleOAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/google/status", async (KennelDb db) =>
        {
            var connected = await db.GoogleConnections.AnyAsync();
            var status = connected ? "ok" : "not_connected";
            return Results.Ok(new { status });
        });

        app.MapGet("/api/google/login", (IGoogleOAuthService oauth, HttpContext ctx) =>
        {
            var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            ctx.Response.Cookies.Append("oauth_state", state, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = false,
                MaxAge = TimeSpan.FromMinutes(10)
            });

            var redirectUri = $"{ctx.Request.Scheme}://{ctx.Request.Host}/api/google/callback";
            var authUrl = oauth.BuildAuthorizationUrl(state, redirectUri);
            return Results.Redirect(authUrl);
        });

        app.MapGet("/api/google/callback", async (
            HttpContext ctx,
            IGoogleOAuthService oauth,
            IDataProtectionProvider dataProtectionProvider,
            KennelDb db,
            IConfiguration config,
            string? code,
            string? state) =>
        {
            var frontendUrl = config["Google:FrontendRedirectUrl"] ?? "http://localhost:5173";

            if (!ctx.Request.Cookies.TryGetValue("oauth_state", out var cookieState)
                || cookieState != state
                || string.IsNullOrEmpty(code))
            {
                return Results.Redirect($"{frontendUrl}?error=invalid_state");
            }

            ctx.Response.Cookies.Delete("oauth_state");

            var redirectUri = $"{ctx.Request.Scheme}://{ctx.Request.Host}/api/google/callback";
            var tokens = await oauth.ExchangeCodeAsync(code, redirectUri);
            if (tokens is null)
            {
                return Results.Redirect($"{frontendUrl}?error=token_exchange_failed");
            }

            var protector = dataProtectionProvider.CreateProtector("GoogleOAuth");
            var encrypted = protector.Protect(tokens.RefreshToken);

            var existing = await db.GoogleConnections.FirstOrDefaultAsync();
            if (existing is not null)
            {
                existing.EncryptedRefreshToken = encrypted;
                existing.ConnectedAt = DateTime.UtcNow;
            }
            else
            {
                db.GoogleConnections.Add(new GoogleConnection
                {
                    EncryptedRefreshToken = encrypted,
                    ConnectedAt = DateTime.UtcNow
                });
            }
            await db.SaveChangesAsync();

            return Results.Redirect($"{frontendUrl}?google=connected");
        });

        return app;
    }
}
