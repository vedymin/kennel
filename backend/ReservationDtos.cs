public record CreateReservationRequest(string DogName, DateOnly StartDate, DateOnly EndDate);

public record ReservationResponse(int Id, string DogName, string StartDate, string EndDate, string CreatedAt)
{
    public ReservationResponse(Reservation r) : this(
        r.Id,
        r.DogName,
        r.StartDate.ToString("yyyy-MM-dd"),
        r.EndDate.ToString("yyyy-MM-dd"),
        r.CreatedAt.ToString("o"))
    { }
}
