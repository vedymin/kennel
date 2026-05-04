using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kennel.Tests;

public class FakeGoogleOAuthService : IGoogleOAuthService
{
    public string? LastState { get; private set; }
    public GoogleTokenResponse? TokenToReturn { get; set; }

    public string BuildAuthorizationUrl(string state, string redirectUri)
    {
        LastState = state;
        return $"https://accounts.google.com/o/oauth2/v2/auth?state={state}&redirect_uri={redirectUri}";
    }

    public Task<GoogleTokenResponse?> ExchangeCodeAsync(string code, string redirectUri)
    {
        return Task.FromResult(TokenToReturn);
    }
}

public class GoogleOAuthTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly SqliteConnection _connection;
    private readonly FakeGoogleOAuthService _fakeOAuth = new();

    public GoogleOAuthTests(WebApplicationFactory<Program> factory)
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<KennelDb>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<KennelDb>(options =>
                    options.UseSqlite(_connection));

                services.AddSingleton<IGoogleOAuthService>(_fakeOAuth);
            });
        });
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Status_WhenNoConnection_ReturnsNotConnected()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/google/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("not_connected", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Login_RedirectsToGoogleAndSetsStateCookie()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/google/login");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.StartsWith("https://accounts.google.com/", location);

        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        var stateCookie = cookies.First(c => c.StartsWith("oauth_state="));
        Assert.Contains("httponly", stateCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", stateCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Callback_WithoutStateCookie_RedirectsToFrontendWithError()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/google/callback?code=authcode&state=somestate");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.Contains("error=", location);
    }

    [Fact]
    public async Task Callback_WithValidState_StoresTokenAndRedirectsToFrontend()
    {
        _fakeOAuth.TokenToReturn = new GoogleTokenResponse("access123", "refresh456", 3600);
        var client = CreateClient();

        var loginResponse = await client.GetAsync("/api/google/login");
        var stateCookie = loginResponse.Headers.GetValues("Set-Cookie")
            .First(c => c.StartsWith("oauth_state="));
        var rawCookieValue = stateCookie.Split('=', 2)[1].Split(';')[0];
        var stateValue = Uri.UnescapeDataString(rawCookieValue);

        var callbackRequest = new HttpRequestMessage(HttpMethod.Get,
            $"/api/google/callback?code=authcode&state={Uri.EscapeDataString(stateValue)}");
        callbackRequest.Headers.Add("Cookie", $"oauth_state={rawCookieValue}");

        var callbackResponse = await client.SendAsync(callbackRequest);

        Assert.Equal(HttpStatusCode.Redirect, callbackResponse.StatusCode);
        var location = callbackResponse.Headers.Location!.ToString();
        Assert.Contains("google=connected", location);
        Assert.DoesNotContain("error=", location);
        Assert.DoesNotContain("refresh", location, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("access", location, StringComparison.OrdinalIgnoreCase);

        var statusResponse = await client.GetAsync("/api/google/status");
        var body = await statusResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ok", body.GetProperty("status").GetString());
    }
}
