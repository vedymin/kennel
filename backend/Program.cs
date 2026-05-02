using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<KennelDb>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=kennel.db"));
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDev", policy =>
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});
builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<KennelDb>().Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("FrontendDev");

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
    return Results.Created($"/api/reservations/{reservation.Id}", response);
});

app.MapGet("/api/reservations", async (KennelDb db) =>
{
    var reservations = await db.Reservations
        .OrderBy(r => r.StartDate)
        .Select(r => new ReservationResponse(r))
        .ToListAsync();

    return Results.Ok(reservations);
});

app.Run();

public class KennelDb(DbContextOptions<KennelDb> options) : DbContext(options)
{
    public DbSet<Reservation> Reservations => Set<Reservation>();
}

public class Reservation
{
    public int Id { get; set; }
    public string DogName { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

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

public partial class Program { }
