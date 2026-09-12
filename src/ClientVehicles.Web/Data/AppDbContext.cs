using ClientVehicles.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace ClientVehicles.Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Client> Clients => Set<Client>();

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Client>(entity =>
        {
            entity.Property(c => c.Name).IsRequired().HasMaxLength(120);
            entity.Property(c => c.Phone).IsRequired().HasMaxLength(40);
            entity.Property(c => c.PhoneKey).IsRequired().HasMaxLength(40);
            entity.Property(c => c.NameKey).IsRequired().HasMaxLength(120);
            entity.Property(c => c.Address).HasMaxLength(200);
            entity.Property(c => c.TaxNumber).HasMaxLength(30);

            entity.HasIndex(c => c.NameKey);
            entity.HasIndex(c => c.PhoneKey);
            entity.HasIndex(c => c.IsArchived);

            // Database level guard: a client row is not valid without a name and a phone.
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_Clients_RequiredText",
                "length(trim(\"Name\")) > 0 AND length(trim(\"Phone\")) > 0"));
        });

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.Property(v => v.LicensePlate).IsRequired().HasMaxLength(20);
            entity.Property(v => v.LicensePlateKey).IsRequired().HasMaxLength(20);
            entity.Property(v => v.Brand).IsRequired().HasMaxLength(60);
            entity.Property(v => v.Model).IsRequired().HasMaxLength(60);
            entity.Property(v => v.Vin).HasMaxLength(30);
            entity.Property(v => v.VinKey).HasMaxLength(30);

            // A vehicle always belongs to a client, and a client that owns vehicles cannot be
            // deleted out from under them. Deleting is not exposed in the UI (archive instead),
            // but the constraint makes an accidental cascade impossible at the database level.
            entity.HasOne(v => v.Client)
                .WithMany(c => c.Vehicles)
                .HasForeignKey(v => v.ClientId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            // Uniqueness applies to active records only, so a plate belonging to an archived
            // vehicle can be reused by a new one. Partial indexes are supported by SQLite
            // and by PostgreSQL, which is the likely production target.
            entity.HasIndex(v => v.LicensePlateKey)
                .IsUnique()
                .HasFilter("\"IsArchived\" = 0")
                .HasDatabaseName("UX_Vehicles_LicensePlateKey_Active");

            entity.HasIndex(v => v.VinKey)
                .IsUnique()
                .HasFilter("\"IsArchived\" = 0 AND \"VinKey\" IS NOT NULL")
                .HasDatabaseName("UX_Vehicles_VinKey_Active");

            entity.HasIndex(v => v.ClientId);
            entity.HasIndex(v => v.IsArchived);

            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Vehicles_MileageNotNegative", "\"Mileage\" IS NULL OR \"Mileage\" >= 0");
                table.HasCheckConstraint(
                    "CK_Vehicles_RequiredText",
                    "length(trim(\"LicensePlate\")) > 0 AND length(trim(\"Brand\")) > 0 AND length(trim(\"Model\")) > 0");
            });
        });
    }
}
