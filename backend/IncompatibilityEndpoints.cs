using Microsoft.EntityFrameworkCore;

public static class IncompatibilityEndpoints
{
    public static IEndpointRouteBuilder MapIncompatibilityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/incompatibilities", async (CreateIncompatibilityRequest req, KennelDb db) =>
        {
            var dogs = await db.Dogs
                .Where(dog => dog.Id == req.DogId1 || dog.Id == req.DogId2)
                .ToListAsync();

            var dog1 = dogs.SingleOrDefault(dog => dog.Id == req.DogId1);
            var dog2 = dogs.SingleOrDefault(dog => dog.Id == req.DogId2);
            var errors = new Dictionary<string, string>();

            if (dog1 is null || dog2 is null)
                errors["dogs"] = "Both dogs must exist.";
            else if (dog1.OwnerId != dog2.OwnerId)
                errors["dogs"] = "Dogs must belong to the same owner.";

            if (errors.Count > 0)
                return Results.BadRequest(new { errors });

            var incompatibility = new Incompatibility
            {
                DogId1 = req.DogId1,
                DogId2 = req.DogId2
            };
            db.Incompatibilities.Add(incompatibility);
            await db.SaveChangesAsync();

            var response = new IncompatibilityResponse(incompatibility);
            return Results.Created($"/api/incompatibilities/{response.Id}", response);
        });

        app.MapDelete("/api/incompatibilities/{id:int}", async (int id, KennelDb db) =>
        {
            var incompatibility = await db.Incompatibilities.FindAsync(id);

            if (incompatibility is null)
                return Results.NotFound();

            db.Incompatibilities.Remove(incompatibility);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        return app;
    }
}
