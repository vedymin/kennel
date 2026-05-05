using System.Net;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Kennel.Tests;

public class GoogleCalendarSourceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly string _dataProtectionPath;

    public GoogleCalendarSourceTests()
    {
        _connection.Open();
        _dataProtectionPath = Path.Combine(Path.GetTempPath(), $"kennel-tests-{Guid.NewGuid()}");
        _dataProtectionProvider = DataProtectionProvider.Create(new DirectoryInfo(_dataProtectionPath));
    }

    public void Dispose()
    {
        _connection.Dispose();

        if (Directory.Exists(_dataProtectionPath))
            Directory.Delete(_dataProtectionPath, recursive: true);
    }

    [Fact]
    public async Task GetReservationsAsync_UsesStoredRefreshTokenConfiguredCalendarAndDefaultFetchRange()
    {
        await using var db = CreateDb();
        var protector = _dataProtectionProvider.CreateProtector("GoogleOAuth");
        db.GoogleConnections.Add(new GoogleConnection
        {
            EncryptedRefreshToken = protector.Protect("refresh-456"),
            ConnectedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var handler = new RecordingGoogleHandler();
        var http = new HttpClient(handler);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Google:ClientId"] = "client-id",
                ["Google:ClientSecret"] = "client-secret",
                ["Google:CalendarId"] = "hotel-calendar"
            })
            .Build();
        var source = new GoogleCalendarReservationSource(
            db,
            _dataProtectionProvider,
            http,
            config,
            new GoogleCalendarEventMapper(TimeZoneInfo.Utc),
            new FixedTimeProvider(new DateTimeOffset(2026, 5, 4, 10, 15, 0, TimeSpan.Zero)));

        var result = await source.GetReservationsAsync();

        Assert.Equal("ok", result.Status.Status);
        var item = Assert.Single(result.Items);
        Assert.Equal("google:google-event", item.Id);
        Assert.Equal("Mila", item.DogName);
        Assert.Equal("2026-06-10", item.StartDate);
        Assert.Equal("2026-06-12", item.EndDate);
        Assert.Null(item.CreatedAt);
        Assert.False(item.CanDelete);

        var tokenRequest = Assert.Single(handler.Requests, r => r.Uri.AbsoluteUri == "https://oauth2.googleapis.com/token");
        Assert.Equal(HttpMethod.Post, tokenRequest.Method);
        Assert.Contains("refresh_token=refresh-456", tokenRequest.Body);
        Assert.Contains("client_id=client-id", tokenRequest.Body);
        Assert.Contains("client_secret=client-secret", tokenRequest.Body);
        Assert.Contains("grant_type=refresh_token", tokenRequest.Body);

        var eventsRequest = Assert.Single(handler.Requests, r => r.Uri.AbsolutePath.Contains("/calendar/v3/calendars/hotel-calendar/events"));
        Assert.Equal("Bearer access-123", eventsRequest.Authorization);
        var query = eventsRequest.Query;
        Assert.Equal("2026-04-04T10:15:00.0000000+00:00", query["timeMin"]);
        Assert.Equal("2027-05-04T10:15:00.0000000+00:00", query["timeMax"]);
        Assert.Equal("true", query["singleEvents"]);
        Assert.Equal("startTime", query["orderBy"]);
    }

    private KennelDb CreateDb()
    {
        var db = new KennelDb(new DbContextOptionsBuilder<KennelDb>()
            .UseSqlite(_connection)
            .Options);
        db.Database.EnsureCreated();
        return db;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingGoogleHandler : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RecordedRequest(
                request.Method,
                request.RequestUri!,
                request.Headers.Authorization?.ToString(),
                body));

            if (request.RequestUri!.AbsoluteUri == "https://oauth2.googleapis.com/token")
            {
                return Json(HttpStatusCode.OK, """{"access_token":"access-123","expires_in":3600}""");
            }

            return Json(HttpStatusCode.OK, """
                {
                  "items": [
                    {
                      "id": "google-event",
                      "summary": "Mila",
                      "start": { "date": "2026-06-10" },
                      "end": { "date": "2026-06-12" }
                    }
                  ]
                }
                """);
        }

        private static HttpResponseMessage Json(HttpStatusCode statusCode, string json) =>
            new(statusCode)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
    }

    private sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Authorization, string Body)
    {
        public Dictionary<string, string> Query => Uri.Query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                pair => Uri.UnescapeDataString(pair[0]),
                pair => Uri.UnescapeDataString(pair[1].Replace("+", " ")));
    }
}
