using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Entities;

namespace SoftwareLicense.Api.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Licenca> Licencas => Set<Licenca>();
    public DbSet<LicencaValor> LicencaValores => Set<LicencaValor>();
    public DbSet<UsuarioLicenca> UsuarioLicencas => Set<UsuarioLicenca>();
    public DbSet<TipoEquipamento> TiposEquipamento => Set<TipoEquipamento>();
    public DbSet<NotaFiscalEntrada> NotasFiscaisEntrada => Set<NotaFiscalEntrada>();
    public DbSet<NotaFiscalItem> NotasFiscaisItens => Set<NotaFiscalItem>();
    public DbSet<Equipamento> Equipamentos => Set<Equipamento>();
    public DbSet<EquipamentoAlocacao> EquipamentoAlocacoes => Set<EquipamentoAlocacao>();
    public DbSet<EquipamentoAnexo> EquipamentoAnexos => Set<EquipamentoAnexo>();
    public DbSet<NotaFiscalEntradaAnexo> NotaFiscalEntradaAnexos => Set<NotaFiscalEntradaAnexo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.HasOne(a => a.Usuario).WithMany().HasForeignKey(a => a.UsuarioId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(a => a.UsuarioId).IsUnique().HasFilter("\"UsuarioId\" IS NOT NULL");
        });

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

        modelBuilder.Entity<LicencaValor>(entity =>
        {
            entity.Property(v => v.Valor).HasPrecision(18, 2);
            entity.Property(v => v.Periodicidade).IsRequired().HasMaxLength(20);
            entity.HasIndex(v => new { v.LicencaId, v.DataVigenciaInicio });
            entity.HasOne(v => v.Licenca).WithMany().HasForeignKey(v => v.LicencaId).OnDelete(DeleteBehavior.Restrict);
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

        modelBuilder.Entity<TipoEquipamento>(entity =>
        {
            entity.Property(t => t.Nome).IsRequired().HasMaxLength(100);
            entity.HasIndex(t => t.Nome).IsUnique();
        });

        modelBuilder.Entity<NotaFiscalEntrada>(entity =>
        {
            entity.Property(n => n.Numero).IsRequired().HasMaxLength(50);
            entity.Property(n => n.FornecedorNome).HasMaxLength(200);
            entity.Property(n => n.Observacao).HasMaxLength(1000);
            entity.HasIndex(n => n.Numero);
        });

        modelBuilder.Entity<NotaFiscalItem>(entity =>
        {
            entity.Property(i => i.Descricao).HasMaxLength(300);
            entity.Property(i => i.ValorUnitario).HasPrecision(18, 2);
            entity.Property(i => i.Origem).IsRequired().HasMaxLength(20);
            entity.HasIndex(i => i.NotaFiscalEntradaId);
            entity.HasOne(i => i.NotaFiscalEntrada).WithMany(n => n.Itens).HasForeignKey(i => i.NotaFiscalEntradaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(i => i.TipoEquipamento).WithMany().HasForeignKey(i => i.TipoEquipamentoId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Equipamento>(entity =>
        {
            entity.Property(e => e.Marca).HasMaxLength(100);
            entity.Property(e => e.Modelo).HasMaxLength(100);
            entity.Property(e => e.NumeroSerie).HasMaxLength(100);
            entity.Property(e => e.Patrimonio).HasMaxLength(100);
            entity.Property(e => e.Origem).IsRequired().HasMaxLength(20);
            entity.Property(e => e.FornecedorNome).HasMaxLength(200);
            entity.Property(e => e.ValorMensal).HasPrecision(18, 2);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.Property(e => e.NumeroNotaSaida).HasMaxLength(50);
            entity.Property(e => e.Observacao).HasMaxLength(1000);
            entity.HasIndex(e => e.Patrimonio).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.TipoEquipamentoId);
            entity.HasOne(e => e.TipoEquipamento).WithMany().HasForeignKey(e => e.TipoEquipamentoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.NotaFiscalItem).WithMany(i => i.Equipamentos).HasForeignKey(e => e.NotaFiscalItemId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EquipamentoAlocacao>(entity =>
        {
            entity.Property(a => a.Observacao).HasMaxLength(1000);
            entity.HasIndex(a => a.EquipamentoId);
            entity.HasIndex(a => a.UsuarioId);
            entity.HasIndex(a => a.DataInicio);
            entity.HasIndex(a => a.DataFim);
            entity.HasOne(a => a.Equipamento).WithMany().HasForeignKey(a => a.EquipamentoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(a => a.Usuario).WithMany().HasForeignKey(a => a.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EquipamentoAnexo>(entity =>
        {
            entity.Property(a => a.NomeArquivo).IsRequired().HasMaxLength(255);
            entity.Property(a => a.TipoConteudo).IsRequired().HasMaxLength(100);
            entity.HasIndex(a => a.EquipamentoId);
            entity.HasOne(a => a.Equipamento).WithMany().HasForeignKey(a => a.EquipamentoId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<NotaFiscalEntradaAnexo>(entity =>
        {
            entity.Property(a => a.NomeArquivo).IsRequired().HasMaxLength(255);
            entity.Property(a => a.TipoConteudo).IsRequired().HasMaxLength(100);
            entity.HasIndex(a => a.NotaFiscalEntradaId);
            entity.HasOne(a => a.NotaFiscalEntrada).WithMany().HasForeignKey(a => a.NotaFiscalEntradaId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
