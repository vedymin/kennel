using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kennel.Tests;

public class OwnerDogApiTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly SqliteConnection _connection;

    public OwnerDogApiTests(WebApplicationFactory<Program> factory)
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

    [Fact]
    public async Task PostOwner_ValidName_Returns201WithBody()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/owners", new
        {
            name = "Anna Kowalska"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("id").GetInt32() > 0);
        Assert.Equal("Anna Kowalska", body.GetProperty("name").GetString());
        Assert.EndsWith($"/api/owners/{body.GetProperty("id").GetInt32()}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task GetOwners_Search_ReturnsCaseInsensitivePartialMatches()
    {
        var client = CreateClient();
        await CreateOwner(client, "Anna Kowalska");
        await CreateOwner(client, "Jan Nowak");

        var response = await client.GetAsync("/api/owners?search=KOW");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var owner = Assert.Single(body.EnumerateArray());
        Assert.Equal("Anna Kowalska", owner.GetProperty("name").GetString());
    }

    [Fact]
    public async Task PostOwner_BlankName_Returns400WithError()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/owners", new
        {
            name = "   "
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("name", out _));
    }

    [Fact]
    public async Task PostOwner_DuplicateName_Returns400WithError()
    {
        var client = CreateClient();
        await CreateOwner(client, "Anna Kowalska");

        var response = await client.PostAsJsonAsync("/api/owners", new
        {
            name = "anna kowalska"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("name", out _));
    }

    [Fact]
    public async Task PostDog_ValidNameAndOwner_Returns201WithBody()
    {
        var client = CreateClient();
        var ownerId = await CreateOwner(client, "Anna Kowalska");

        var response = await client.PostAsJsonAsync("/api/dogs", new
        {
            name = "Burek",
            ownerId
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("id").GetInt32() > 0);
        Assert.Equal("Burek", body.GetProperty("name").GetString());
        Assert.Equal(ownerId, body.GetProperty("ownerId").GetInt32());
        Assert.EndsWith($"/api/dogs/{body.GetProperty("id").GetInt32()}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task GetOwnerDogs_ReturnsOnlyDogsForOwner()
    {
        var client = CreateClient();
        var annaId = await CreateOwner(client, "Anna Kowalska");
        var janId = await CreateOwner(client, "Jan Nowak");
        await CreateDog(client, "Burek", annaId);
        await CreateDog(client, "Azor", annaId);
        await CreateDog(client, "Burek", janId);

        var response = await client.GetAsync($"/api/owners/{annaId}/dogs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var dogs = body.EnumerateArray().ToArray();
        Assert.Equal(2, dogs.Length);
        Assert.All(dogs, dog => Assert.Equal(annaId, dog.GetProperty("ownerId").GetInt32()));
        Assert.Equal(["Azor", "Burek"], dogs.Select(dog => dog.GetProperty("name").GetString()!).ToArray());
    }

    [Fact]
    public async Task PostDog_BlankName_Returns400WithError()
    {
        var client = CreateClient();
        var ownerId = await CreateOwner(client, "Anna Kowalska");

        var response = await client.PostAsJsonAsync("/api/dogs", new
        {
            name = "   ",
            ownerId
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("name", out _));
    }

    [Fact]
    public async Task PostDog_DuplicateNameForOwner_Returns400WithError()
    {
        var client = CreateClient();
        var ownerId = await CreateOwner(client, "Anna Kowalska");
        await CreateDog(client, "Burek", ownerId);

        var response = await client.PostAsJsonAsync("/api/dogs", new
        {
            name = "burek",
            ownerId
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("name", out _));
    }
}
