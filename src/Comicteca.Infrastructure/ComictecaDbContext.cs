using Comicteca.Domain;
using Microsoft.EntityFrameworkCore;

namespace Comicteca.Infrastructure;

public sealed class ComictecaDbContext(DbContextOptions<ComictecaDbContext> options) : DbContext(options)
{
    public DbSet<Serie> Series => Set<Serie>();

    public DbSet<Tomo> Tomos => Set<Tomo>();

    public DbSet<Auditoria> RegistrosDeAuditoria => Set<Auditoria>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Serie>(entidad =>
        {
            entidad.ToTable("Series");
            entidad.HasKey(serie => serie.Id);
            entidad.Property(serie => serie.Titulo).IsRequired().HasMaxLength(Serie.TituloMaximo);
            entidad.Property(serie => serie.Editorial).IsRequired().HasMaxLength(Serie.EditorialMaxima);
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
            entidad.Property(tomo => tomo.Edicion).HasMaxLength(Tomo.EdicionMaxima);
            entidad.HasIndex(tomo => new { tomo.SerieId, tomo.Numero });
        });

        modelBuilder.Entity<Auditoria>(entidad =>
        {
            entidad.ToTable("Auditoria");
            entidad.HasKey(fila => fila.Id);
            entidad.Property(fila => fila.Operacion).IsRequired().HasMaxLength(Auditoria.OperacionMaxima);
            entidad.Property(fila => fila.Entidad).IsRequired().HasMaxLength(Auditoria.EntidadMaxima);
            entidad.Property(fila => fila.Origen).IsRequired().HasMaxLength(Auditoria.OrigenMaximo);
            entidad.Property(fila => fila.Detalle).HasMaxLength(Auditoria.DetalleMaximo);
            entidad.Property(fila => fila.Cuenta).HasMaxLength(Auditoria.CuentaMaxima);
            entidad.HasIndex(fila => fila.FechaUtc);
            entidad.HasIndex(fila => fila.SerieId);
        });
    }
}
