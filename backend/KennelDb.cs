using Microsoft.EntityFrameworkCore;

public class KennelDb(DbContextOptions<KennelDb> options) : DbContext(options)
{
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<GoogleConnection> GoogleConnections => Set<GoogleConnection>();
    public DbSet<Owner> Owners => Set<Owner>();
    public DbSet<Dog> Dogs => Set<Dog>();
    public DbSet<Domain.Kennel> Kennels => Set<Domain.Kennel>();
    public DbSet<Occupation> Occupations => Set<Occupation>();
    public DbSet<Incompatibility> Incompatibilities => Set<Incompatibility>();

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

        modelBuilder.Entity<Domain.Kennel>(entity =>
        {
            entity.Property(kennel => kennel.Name)
                .IsRequired();
        });

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.HasOne(reservation => reservation.Dog)
                .WithMany(dog => dog.Reservations)
                .HasForeignKey(reservation => reservation.DogId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(reservation => reservation.Occupations)
                .WithOne(occupation => occupation.Reservation)
                .HasForeignKey(occupation => occupation.ReservationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Occupation>(entity =>
        {
            entity.HasOne(occupation => occupation.Kennel)
                .WithMany()
                .HasForeignKey(occupation => occupation.KennelId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Incompatibility>(entity =>
        {
            entity.HasOne(incompatibility => incompatibility.Dog1)
                .WithMany()
                .HasForeignKey(incompatibility => incompatibility.DogId1)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(incompatibility => incompatibility.Dog2)
                .WithMany()
                .HasForeignKey(incompatibility => incompatibility.DogId2)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
