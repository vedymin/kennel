using Microsoft.EntityFrameworkCore;

public static class ReservationEndpoints
{
    public static IEndpointRouteBuilder MapReservationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/reservations", async (CreateReservationRequest req, KennelDb db) =>
        {
            var errors = new Dictionary<string, string>();
            var trimmedOwnerName = req.OwnerName?.Trim() ?? "";
            var trimmedDogName = req.DogName?.Trim() ?? "";
            var requestedOccupations = req.Occupations?.ToList() ?? [];

            if (!req.OwnerId.HasValue && string.IsNullOrEmpty(trimmedOwnerName))
                errors["owner"] = "Owner identification is required.";

            if (!req.DogId.HasValue && string.IsNullOrEmpty(trimmedDogName))
                errors["dogName"] = "Dog name is required.";
            else if (!string.IsNullOrEmpty(trimmedDogName) && trimmedDogName.Length > 50)
                errors["dogName"] = "Dog name can have at most 50 characters.";

            if (req.EndDate <= req.StartDate)
                errors["endDate"] = "End date must be after start date.";

            if (req.StartDate < DateOnly.FromDateTime(DateTime.Today))
                errors["startDate"] = "Start date cannot be in the past.";

            ValidateOccupationCoverage(req.StartDate, req.EndDate, requestedOccupations, errors);
            await ValidateKennelsExist(requestedOccupations, db, errors);

            if (errors.Count > 0)
                return Results.BadRequest(new { errors });

            var owner = await ResolveOwner(req.OwnerId, trimmedOwnerName, db, errors);
            var dog = await ResolveDog(req.DogId, trimmedDogName, owner, db, errors);

            if (errors.Count > 0)
                return Results.BadRequest(new { errors });

            await ValidateKennelSharing(null, dog!, requestedOccupations, db, errors);

            if (errors.Count > 0)
                return Results.BadRequest(new { errors });

            var reservation = new Reservation
            {
                Dog = dog,
                DogName = dog!.Name,
                StartDate = req.StartDate,
                EndDate = req.EndDate,
                ArrivalTime = req.ArrivalTime,
                DepartureTime = req.DepartureTime,
                CreatedAt = DateTime.UtcNow
            };
            reservation.Occupations.AddRange(requestedOccupations.Select(occupation => new Occupation
            {
                KennelId = occupation.KennelId,
                StartDate = occupation.StartDate,
                EndDate = occupation.EndDate
            }));

            db.Reservations.Add(reservation);
            await db.SaveChangesAsync();

            var response = new ReservationResponse(reservation);
            return Results.Created($"/api/reservations/{response.Id}", response);
        });

        app.MapGet("/api/reservations", async (IReservationAggregationService reservationAggregation) =>
        {
            var response = await reservationAggregation.GetReservationsAsync();
            return Results.Ok(response);
        });

        app.MapPut("/api/reservations/{id}/occupations", async (string id, ReplaceReservationOccupationsRequest req, KennelDb db) =>
        {
            if (!TryParseLocalReservationId(id, out var localId))
                return Results.NotFound();

            var reservation = await db.Reservations
                .Include(r => r.Dog)
                .ThenInclude(dog => dog!.Owner)
                .Include(r => r.Occupations)
                .SingleOrDefaultAsync(r => r.Id == localId);

            if (reservation is null)
                return Results.NotFound();

            var requestedOccupations = req.Occupations?.ToList() ?? [];
            var errors = new Dictionary<string, string>();
            ValidateOccupationCoverage(reservation.StartDate, reservation.EndDate, requestedOccupations, errors);
            await ValidateKennelsExist(requestedOccupations, db, errors);

            if (errors.Count > 0)
                return Results.BadRequest(new { errors });

            if (reservation.Dog is not null)
                await ValidateKennelSharing(reservation.Id, reservation.Dog, requestedOccupations, db, errors);

            if (errors.Count > 0)
                return Results.BadRequest(new { errors });

            db.Occupations.RemoveRange(reservation.Occupations);
            reservation.Occupations.Clear();
            reservation.Occupations.AddRange(requestedOccupations.Select(occupation => new Occupation
            {
                KennelId = occupation.KennelId,
                StartDate = occupation.StartDate,
                EndDate = occupation.EndDate
            }));

            await db.SaveChangesAsync();

            return Results.Ok(new ReservationResponse(reservation));
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

    private static async Task<Owner?> ResolveOwner(int? ownerId, string ownerName, KennelDb db, Dictionary<string, string> errors)
    {
        if (ownerId.HasValue)
        {
            var owner = await db.Owners.FindAsync(ownerId.Value);
            if (owner is null)
                errors["ownerId"] = "Owner does not exist.";

            return owner;
        }

        var existingOwner = await db.Owners.SingleOrDefaultAsync(owner => owner.Name == ownerName);
        if (existingOwner is not null)
            return existingOwner;

        return new Owner { Name = ownerName };
    }

    private static async Task<Dog?> ResolveDog(int? dogId, string dogName, Owner? owner, KennelDb db, Dictionary<string, string> errors)
    {
        if (dogId.HasValue)
        {
            var dog = await db.Dogs
                .Include(d => d.Owner)
                .SingleOrDefaultAsync(d => d.Id == dogId.Value);

            if (dog is null)
            {
                errors["dogId"] = "Dog does not exist.";
                return null;
            }

            if (owner is not null && dog.OwnerId != owner.Id)
                errors["dogId"] = "Dog does not belong to the selected owner.";

            return dog;
        }

        if (owner is null)
            return null;

        if (owner.Id != 0)
        {
            var existingDog = await db.Dogs
                .Include(dog => dog.Owner)
                .SingleOrDefaultAsync(dog => dog.OwnerId == owner.Id && dog.Name == dogName);
            if (existingDog is not null)
                return existingDog;
        }

        return new Dog { Name = dogName, Owner = owner };
    }

    private static async Task ValidateKennelsExist(
        IReadOnlyList<CreateOccupationRequest> occupations,
        KennelDb db,
        Dictionary<string, string> errors)
    {
        var requestedKennelIds = occupations.Select(occupation => occupation.KennelId).Distinct().ToList();
        if (requestedKennelIds.Count == 0)
            return;

        var existingKennelIds = await db.Kennels
            .Where(kennel => requestedKennelIds.Contains(kennel.Id))
            .Select(kennel => kennel.Id)
            .ToListAsync();

        var missingKennelId = requestedKennelIds.Except(existingKennelIds).FirstOrDefault();
        if (missingKennelId != 0)
            errors["occupations"] = $"Kennel {missingKennelId} does not exist.";
    }

    private static void ValidateOccupationCoverage(
        DateOnly reservationStart,
        DateOnly reservationEnd,
        IReadOnlyList<CreateOccupationRequest> occupations,
        Dictionary<string, string> errors)
    {
        if (occupations.Count == 0)
        {
            errors["occupations"] = "At least one occupation is required.";
            return;
        }

        for (var i = 0; i < occupations.Count; i++)
        {
            if (occupations[i].EndDate <= occupations[i].StartDate)
                errors[$"occupations[{i}]"] = "Occupation endDate must be after startDate.";
        }

        if (errors.Count > 0)
            return;

        var expectedStart = reservationStart;
        foreach (var occupation in occupations.OrderBy(occupation => occupation.StartDate).ThenBy(occupation => occupation.EndDate))
        {
            if (occupation.StartDate != expectedStart)
            {
                errors["occupations"] = occupation.StartDate > expectedStart
                    ? "Occupations must fully cover the reservation date range without gaps."
                    : "Occupations must not overlap.";
                return;
            }

            expectedStart = occupation.EndDate;
        }

        if (expectedStart != reservationEnd)
            errors["occupations"] = "Occupations must fully cover the reservation date range without gaps.";
    }

    private static async Task ValidateKennelSharing(
        int? reservationId,
        Dog dog,
        IReadOnlyList<CreateOccupationRequest> requestedOccupations,
        KennelDb db,
        Dictionary<string, string> errors)
    {
        var kennelIds = requestedOccupations.Select(occupation => occupation.KennelId).Distinct().ToList();
        var requestedStart = requestedOccupations.Min(occupation => occupation.StartDate);
        var requestedEnd = requestedOccupations.Max(occupation => occupation.EndDate);

        var overlappingOccupations = await db.Occupations
            .Include(occupation => occupation.Reservation)
            .ThenInclude(reservation => reservation.Dog)
            .Where(occupation => kennelIds.Contains(occupation.KennelId))
            .Where(occupation => !reservationId.HasValue || occupation.ReservationId != reservationId.Value)
            .Where(occupation => occupation.StartDate < requestedEnd && requestedStart < occupation.EndDate)
            .ToListAsync();
        var overlappingDogIds = overlappingOccupations
            .Select(occupation => occupation.Reservation.DogId)
            .OfType<int>()
            .Distinct()
            .ToList();
        var incompatibilities = dog.Id == 0 || overlappingDogIds.Count == 0
            ? []
            : await db.Incompatibilities
                .Where(incompatibility =>
                    (incompatibility.DogId1 == dog.Id && overlappingDogIds.Contains(incompatibility.DogId2)) ||
                    (incompatibility.DogId2 == dog.Id && overlappingDogIds.Contains(incompatibility.DogId1)))
                .ToListAsync();

        var ownerId = dog.OwnerId != 0 ? dog.OwnerId : dog.Owner.Id;

        foreach (var requestedOccupation in requestedOccupations)
        {
            foreach (var existingOccupation in overlappingOccupations)
            {
                if (existingOccupation.KennelId != requestedOccupation.KennelId ||
                    existingOccupation.StartDate >= requestedOccupation.EndDate ||
                    requestedOccupation.StartDate >= existingOccupation.EndDate)
                    continue;

                var existingDog = existingOccupation.Reservation.Dog;
                if (existingDog is null)
                    continue;

                if (existingDog.OwnerId != ownerId)
                {
                    errors["occupations"] = "Dogs from different owners cannot share a kennel.";
                    return;
                }

                var dogsAreIncompatible = incompatibilities.Any(incompatibility =>
                    (incompatibility.DogId1 == dog.Id && incompatibility.DogId2 == existingDog.Id) ||
                    (incompatibility.DogId2 == dog.Id && incompatibility.DogId1 == existingDog.Id));
                if (dogsAreIncompatible)
                {
                    errors["occupations"] = "Incompatible dogs cannot share a kennel.";
                    return;
                }
            }
        }
    }

    private static bool TryParseLocalReservationId(string publicId, out int localId)
    {
        const string prefix = "local:";
        localId = 0;

        return publicId.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(publicId[prefix.Length..], out localId);
    }
}
