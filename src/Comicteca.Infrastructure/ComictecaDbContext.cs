using Comicteca.Domain;
using Microsoft.EntityFrameworkCore;

namespace Comicteca.Infrastructure;

public sealed class ComictecaDbContext(DbContextOptions<ComictecaDbContext> options) : DbContext(options)
{
    public DbSet<Serie> Series => Set<Serie>();

    public DbSet<Tomo> Tomos => Set<Tomo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Serie>(entidad =>
        {
            entidad.ToTable("Series");
            entidad.HasKey(serie => serie.Id);
            entidad.Property(serie => serie.Titulo).IsRequired().HasMaxLength(200);
            entidad.Property(serie => serie.Editorial).IsRequired().HasMaxLength(120);
            entidad.HasIndex(serie => new { serie.Titulo, serie.Editorial }).IsUnique();
            entidad.HasMany(serie => serie.Tomos)
                .WithOne()
                .HasForeignKey(tomo => tomo.SerieId)
                .OnDelete(DeleteBehavior.Cascade);
            entidad.Navigation(serie => serie.Tomos).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<Tomo>(entidad =>
        {
            entidad.ToTable("Tomos");
            entidad.HasKey(tomo => tomo.Id);
            entidad.Property(tomo => tomo.Edicion).HasMaxLength(200);
            entidad.HasIndex(tomo => new { tomo.SerieId, tomo.Numero });
        });
    }
}
