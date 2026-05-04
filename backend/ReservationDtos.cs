public record CreateReservationRequest(string DogName, DateOnly StartDate, DateOnly EndDate);

public record ReservationListResponse(IReadOnlyList<ReservationResponse> Items, ReservationSources Sources);

public record ReservationSources(SourceStatus Local);

public record SourceStatus(string Status);

public record ReservationResponse(
    string Id,
    string Source,
    string DogName,
    string StartDate,
    string EndDate,
    string? CreatedAt,
    bool CanDelete)
{
    public ReservationResponse(Reservation r) : this(
        $"local:{r.Id}",
        "local",
        r.DogName,
        r.StartDate.ToString("yyyy-MM-dd"),
        r.EndDate.ToString("yyyy-MM-dd"),
        r.CreatedAt.ToString("o"),
        true)
    { }
}
