using Microsoft.EntityFrameworkCore;

public class KennelDb(DbContextOptions<KennelDb> options) : DbContext(options)
{
    public DbSet<Reservation> Reservations => Set<Reservation>();
}
