using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Entities;

namespace SoftwareLicense.Api.Data;

public class AppDbContext : IdentityDbContext<IdentityUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Licenca> Licencas => Set<Licenca>();
    public DbSet<UsuarioLicenca> UsuarioLicencas => Set<UsuarioLicenca>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.Property(u => u.Nome).IsRequired().HasMaxLength(200);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(200);
            entity.Property(u => u.Observacao).HasMaxLength(1000);
            entity.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<Licenca>(entity =>
        {
            entity.Property(l => l.Nome).IsRequired().HasMaxLength(200);
            entity.Property(l => l.Descricao).HasMaxLength(1000);
            entity.Property(l => l.Observacao).HasMaxLength(1000);
        });

        modelBuilder.Entity<UsuarioLicenca>(entity =>
        {
            entity.Property(m => m.Observacao).HasMaxLength(1000);
            entity.HasIndex(m => m.UsuarioId);
            entity.HasIndex(m => m.LicencaId);
            entity.HasIndex(m => m.DataInicio);
            entity.HasIndex(m => m.DataFim);
            entity.HasOne(m => m.Usuario).WithMany().HasForeignKey(m => m.UsuarioId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(m => m.Licenca).WithMany().HasForeignKey(m => m.LicencaId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
