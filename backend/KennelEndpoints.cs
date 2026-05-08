using Microsoft.EntityFrameworkCore;

public static class KennelEndpoints
{
    public static IEndpointRouteBuilder MapKennelEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/kennels", async (KennelDb db) =>
        {
            var kennels = await db.Kennels
                .AsNoTracking()
                .OrderBy(kennel => kennel.Name)
                .Select(kennel => new KennelResponse(kennel.Id, kennel.Name))
                .ToListAsync();

            return Results.Ok(kennels);
        });

        app.MapPost("/api/kennels", async (CreateKennelRequest req, KennelDb db) =>
        {
            var trimmedName = req.Name?.Trim() ?? "";
            if (string.IsNullOrEmpty(trimmedName))
                return Results.BadRequest(new { errors = new Dictionary<string, string> { ["name"] = "Kennel name is required." } });

            var kennel = new Domain.Kennel { Name = trimmedName };
            db.Kennels.Add(kennel);
            await db.SaveChangesAsync();

            var response = new KennelResponse(kennel);
            return Results.Created($"/api/kennels/{kennel.Id}", response);
        });

        app.MapPut("/api/kennels/{id:int}", async (int id, RenameKennelRequest req, KennelDb db) =>
        {
            var kennel = await db.Kennels.FindAsync(id);
            if (kennel is null)
                return Results.NotFound();

            var trimmedName = req.Name?.Trim() ?? "";
            if (string.IsNullOrEmpty(trimmedName))
                return Results.BadRequest(new { errors = new Dictionary<string, string> { ["name"] = "Kennel name is required." } });

            kennel.Name = trimmedName;
            await db.SaveChangesAsync();

            return Results.Ok(new KennelResponse(kennel));
        });

        app.MapDelete("/api/kennels/{id:int}", async (int id, KennelDb db) =>
        {
            var kennel = await db.Kennels.FindAsync(id);
            if (kennel is null)
                return Results.NotFound();

            db.Kennels.Remove(kennel);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        return app;
    }
}
