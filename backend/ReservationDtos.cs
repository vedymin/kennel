public record CreateReservationRequest(
    int? OwnerId,
    string? OwnerName,
    int? DogId,
    string? DogName,
    DateOnly StartDate,
    DateOnly EndDate,
    TimeOnly? ArrivalTime,
    TimeOnly? DepartureTime,
    IReadOnlyList<CreateOccupationRequest>? Occupations);

public record CreateOccupationRequest(int KennelId, DateOnly StartDate, DateOnly EndDate);

public record ReplaceReservationOccupationsRequest(IReadOnlyList<CreateOccupationRequest>? Occupations);

public record ReservationListResponse(IReadOnlyList<ReservationResponse> Items, ReservationSources Sources);

public record ReservationSources(SourceStatus Local, SourceStatus Google);

public record SourceStatus(string Status);

public record OccupationResponse(int Id, int KennelId, string StartDate, string EndDate)
{
    public OccupationResponse(Occupation occupation) : this(
        occupation.Id,
        occupation.KennelId,
        occupation.StartDate.ToString("yyyy-MM-dd"),
        occupation.EndDate.ToString("yyyy-MM-dd"))
    { }
}

public record ReservationResponse(
    string Id,
    string Source,
    string DogName,
    string StartDate,
    string EndDate,
    string? CreatedAt,
    bool CanDelete,
    int? OwnerId = null,
    string? OwnerName = null,
    int? DogId = null,
    string? ArrivalTime = null,
    string? DepartureTime = null,
    IReadOnlyList<OccupationResponse>? Occupations = null)
{
    public ReservationResponse(Reservation r) : this(
        $"local:{r.Id}",
        "local",
        r.Dog?.Name ?? r.DogName,
        r.StartDate.ToString("yyyy-MM-dd"),
        r.EndDate.ToString("yyyy-MM-dd"),
        r.CreatedAt.ToString("o"),
        true,
        r.Dog?.OwnerId,
        r.Dog?.Owner?.Name,
        r.DogId,
        r.ArrivalTime?.ToString("HH:mm"),
        r.DepartureTime?.ToString("HH:mm"),
        r.Occupations.Select(occupation => new OccupationResponse(occupation)).ToList())
    { }
}
