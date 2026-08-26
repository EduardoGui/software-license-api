using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Setor> Setores => Set<Setor>();
    public DbSet<SetorAprovador> SetorAprovadores => Set<SetorAprovador>();
    public DbSet<TipoDespesa> TiposDespesa => Set<TipoDespesa>();
    public DbSet<ReembolsoDespesa> ReembolsosDespesa => Set<ReembolsoDespesa>();
    public DbSet<ReembolsoDespesaItem> ReembolsoDespesaItens => Set<ReembolsoDespesaItem>();
    public DbSet<ReembolsoDespesaItemAnexo> ReembolsoDespesaItemAnexos => Set<ReembolsoDespesaItemAnexo>();
    public DbSet<EmailNotificacaoReembolso> EmailsNotificacaoReembolso => Set<EmailNotificacaoReembolso>();
    public DbSet<Local> Locais => Set<Local>();
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
    public DbSet<TipoPatrimonio> TiposPatrimonio => Set<TipoPatrimonio>();
    public DbSet<PatrimonioItem> PatrimonioItens => Set<PatrimonioItem>();
    public DbSet<PatrimonioItemAnexo> PatrimonioItemAnexos => Set<PatrimonioItemAnexo>();
    public DbSet<LogAuditoria> LogsAuditoria => Set<LogAuditoria>();

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
            entity.Property(u => u.Cpf).HasMaxLength(20);
            entity.Property(u => u.Cargo).HasMaxLength(100);
            entity.Property(u => u.ChavePix).HasMaxLength(200);
            entity.Property(u => u.Banco).HasMaxLength(100);
            entity.Property(u => u.Agencia).HasMaxLength(20);
            entity.Property(u => u.ContaBancaria).HasMaxLength(30);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasOne(u => u.Setor).WithMany().HasForeignKey(u => u.SetorId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Setor>(entity =>
        {
            entity.Property(s => s.Nome).IsRequired().HasMaxLength(100);
            entity.HasIndex(s => s.Nome).IsUnique();
        });

        modelBuilder.Entity<SetorAprovador>(entity =>
        {
            entity.HasIndex(a => new { a.SetorId, a.UsuarioId }).IsUnique();
            entity.HasOne(a => a.Setor).WithMany().HasForeignKey(a => a.SetorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(a => a.Usuario).WithMany().HasForeignKey(a => a.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TipoDespesa>(entity =>
        {
            entity.Property(t => t.Nome).IsRequired().HasMaxLength(100);
            entity.HasIndex(t => t.Nome).IsUnique();
        });

        modelBuilder.Entity<ReembolsoDespesa>(entity =>
        {
            entity.Property(r => r.Finalidade).IsRequired().HasMaxLength(300);
            entity.Property(r => r.FormaPagamento).HasMaxLength(50);
            entity.Property(r => r.Status).IsRequired().HasMaxLength(30);
            entity.Property(r => r.ObservacaoAprovador).HasMaxLength(1000);
            entity.Property(r => r.Observacao).HasMaxLength(1000);
            entity.HasIndex(r => r.UsuarioId);
            entity.HasIndex(r => r.Status);
            entity.HasOne(r => r.Usuario).WithMany().HasForeignKey(r => r.UsuarioId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(r => r.Setor).WithMany().HasForeignKey(r => r.SetorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(r => r.Aprovador).WithMany().HasForeignKey(r => r.AprovadorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(r => r.Local).WithMany().HasForeignKey(r => r.LocalId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReembolsoDespesaItem>(entity =>
        {
            entity.Property(i => i.Descricao).HasMaxLength(300);
            entity.Property(i => i.NumeroDocumento).HasMaxLength(50);
            entity.Property(i => i.Valor).HasPrecision(18, 2);
            entity.HasIndex(i => i.ReembolsoDespesaId);
            entity.HasOne(i => i.ReembolsoDespesa).WithMany(r => r.Itens).HasForeignKey(i => i.ReembolsoDespesaId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(i => i.TipoDespesa).WithMany().HasForeignKey(i => i.TipoDespesaId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReembolsoDespesaItemAnexo>(entity =>
        {
            entity.Property(a => a.NomeArquivo).IsRequired().HasMaxLength(255);
            entity.Property(a => a.TipoConteudo).IsRequired().HasMaxLength(100);
            entity.HasIndex(a => a.ReembolsoDespesaItemId);
            // Cascade (diferente dos outros anexos, que usam Restrict): itens de reembolso são
            // de fato apagados ao editar/excluir o reembolso, então o anexo não pode sobreviver órfão.
            entity.HasOne(a => a.ReembolsoDespesaItem).WithMany(i => i.Anexos).HasForeignKey(a => a.ReembolsoDespesaItemId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EmailNotificacaoReembolso>(entity =>
        {
            entity.Property(e => e.Email).IsRequired().HasMaxLength(200);
            entity.Property(e => e.TipoDestinatario).IsRequired().HasMaxLength(10);
            entity.HasIndex(e => e.Email).IsUnique();
        });

        modelBuilder.Entity<Local>(entity =>
        {
            entity.Property(l => l.Nome).IsRequired().HasMaxLength(100);
            entity.Property(l => l.Endereco).HasMaxLength(300);
            entity.HasIndex(l => l.Nome).IsUnique();
        });

        modelBuilder.Entity<Licenca>(entity =>
        {
            entity.Property(l => l.Nome).IsRequired().HasMaxLength(200);
            entity.Property(l => l.Descricao).HasMaxLength(1000);
            entity.Property(l => l.Observacao).HasMaxLength(1000);
            entity.HasOne(l => l.NotaFiscalEntrada).WithMany().HasForeignKey(l => l.NotaFiscalEntradaId).OnDelete(DeleteBehavior.Restrict);
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
            entity.Property(i => i.Destino).IsRequired().HasMaxLength(20).HasDefaultValue(NotaFiscalItemDestino.Equipamento);
            entity.HasIndex(i => i.NotaFiscalEntradaId);
            entity.HasOne(i => i.NotaFiscalEntrada).WithMany(n => n.Itens).HasForeignKey(i => i.NotaFiscalEntradaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(i => i.TipoEquipamento).WithMany().HasForeignKey(i => i.TipoEquipamentoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(i => i.TipoPatrimonio).WithMany().HasForeignKey(i => i.TipoPatrimonioId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(i => i.Local).WithMany().HasForeignKey(i => i.LocalId).OnDelete(DeleteBehavior.Restrict);
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

        modelBuilder.Entity<TipoPatrimonio>(entity =>
        {
            entity.Property(t => t.Nome).IsRequired().HasMaxLength(100);
            entity.HasIndex(t => t.Nome).IsUnique();
        });

        modelBuilder.Entity<PatrimonioItem>(entity =>
        {
            entity.Property(p => p.Descricao).HasMaxLength(300);
            entity.Property(p => p.NumeroPatrimonio).HasMaxLength(100);
            entity.Property(p => p.Status).IsRequired().HasMaxLength(20);
            entity.Property(p => p.Observacao).HasMaxLength(1000);
            entity.HasIndex(p => p.NumeroPatrimonio).IsUnique().HasFilter("\"NumeroPatrimonio\" IS NOT NULL");
            entity.HasIndex(p => p.Status);
            entity.HasIndex(p => p.TipoPatrimonioId);
            entity.HasOne(p => p.NotaFiscalItem).WithMany(i => i.PatrimonioItens).HasForeignKey(p => p.NotaFiscalItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(p => p.TipoPatrimonio).WithMany().HasForeignKey(p => p.TipoPatrimonioId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(p => p.Local).WithMany().HasForeignKey(p => p.LocalId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PatrimonioItemAnexo>(entity =>
        {
            entity.Property(a => a.NomeArquivo).IsRequired().HasMaxLength(255);
            entity.Property(a => a.TipoConteudo).IsRequired().HasMaxLength(100);
            entity.HasIndex(a => a.PatrimonioItemId);
            entity.HasOne(a => a.PatrimonioItem).WithMany().HasForeignKey(a => a.PatrimonioItemId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LogAuditoria>(entity =>
        {
            entity.Property(l => l.UsuarioNome).IsRequired().HasMaxLength(200);
            entity.Property(l => l.Entidade).IsRequired().HasMaxLength(100);
            entity.Property(l => l.Acao).IsRequired().HasMaxLength(100);
            entity.Property(l => l.Detalhe).HasMaxLength(2000);
            entity.HasIndex(l => l.DataHora);
            entity.HasIndex(l => new { l.Entidade, l.EntidadeId });
        });
    }
}
