using Microsoft.EntityFrameworkCore;

public static class ReservationEndpoints
{
    public static IEndpointRouteBuilder MapReservationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/reservations", async (CreateReservationRequest req, KennelDb db) =>
        {
            var errors = new Dictionary<string, string>();
            var trimmedName = req.DogName?.Trim() ?? "";

            if (string.IsNullOrEmpty(trimmedName))
                errors["dogName"] = "Imię psa jest wymagane.";
            else if (trimmedName.Length > 50)
                errors["dogName"] = "Imię psa może mieć maksymalnie 50 znaków.";

            if (req.EndDate <= req.StartDate)
                errors["endDate"] = "Data zakończenia musi być po dacie rozpoczęcia.";

            if (req.StartDate < DateOnly.FromDateTime(DateTime.Today))
                errors["startDate"] = "Data rozpoczęcia nie może być w przeszłości.";

            if (errors.Count > 0)
                return Results.BadRequest(new { errors });

            var reservation = new Reservation
            {
                DogName = trimmedName,
                StartDate = req.StartDate,
                EndDate = req.EndDate,
                CreatedAt = DateTime.UtcNow
            };

            db.Reservations.Add(reservation);
            await db.SaveChangesAsync();

            var response = new ReservationResponse(reservation);
            return Results.Created($"/api/reservations/{response.Id}", response);
        });

        app.MapGet("/api/reservations", async (KennelDb db) =>
        {
            var reservations = await db.Reservations
                .OrderBy(r => r.StartDate)
                .Select(r => new ReservationResponse(r))
                .ToListAsync();

            return Results.Ok(new ReservationListResponse(
                reservations,
                new ReservationSources(new SourceStatus("ok"))));
        });

        app.MapDelete("/api/reservations/{id}", async (string id, KennelDb db) =>
        {
            if (!TryParseLocalReservationId(id, out var localId))
                return Results.NotFound();

            var reservation = await db.Reservations.FindAsync(localId);

            if (reservation is null)
                return Results.NotFound();

            db.Reservations.Remove(reservation);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        return app;
    }

    private static bool TryParseLocalReservationId(string publicId, out int localId)
    {
        const string prefix = "local:";
        localId = 0;

        return publicId.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(publicId[prefix.Length..], out localId);
    }
}
