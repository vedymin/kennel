using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kennel.Tests;

public class FakeGoogleCalendarReservationSource : IGoogleCalendarReservationSource
{
    public GoogleReservationSourceResult Result { get; set; } =
        new([], new SourceStatus("not_connected"));

    public Task<GoogleReservationSourceResult> GetReservationsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Result);
}

public class FakeReservationAggregationService(ReservationListResponse response) : IReservationAggregationService
{
    public Task<ReservationListResponse> GetReservationsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(response);
}

public class ThrowingGoogleCalendarReservationSource : IGoogleCalendarReservationSource
{
    public Task<GoogleReservationSourceResult> GetReservationsAsync(CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Endpoint should use the aggregation service.");
}

public class ReservationApiTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly SqliteConnection _connection;
    private readonly FakeGoogleCalendarReservationSource _fakeGoogleSource = new();

    public ReservationApiTests(WebApplicationFactory<Program> factory)
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

                var googleSourceDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IGoogleCalendarReservationSource));
                if (googleSourceDescriptor != null) services.Remove(googleSourceDescriptor);

                services.AddSingleton<IGoogleCalendarReservationSource>(_fakeGoogleSource);
                services.AddScoped<ILocalReservationSource, LocalReservationSource>();
                services.AddScoped<IReservationAggregationService, ReservationAggregationService>();
            });
        });
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private HttpClient CreateClient() => _factory.CreateClient();

    private static JsonElement[] GetItems(JsonElement body) =>
        body.GetProperty("items").EnumerateArray().ToArray();

    private static object[] Occupations(params (int KennelId, DateOnly StartDate, DateOnly EndDate)[] occupations) =>
        occupations
            .Select(occupation => new
            {
                kennelId = occupation.KennelId,
                startDate = occupation.StartDate.ToString("yyyy-MM-dd"),
                endDate = occupation.EndDate.ToString("yyyy-MM-dd")
            })
            .Cast<object>()
            .ToArray();

    private static object ReservationPayload(string ownerName, string dogName, DateOnly startDate, DateOnly endDate, int kennelId) => new
    {
        ownerName,
        dogName,
        startDate = startDate.ToString("yyyy-MM-dd"),
        endDate = endDate.ToString("yyyy-MM-dd"),
        occupations = Occupations((kennelId, startDate, endDate))
    };

    private static async Task<string> CreateReservation(HttpClient client, string dogName, DateOnly startDate, DateOnly endDate)
    {
        var kennelId = await CreateKennel(client, $"Boks dla {dogName}");
        var response = await client.PostAsJsonAsync(
            "/api/reservations",
            ReservationPayload("Anna Kowalska", dogName, startDate, endDate, kennelId));
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();

        return created.GetProperty("id").GetString()!;
    }

    private static async Task<int> CreateKennel(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/kennels", new { name });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        return body.GetProperty("id").GetInt32();
    }

    private static async Task<int> CreateOwner(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/owners", new { name });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        return body.GetProperty("id").GetInt32();
    }

    private static async Task<int> CreateDog(HttpClient client, string name, int ownerId)
    {
        var response = await client.PostAsJsonAsync("/api/dogs", new { name, ownerId });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        return body.GetProperty("id").GetInt32();
    }

    private async Task SeedReservation(string dogName, DateOnly startDate, DateOnly endDate)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KennelDb>();
        db.Reservations.Add(new Reservation
        {
            DogName = dogName,
            StartDate = startDate,
            EndDate = endDate,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Post_ValidReservation_Returns201WithBody()
    {
        var client = CreateClient();
        var tomorrow = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var dayAfter = DateOnly.FromDateTime(DateTime.Today.AddDays(2));
        var kennelId = await CreateKennel(client, "Boks 1");

        var response = await client.PostAsJsonAsync("/api/reservations", new
        {
            ownerName = "Anna Kowalska",
            dogName = "Burek",
            startDate = tomorrow.ToString("yyyy-MM-dd"),
            endDate = dayAfter.ToString("yyyy-MM-dd"),
            arrivalTime = "13:30",
            departureTime = "10:15",
            occupations = Occupations((kennelId, tomorrow, dayAfter))
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Burek", body.GetProperty("dogName").GetString());
        Assert.Equal(tomorrow.ToString("yyyy-MM-dd"), body.GetProperty("startDate").GetString());
        Assert.Equal(dayAfter.ToString("yyyy-MM-dd"), body.GetProperty("endDate").GetString());
        var id = body.GetProperty("id").GetString();
        Assert.StartsWith("local:", id);
        Assert.EndsWith($"/api/reservations/{id}", response.Headers.Location!.ToString());
        Assert.Equal("local", body.GetProperty("source").GetString());
        Assert.True(body.GetProperty("canDelete").GetBoolean());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("createdAt").GetString()));
        Assert.Equal("Anna Kowalska", body.GetProperty("ownerName").GetString());
        Assert.True(body.GetProperty("dogId").GetInt32() > 0);
        Assert.Equal("13:30", body.GetProperty("arrivalTime").GetString());
        Assert.Equal("10:15", body.GetProperty("departureTime").GetString());
    }

    [Fact]
    public async Task Post_WithInlineOwnerDogAndOccupation_CreatesReservation()
    {
        var client = CreateClient();
        var kennelId = await CreateKennel(client, "Boks 1");
        var startDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var endDate = startDate.AddDays(3);

        var response = await client.PostAsJsonAsync("/api/reservations", new
        {
            ownerName = "Anna Kowalska",
            dogName = "Burek",
            startDate = startDate.ToString("yyyy-MM-dd"),
            endDate = endDate.ToString("yyyy-MM-dd"),
            occupations = new[]
            {
                new
                {
                    kennelId,
                    startDate = startDate.ToString("yyyy-MM-dd"),
                    endDate = endDate.ToString("yyyy-MM-dd")
                }
            }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Anna Kowalska", body.GetProperty("ownerName").GetString());
        Assert.Equal("Burek", body.GetProperty("dogName").GetString());
        Assert.True(body.GetProperty("ownerId").GetInt32() > 0);
        Assert.True(body.GetProperty("dogId").GetInt32() > 0);
        var occupation = Assert.Single(body.GetProperty("occupations").EnumerateArray());
        Assert.True(occupation.GetProperty("id").GetInt32() > 0);
        Assert.Equal(kennelId, occupation.GetProperty("kennelId").GetInt32());
        Assert.Equal(startDate.ToString("yyyy-MM-dd"), occupation.GetProperty("startDate").GetString());
        Assert.Equal(endDate.ToString("yyyy-MM-dd"), occupation.GetProperty("endDate").GetString());
    }

    [Fact]
    public async Task Post_WithOccupationGap_Returns400WithError()
    {
        var client = CreateClient();
        var kennelId = await CreateKennel(client, "Boks 1");
        var startDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var endDate = startDate.AddDays(4);

        var response = await client.PostAsJsonAsync("/api/reservations", new
        {
            ownerName = "Anna Kowalska",
            dogName = "Burek",
            startDate = startDate.ToString("yyyy-MM-dd"),
            endDate = endDate.ToString("yyyy-MM-dd"),
            occupations = Occupations(
                (kennelId, startDate, startDate.AddDays(2)),
                (kennelId, startDate.AddDays(3), endDate))
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("occupations", out _));
    }

    [Fact]
    public async Task Post_WithoutOccupations_Returns400WithError()
    {
        var client = CreateClient();
        var startDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var endDate = startDate.AddDays(1);

        var response = await client.PostAsJsonAsync("/api/reservations", new
        {
            ownerName = "Anna Kowalska",
            dogName = "Burek",
            startDate = startDate.ToString("yyyy-MM-dd"),
            endDate = endDate.ToString("yyyy-MM-dd"),
            occupations = Array.Empty<object>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("occupations", out _));
    }

    [Fact]
    public async Task Post_WithOccupationOverlap_Returns400WithError()
    {
        var client = CreateClient();
        var kennelId = await CreateKennel(client, "Boks 1");
        var startDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var endDate = startDate.AddDays(4);

        var response = await client.PostAsJsonAsync("/api/reservations", new
        {
            ownerName = "Anna Kowalska",
            dogName = "Burek",
            startDate = startDate.ToString("yyyy-MM-dd"),
            endDate = endDate.ToString("yyyy-MM-dd"),
            occupations = Occupations(
                (kennelId, startDate, startDate.AddDays(3)),
                (kennelId, startDate.AddDays(2), endDate))
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("occupations", out _));
    }

    [Fact]
    public async Task Post_WithExistingDogNameForOwner_ReusesDog()
    {
        var client = CreateClient();
        var ownerId = await CreateOwner(client, "Anna Kowalska");
        var dogId = await CreateDog(client, "Burek", ownerId);
        var kennelId = await CreateKennel(client, "Boks 1");
        var startDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var endDate = startDate.AddDays(1);

        var response = await client.PostAsJsonAsync("/api/reservations", new
        {
            ownerId,
            dogName = "burek",
            startDate = startDate.ToString("yyyy-MM-dd"),
            endDate = endDate.ToString("yyyy-MM-dd"),
            occupations = Occupations((kennelId, startDate, endDate))
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(dogId, body.GetProperty("dogId").GetInt32());

        var dogs = await client.GetFromJsonAsync<JsonElement>($"/api/owners/{ownerId}/dogs");
        var dog = Assert.Single(dogs.EnumerateArray());
        Assert.Equal(dogId, dog.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task PutOccupations_ReplacesOccupations()
    {
        var client = CreateClient();
        var firstKennelId = await CreateKennel(client, "Boks 1");
        var secondKennelId = await CreateKennel(client, "Boks 2");
        var startDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var splitDate = startDate.AddDays(2);
        var endDate = startDate.AddDays(4);
        var reservationId = await CreateReservation(client, "Burek", startDate, endDate);

        var response = await client.PutAsJsonAsync($"/api/reservations/{reservationId}/occupations", new
        {
            occupations = Occupations(
                (firstKennelId, startDate, splitDate),
                (secondKennelId, splitDate, endDate))
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var occupations = body.GetProperty("occupations").EnumerateArray().ToArray();
        Assert.Equal(2, occupations.Length);
        Assert.Equal(firstKennelId, occupations[0].GetProperty("kennelId").GetInt32());
        Assert.Equal(secondKennelId, occupations[1].GetProperty("kennelId").GetInt32());
        Assert.All(occupations, occupation => Assert.True(occupation.GetProperty("id").GetInt32() > 0));
    }

    [Fact]
    public async Task PutOccupations_WithGap_Returns400WithError()
    {
        var client = CreateClient();
        var kennelId = await CreateKennel(client, "Boks 1");
        var startDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var endDate = startDate.AddDays(4);
        var reservationId = await CreateReservation(client, "Burek", startDate, endDate);

        var response = await client.PutAsJsonAsync($"/api/reservations/{reservationId}/occupations", new
        {
            occupations = Occupations(
                (kennelId, startDate, startDate.AddDays(2)),
                (kennelId, startDate.AddDays(3), endDate))
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("occupations", out _));
    }

    [Fact]
    public async Task Post_EmptyDogName_Returns400WithError()
    {
        var client = CreateClient();
        var tomorrow = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var dayAfter = DateOnly.FromDateTime(DateTime.Today.AddDays(2));
        var kennelId = await CreateKennel(client, "Boks 1");

        var response = await client.PostAsJsonAsync("/api/reservations", new
        {
            ownerName = "Anna Kowalska",
            dogName = "   ",
            startDate = tomorrow.ToString("yyyy-MM-dd"),
            endDate = dayAfter.ToString("yyyy-MM-dd"),
            occupations = Occupations((kennelId, tomorrow, dayAfter))
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("dogName", out _));
    }

    [Fact]
    public async Task Post_DogNameTooLong_Returns400WithError()
    {
        var client = CreateClient();
        var tomorrow = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var dayAfter = DateOnly.FromDateTime(DateTime.Today.AddDays(2));
        var kennelId = await CreateKennel(client, "Boks 1");

        var response = await client.PostAsJsonAsync("/api/reservations", new
        {
            ownerName = "Anna Kowalska",
            dogName = new string('A', 51),
            startDate = tomorrow.ToString("yyyy-MM-dd"),
            endDate = dayAfter.ToString("yyyy-MM-dd"),
            occupations = Occupations((kennelId, tomorrow, dayAfter))
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("dogName", out _));
    }

    [Fact]
    public async Task Post_EndDateBeforeStartDate_Returns400WithError()
    {
        var client = CreateClient();
        var tomorrow = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var kennelId = await CreateKennel(client, "Boks 1");

        var response = await client.PostAsJsonAsync("/api/reservations", new
        {
            ownerName = "Anna Kowalska",
            dogName = "Burek",
            startDate = tomorrow.ToString("yyyy-MM-dd"),
            endDate = tomorrow.ToString("yyyy-MM-dd"),
            occupations = Occupations((kennelId, tomorrow, tomorrow.AddDays(1)))
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("endDate", out _));
    }

    [Fact]
    public async Task Post_StartDateInPast_Returns400WithError()
    {
        var client = CreateClient();
        var yesterday = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
        var tomorrow = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var kennelId = await CreateKennel(client, "Boks 1");

        var response = await client.PostAsJsonAsync("/api/reservations", new
        {
            ownerName = "Anna Kowalska",
            dogName = "Burek",
            startDate = yesterday.ToString("yyyy-MM-dd"),
            endDate = tomorrow.ToString("yyyy-MM-dd"),
            occupations = Occupations((kennelId, yesterday, tomorrow))
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("startDate", out _));
    }

    [Fact]
    public async Task Post_FromFrontendDevOrigin_AllowsCors()
    {
        var client = CreateClient();
        var tomorrow = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var dayAfter = DateOnly.FromDateTime(DateTime.Today.AddDays(2));
        var kennelId = await CreateKennel(client, "Boks 1");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/reservations")
        {
            Content = JsonContent.Create(ReservationPayload("Anna Kowalska", "Burek", tomorrow, dayAfter, kennelId))
        };
        request.Headers.Add("Origin", "http://localhost:5173");

        var response = await client.SendAsync(request);

        Assert.Equal("http://localhost:5173", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task Get_ReturnsReservationsSortedByStartDate()
    {
        var client = CreateClient();
        var day1 = DateOnly.FromDateTime(DateTime.Today.AddDays(3));
        var day2 = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var kennelId = await CreateKennel(client, "Boks 1");

        await client.PostAsJsonAsync(
            "/api/reservations",
            ReservationPayload("Anna Kowalska", "Azor", day1, day1.AddDays(1), kennelId));
        await client.PostAsJsonAsync(
            "/api/reservations",
            ReservationPayload("Anna Kowalska", "Burek", day2, day2.AddDays(1), kennelId));

        var response = await client.GetAsync("/api/reservations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var list = GetItems(body);
        Assert.True(list.Length >= 2);
        Assert.Equal("Burek", list[0].GetProperty("dogName").GetString());
        Assert.Equal("Azor", list[1].GetProperty("dogName").GetString());
        Assert.StartsWith("local:", list[0].GetProperty("id").GetString());
        Assert.Equal("local", list[0].GetProperty("source").GetString());
        Assert.True(list[0].GetProperty("canDelete").GetBoolean());
        Assert.False(string.IsNullOrEmpty(list[0].GetProperty("createdAt").GetString()));
    }

    [Fact]
    public async Task Get_MergesGoogleReservationsWithLocalReservationsAndSourceStatus()
    {
        var client = CreateClient();
        var localStart = DateOnly.FromDateTime(DateTime.Today.AddDays(3));
        await SeedReservation("Azor", localStart, localStart.AddDays(1));
        _fakeGoogleSource.Result = new GoogleReservationSourceResult(
            [
                new ReservationResponse(
                    "google:calendar-event",
                    "google",
                    "Mila",
                    localStart.AddDays(-2).ToString("yyyy-MM-dd"),
                    localStart.AddDays(-1).ToString("yyyy-MM-dd"),
                    null,
                    false)
            ],
            new SourceStatus("ok"));

        var response = await client.GetAsync("/api/reservations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var list = GetItems(body);
        Assert.Equal("google:calendar-event", list[0].GetProperty("id").GetString());
        Assert.Equal("google", list[0].GetProperty("source").GetString());
        Assert.False(list[0].GetProperty("canDelete").GetBoolean());
        Assert.Equal(JsonValueKind.Null, list[0].GetProperty("createdAt").ValueKind);
        Assert.Equal("local", list[1].GetProperty("source").GetString());
        Assert.Equal("ok", body.GetProperty("sources").GetProperty("local").GetProperty("status").GetString());
        Assert.Equal("ok", body.GetProperty("sources").GetProperty("google").GetProperty("status").GetString());
    }

    [Theory]
    [InlineData("not_connected")]
    [InlineData("not_configured")]
    [InlineData("unauthorized")]
    [InlineData("error")]
    public async Task Get_WhenGoogleSourceUnavailable_ReturnsLocalReservationsWithGoogleStatus(string googleStatus)
    {
        var client = CreateClient();
        var startDate = DateOnly.FromDateTime(DateTime.Today.AddDays(3));
        await SeedReservation("Azor", startDate, startDate.AddDays(1));
        _fakeGoogleSource.Result = GoogleReservationSourceResult.Empty(googleStatus);

        var response = await client.GetAsync("/api/reservations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var item = Assert.Single(GetItems(body));
        Assert.Equal("local", item.GetProperty("source").GetString());
        Assert.Equal("Azor", item.GetProperty("dogName").GetString());
        Assert.Equal("ok", body.GetProperty("sources").GetProperty("local").GetProperty("status").GetString());
        Assert.Equal(googleStatus, body.GetProperty("sources").GetProperty("google").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Get_UsesReservationAggregationService()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IReservationAggregationService)).ToList())
                    services.Remove(descriptor);
                foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IGoogleCalendarReservationSource)).ToList())
                    services.Remove(descriptor);

                services.AddSingleton<IReservationAggregationService>(new FakeReservationAggregationService(
                    new ReservationListResponse(
                        [
                            new ReservationResponse(
                                "google:from-aggregation",
                                "google",
                                "Figa",
                                "2026-06-01",
                                "2026-06-02",
                                null,
                                false)
                        ],
                        new ReservationSources(new SourceStatus("ok"), new SourceStatus("not_configured")))));
                services.AddSingleton<IGoogleCalendarReservationSource, ThrowingGoogleCalendarReservationSource>();
            });
        });
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/reservations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var item = Assert.Single(GetItems(body));
        Assert.Equal("google:from-aggregation", item.GetProperty("id").GetString());
        Assert.Equal("not_configured", body.GetProperty("sources").GetProperty("google").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Get_WhenNoReservations_ReturnsEmptyAggregate()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/reservations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(GetItems(body));
        Assert.Equal("ok", body.GetProperty("sources").GetProperty("local").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Get_IncludesPastReservations()
    {
        var client = CreateClient();
        var pastStart = DateOnly.FromDateTime(DateTime.Today.AddDays(-3));
        var pastEnd = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
        await SeedReservation("Senior", pastStart, pastEnd);

        var response = await client.GetAsync("/api/reservations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var list = GetItems(body);
        Assert.Contains(list, r => r.GetProperty("dogName").GetString() == "Senior");
    }

    [Fact]
    public async Task Delete_ExistingReservation_Returns204()
    {
        var client = CreateClient();
        var tomorrow = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var id = await CreateReservation(client, "Burek", tomorrow, tomorrow.AddDays(1));

        var response = await client.DeleteAsync($"/api/reservations/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingReservation_CascadesToOccupations()
    {
        var client = CreateClient();
        var tomorrow = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var id = await CreateReservation(client, "Burek", tomorrow, tomorrow.AddDays(1));

        using (var beforeScope = _factory.Services.CreateScope())
        {
            var db = beforeScope.ServiceProvider.GetRequiredService<KennelDb>();
            Assert.Equal(1, await db.Occupations.CountAsync());
        }

        var response = await client.DeleteAsync($"/api/reservations/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var afterScope = _factory.Services.CreateScope();
        var afterDb = afterScope.ServiceProvider.GetRequiredService<KennelDb>();
        Assert.Equal(0, await afterDb.Occupations.CountAsync());
    }

    [Fact]
    public async Task Delete_PastReservation_Returns204()
    {
        var client = CreateClient();
        var pastStart = DateOnly.FromDateTime(DateTime.Today.AddDays(-5));
        var pastEnd = DateOnly.FromDateTime(DateTime.Today.AddDays(-3));
        await SeedReservation("Senior", pastStart, pastEnd);

        var body = await client.GetFromJsonAsync<JsonElement>("/api/reservations");
        var id = GetItems(body)[0].GetProperty("id").GetString();

        var response = await client.DeleteAsync($"/api/reservations/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var afterDelete = await client.GetFromJsonAsync<JsonElement>("/api/reservations");
        Assert.Empty(GetItems(afterDelete));
    }

    [Fact]
    public async Task Delete_MissingReservation_Returns404()
    {
        var client = CreateClient();

        var response = await client.DeleteAsync("/api/reservations/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesReservationFromList()
    {
        var client = CreateClient();
        var tomorrow = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var id = await CreateReservation(client, "Burek", tomorrow, tomorrow.AddDays(1));

        await client.DeleteAsync($"/api/reservations/{id}");
        var response = await client.GetAsync("/api/reservations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var list = GetItems(body);
        Assert.DoesNotContain(list, r => r.GetProperty("id").GetString() == id);
    }
}
