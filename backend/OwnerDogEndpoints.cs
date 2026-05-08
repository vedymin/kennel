using Microsoft.EntityFrameworkCore;

public static class OwnerDogEndpoints
{
    public static IEndpointRouteBuilder MapOwnerDogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/owners", async (string? search, KennelDb db) =>
        {
            var query = db.Owners.AsNoTracking();
            var trimmedSearch = search?.Trim();

            if (!string.IsNullOrEmpty(trimmedSearch))
            {
                var pattern = $"%{trimmedSearch}%";
                query = query.Where(owner => EF.Functions.Like(owner.Name, pattern));
            }

            var owners = await query
                .OrderBy(owner => owner.Name)
                .Select(owner => new OwnerResponse(owner.Id, owner.Name))
                .ToListAsync();

            return Results.Ok(owners);
        });

        app.MapPost("/api/owners", async (CreateOwnerRequest req, KennelDb db) =>
        {
            var errors = new Dictionary<string, string>();
            var trimmedName = req.Name?.Trim() ?? "";

            if (string.IsNullOrEmpty(trimmedName))
                errors["name"] = "Owner name is required.";
            else if (await db.Owners.AnyAsync(owner => owner.Name == trimmedName))
                errors["name"] = "Owner name must be unique.";

            if (errors.Count > 0)
                return Results.BadRequest(new { errors });

            var owner = new Owner { Name = trimmedName };
            db.Owners.Add(owner);
            await db.SaveChangesAsync();

            var response = new OwnerResponse(owner);
            return Results.Created($"/api/owners/{owner.Id}", response);
        });

        app.MapGet("/api/owners/{ownerId:int}/dogs", async (int ownerId, KennelDb db) =>
        {
            var dogs = await db.Dogs
                .AsNoTracking()
                .Where(dog => dog.OwnerId == ownerId)
                .OrderBy(dog => dog.Name)
                .Select(dog => new DogResponse(dog.Id, dog.Name, dog.OwnerId))
                .ToListAsync();

            return Results.Ok(dogs);
        });

        app.MapPost("/api/dogs", async (CreateDogRequest req, KennelDb db) =>
        {
            var errors = new Dictionary<string, string>();
            var trimmedName = req.Name?.Trim() ?? "";

            if (string.IsNullOrEmpty(trimmedName))
            {
                errors["name"] = "Dog name is required.";
            }
            else
            {
                var ownerExists = await db.Owners.AnyAsync(owner => owner.Id == req.OwnerId);
                if (!ownerExists)
                    errors["ownerId"] = "Owner does not exist.";
                else if (await db.Dogs.AnyAsync(dog => dog.OwnerId == req.OwnerId && dog.Name == trimmedName))
                    errors["name"] = "Dog name must be unique for this owner.";
            }

            if (errors.Count > 0)
                return Results.BadRequest(new { errors });

            var dog = new Dog { Name = trimmedName, OwnerId = req.OwnerId };
            db.Dogs.Add(dog);
            await db.SaveChangesAsync();

            var response = new DogResponse(dog);
            return Results.Created($"/api/dogs/{dog.Id}", response);
        });

        return app;
    }
}
