using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Kennel.Tests;

public class ReservationAggregationServiceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public ReservationAggregationServiceTests()
    {
        _connection.Open();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task GetReservationsAsync_WhenBothSourcesOk_ReturnsSortedItemsAndBothSourceStatuses()
    {
        await using var db = CreateDb();
        await SeedReservation(db, "Mila", new DateOnly(2026, 6, 10), new DateOnly(2026, 6, 12));
        await SeedReservation(db, "Burek", new DateOnly(2026, 6, 10), new DateOnly(2026, 6, 11));

        var googleSource = new FakeGoogleCalendarReservationSource
        {
            Result = new GoogleReservationSourceResult(
                [
                    new ReservationResponse("google:mila", "google", "Mila", "2026-06-10", "2026-06-12", null, false),
                    new ReservationResponse("google:azor", "google", "Azor", "2026-06-09", "2026-06-10", null, false)
                ],
                new SourceStatus("ok"))
        };
        var service = new ReservationAggregationService(new LocalReservationSource(db), googleSource);

        var result = await service.GetReservationsAsync();

        Assert.Equal(["google:azor", "local:2", "google:mila", "local:1"],
            result.Items.Select(item => item.Id).ToArray());
        Assert.Equal("ok", result.Sources.Local.Status);
        Assert.Equal("ok", result.Sources.Google.Status);
    }

    private KennelDb CreateDb()
    {
        var db = new KennelDb(new DbContextOptionsBuilder<KennelDb>()
            .UseSqlite(_connection)
            .Options);
        db.Database.EnsureCreated();
        return db;
    }

    private static async Task SeedReservation(KennelDb db, string dogName, DateOnly startDate, DateOnly endDate)
    {
        db.Reservations.Add(new Reservation
        {
            DogName = dogName,
            StartDate = startDate,
            EndDate = endDate,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }
}
