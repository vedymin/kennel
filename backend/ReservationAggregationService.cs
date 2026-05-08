using Microsoft.EntityFrameworkCore;

public interface IReservationAggregationService
{
    Task<ReservationListResponse> GetReservationsAsync(CancellationToken cancellationToken = default);
}

public interface ILocalReservationSource
{
    Task<LocalReservationSourceResult> GetReservationsAsync(CancellationToken cancellationToken = default);
}

public record LocalReservationSourceResult(IReadOnlyList<ReservationResponse> Items, SourceStatus Status);

public class LocalReservationSource(KennelDb db) : ILocalReservationSource
{
    public async Task<LocalReservationSourceResult> GetReservationsAsync(CancellationToken cancellationToken = default)
    {
        var reservations = await db.Reservations
            .Include(r => r.Dog)
            .ThenInclude(dog => dog!.Owner)
            .Include(r => r.Occupations)
            .OrderBy(r => r.StartDate)
            .ToListAsync(cancellationToken);

        return new LocalReservationSourceResult(
            reservations.Select(r => new ReservationResponse(r)).ToList(),
            new SourceStatus("ok"));
    }
}

public class ReservationAggregationService(
    ILocalReservationSource localSource,
    IGoogleCalendarReservationSource googleSource) : IReservationAggregationService
{
    public async Task<ReservationListResponse> GetReservationsAsync(CancellationToken cancellationToken = default)
    {
        var local = await localSource.GetReservationsAsync(cancellationToken);
        var google = await googleSource.GetReservationsAsync(cancellationToken);
        var items = local.Items
            .Concat(google.Items)
            .OrderBy(r => r.StartDate, StringComparer.Ordinal)
            .ThenBy(r => r.EndDate, StringComparer.Ordinal)
            .ThenBy(r => r.DogName, StringComparer.Ordinal)
            .ThenBy(r => r.Source, StringComparer.Ordinal)
            .ToList();

        return new ReservationListResponse(items, new ReservationSources(local.Status, google.Status));
    }
}
