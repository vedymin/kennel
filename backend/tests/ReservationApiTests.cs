using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kennel.Tests;

public class ReservationApiTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly SqliteConnection _connection;

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
            });
        });
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private HttpClient CreateClient() => _factory.CreateClient();

    [Fact]
    public async Task Post_ValidReservation_Returns201WithBody()
    {
        var client = CreateClient();
        var tomorrow = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var dayAfter = DateOnly.FromDateTime(DateTime.Today.AddDays(2));

        var response = await client.PostAsJsonAsync("/api/reservations", new
        {
            dogName = "Burek",
            startDate = tomorrow.ToString("yyyy-MM-dd"),
            endDate = dayAfter.ToString("yyyy-MM-dd")
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Burek", body.GetProperty("dogName").GetString());
        Assert.Equal(tomorrow.ToString("yyyy-MM-dd"), body.GetProperty("startDate").GetString());
        Assert.Equal(dayAfter.ToString("yyyy-MM-dd"), body.GetProperty("endDate").GetString());
        Assert.True(body.GetProperty("id").GetInt32() > 0);
        Assert.True(body.TryGetProperty("createdAt", out _));
    }

    [Fact]
    public async Task Post_EmptyDogName_Returns400WithError()
    {
        var client = CreateClient();
        var tomorrow = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var dayAfter = DateOnly.FromDateTime(DateTime.Today.AddDays(2));

        var response = await client.PostAsJsonAsync("/api/reservations", new
        {
            dogName = "   ",
            startDate = tomorrow.ToString("yyyy-MM-dd"),
            endDate = dayAfter.ToString("yyyy-MM-dd")
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

        var response = await client.PostAsJsonAsync("/api/reservations", new
        {
            dogName = new string('A', 51),
            startDate = tomorrow.ToString("yyyy-MM-dd"),
            endDate = dayAfter.ToString("yyyy-MM-dd")
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

        var response = await client.PostAsJsonAsync("/api/reservations", new
        {
            dogName = "Burek",
            startDate = tomorrow.ToString("yyyy-MM-dd"),
            endDate = tomorrow.ToString("yyyy-MM-dd")
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

        var response = await client.PostAsJsonAsync("/api/reservations", new
        {
            dogName = "Burek",
            startDate = yesterday.ToString("yyyy-MM-dd"),
            endDate = tomorrow.ToString("yyyy-MM-dd")
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
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/reservations")
        {
            Content = JsonContent.Create(new
            {
                dogName = "Burek",
                startDate = tomorrow.ToString("yyyy-MM-dd"),
                endDate = dayAfter.ToString("yyyy-MM-dd")
            })
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

        await client.PostAsJsonAsync("/api/reservations", new
        {
            dogName = "Azor",
            startDate = day1.ToString("yyyy-MM-dd"),
            endDate = day1.AddDays(1).ToString("yyyy-MM-dd")
        });
        await client.PostAsJsonAsync("/api/reservations", new
        {
            dogName = "Burek",
            startDate = day2.ToString("yyyy-MM-dd"),
            endDate = day2.AddDays(1).ToString("yyyy-MM-dd")
        });

        var response = await client.GetAsync("/api/reservations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(list);
        Assert.True(list.Length >= 2);
        Assert.Equal("Burek", list[0].GetProperty("dogName").GetString());
        Assert.Equal("Azor", list[1].GetProperty("dogName").GetString());
    }
}
