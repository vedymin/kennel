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
builder.Services.AddDataProtection();
builder.Services.AddSingleton<IGoogleOAuthService, GoogleOAuthService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped(_ => new GoogleCalendarEventMapper(TimeZoneInfo.Local));
builder.Services.AddHttpClient<IGoogleCalendarReservationSource, GoogleCalendarReservationSource>();
builder.Services.AddScoped<ILocalReservationSource, LocalReservationSource>();
builder.Services.AddScoped<IReservationAggregationService, ReservationAggregationService>();
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

app.MapReservationEndpoints();
app.MapOwnerDogEndpoints();
app.MapGoogleOAuthEndpoints();

app.Run();

public partial class Program { }
