using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kennel.Tests;

public class KennelApiTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly SqliteConnection _connection;

    public KennelApiTests(WebApplicationFactory<Program> factory)
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

    private static async Task<int> CreateKennel(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/kennels", new { name });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        return body.GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task PostKennel_ValidName_Returns201WithBody()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/kennels", new
        {
            name = "Boks 1"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("id").GetInt32() > 0);
        Assert.Equal("Boks 1", body.GetProperty("name").GetString());
        Assert.EndsWith($"/api/kennels/{body.GetProperty("id").GetInt32()}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task GetKennels_ReturnsAllKennels()
    {
        var client = CreateClient();
        await CreateKennel(client, "Boks 2");
        await CreateKennel(client, "Boks 1");

        var response = await client.GetAsync("/api/kennels");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var kennels = body.EnumerateArray().ToArray();
        Assert.Equal(["Boks 1", "Boks 2"], kennels.Select(kennel => kennel.GetProperty("name").GetString()!).ToArray());
        Assert.All(kennels, kennel => Assert.True(kennel.GetProperty("id").GetInt32() > 0));
    }

    [Fact]
    public async Task PostKennel_BlankName_Returns400WithError()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/kennels", new
        {
            name = "   "
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("name", out _));
    }

    [Fact]
    public async Task PutKennel_ExistingKennel_RenamesKennel()
    {
        var client = CreateClient();
        var kennelId = await CreateKennel(client, "Stary boks");

        var response = await client.PutAsJsonAsync($"/api/kennels/{kennelId}", new
        {
            name = "Nowy boks"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(kennelId, body.GetProperty("id").GetInt32());
        Assert.Equal("Nowy boks", body.GetProperty("name").GetString());

        var listResponse = await client.GetAsync("/api/kennels");
        var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        var kennel = Assert.Single(listBody.EnumerateArray());
        Assert.Equal("Nowy boks", kennel.GetProperty("name").GetString());
    }

    [Fact]
    public async Task PutKennel_MissingKennel_Returns404()
    {
        var client = CreateClient();

        var response = await client.PutAsJsonAsync("/api/kennels/999", new
        {
            name = "Nowy boks"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteKennel_ExistingKennel_DeletesKennel()
    {
        var client = CreateClient();
        var kennelId = await CreateKennel(client, "Boks do usuniecia");

        var response = await client.DeleteAsync($"/api/kennels/{kennelId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var listResponse = await client.GetAsync("/api/kennels");
        var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(listBody.EnumerateArray());
    }

    [Fact]
    public async Task DeleteKennel_MissingKennel_Returns404()
    {
        var client = CreateClient();

        var response = await client.DeleteAsync("/api/kennels/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
