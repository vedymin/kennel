using Microsoft.EntityFrameworkCore;

public class KennelDb(DbContextOptions<KennelDb> options) : DbContext(options)
{
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<GoogleConnection> GoogleConnections => Set<GoogleConnection>();
    public DbSet<Owner> Owners => Set<Owner>();
    public DbSet<Dog> Dogs => Set<Dog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Owner>(entity =>
        {
            entity.Property(owner => owner.Name)
                .IsRequired()
                .UseCollation("NOCASE");

            entity.HasIndex(owner => owner.Name)
                .IsUnique();
        });

        modelBuilder.Entity<Dog>(entity =>
        {
            entity.Property(dog => dog.Name)
                .IsRequired()
                .UseCollation("NOCASE");

            entity.HasIndex(dog => new { dog.Name, dog.OwnerId })
                .IsUnique();

            entity.HasOne(dog => dog.Owner)
                .WithMany(owner => owner.Dogs)
                .HasForeignKey(dog => dog.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.HasOne(reservation => reservation.Dog)
                .WithMany(dog => dog.Reservations)
                .HasForeignKey(reservation => reservation.DogId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
