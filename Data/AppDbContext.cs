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
    public DbSet<EmpresaPj> EmpresasPj => Set<EmpresaPj>();
    public DbSet<Dependente> Dependentes => Set<Dependente>();
    public DbSet<PlanoSaudeCusto> PlanoSaudeCustos => Set<PlanoSaudeCusto>();
    public DbSet<NotaDebitoPj> NotasDebitoPj => Set<NotaDebitoPj>();
    public DbSet<NotaDebitoPjAnexo> NotasDebitoPjAnexos => Set<NotaDebitoPjAnexo>();
    public DbSet<Fornecedor> Fornecedores => Set<Fornecedor>();
    public DbSet<Contrato> Contratos => Set<Contrato>();
    public DbSet<ContratoItem> ContratoItens => Set<ContratoItem>();
    public DbSet<ContratoMedicaoConfig> ContratoMedicaoConfigs => Set<ContratoMedicaoConfig>();
    public DbSet<ContratoFaturamentoConfig> ContratoFaturamentoConfigs => Set<ContratoFaturamentoConfig>();
    public DbSet<ContratoAnexo> ContratoAnexos => Set<ContratoAnexo>();
    public DbSet<Aditivo> Aditivos => Set<Aditivo>();
    public DbSet<AditivoItem> AditivoItens => Set<AditivoItem>();
    public DbSet<MedicaoBm> MedicaoBms => Set<MedicaoBm>();
    public DbSet<MedicaoBmItem> MedicaoBmItens => Set<MedicaoBmItem>();
    public DbSet<MedicaoBmAnexo> MedicaoBmAnexos => Set<MedicaoBmAnexo>();
    public DbSet<MedicaoBmAcerto> MedicaoBmAcertos => Set<MedicaoBmAcerto>();
    public DbSet<MedicaoBmImposto> MedicaoBmImpostos => Set<MedicaoBmImposto>();

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
            entity.Property(u => u.Tipo).HasMaxLength(20);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasOne(u => u.Setor).WithMany().HasForeignKey(u => u.SetorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(u => u.EmpresaPj).WithMany().HasForeignKey(u => u.EmpresaPjId).OnDelete(DeleteBehavior.Restrict);
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

        modelBuilder.Entity<EmpresaPj>(entity =>
        {
            entity.Property(e => e.RazaoSocial).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Cnpj).IsRequired().HasMaxLength(20);
            entity.HasIndex(e => e.Cnpj).IsUnique();
        });

        modelBuilder.Entity<Dependente>(entity =>
        {
            entity.Property(d => d.Nome).IsRequired().HasMaxLength(200);
            entity.HasIndex(d => d.UsuarioId);
            entity.HasOne(d => d.Usuario).WithMany().HasForeignKey(d => d.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PlanoSaudeCusto>(entity =>
        {
            entity.Property(p => p.ValorMensal).HasPrecision(18, 2);
            entity.Property(p => p.ValorCoparticipacao).HasPrecision(18, 2);
            entity.HasIndex(p => new { p.UsuarioId, p.Ano, p.Mes });
            entity.HasIndex(p => p.DependenteId);
            entity.HasOne(p => p.Usuario).WithMany().HasForeignKey(p => p.UsuarioId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(p => p.Dependente).WithMany().HasForeignKey(p => p.DependenteId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<NotaDebitoPj>(entity =>
        {
            entity.Property(n => n.ValorBruto).HasPrecision(18, 2);
            entity.Property(n => n.Desconto).HasPrecision(18, 2);
            entity.Property(n => n.RetencaoTributaria).HasPrecision(18, 2);
            entity.Property(n => n.OperadoraSaude).IsRequired().HasMaxLength(100);
            entity.Property(n => n.NumeroDocumento).HasMaxLength(50);
            entity.Property(n => n.Descricao).HasMaxLength(500);
            entity.Property(n => n.FormaPagamento).HasMaxLength(50);
            entity.Property(n => n.CentroCusto).HasMaxLength(100);
            entity.Property(n => n.Area).HasMaxLength(100);
            entity.Property(n => n.ContaContabil).HasMaxLength(100);
            entity.Property(n => n.ProjetoContrato).HasMaxLength(100);
            entity.Property(n => n.Status).IsRequired().HasMaxLength(20);
            entity.HasIndex(n => new { n.UsuarioId, n.Ano, n.Mes }).IsUnique();
            entity.HasOne(n => n.Usuario).WithMany().HasForeignKey(n => n.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<NotaDebitoPjAnexo>(entity =>
        {
            entity.Property(a => a.NomeArquivo).IsRequired().HasMaxLength(255);
            entity.Property(a => a.TipoConteudo).IsRequired().HasMaxLength(100);
            entity.HasIndex(a => a.NotaDebitoPjId);
            entity.HasOne(a => a.NotaDebitoPj).WithMany().HasForeignKey(a => a.NotaDebitoPjId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Licenca>(entity =>
        {
            entity.Property(l => l.Nome).IsRequired().HasMaxLength(200);
            entity.Property(l => l.Tipo).HasMaxLength(100);
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

        modelBuilder.Entity<Fornecedor>(entity =>
        {
            entity.Property(f => f.Nome).IsRequired().HasMaxLength(200);
            entity.Property(f => f.Cnpj).HasMaxLength(20);
            entity.HasIndex(f => f.Nome).IsUnique();
            entity.HasIndex(f => f.Cnpj).IsUnique().HasFilter("\"Cnpj\" IS NOT NULL");
        });

        modelBuilder.Entity<NotaFiscalEntrada>(entity =>
        {
            entity.Property(n => n.Numero).IsRequired().HasMaxLength(50);
            entity.Property(n => n.Observacao).HasMaxLength(1000);
            entity.HasIndex(n => n.Numero);
            entity.HasOne(n => n.Fornecedor).WithMany().HasForeignKey(n => n.FornecedorId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Contrato>(entity =>
        {
            entity.Property(c => c.Numero).IsRequired().HasMaxLength(200);
            entity.Property(c => c.Objeto).IsRequired().HasMaxLength(500);
            entity.Property(c => c.Natureza).HasMaxLength(100);
            entity.Property(c => c.ValorOriginal).HasPrecision(18, 2);
            entity.Property(c => c.Status).IsRequired().HasMaxLength(20);
            entity.Property(c => c.Observacoes).HasMaxLength(2000);
            entity.HasIndex(c => c.Numero).IsUnique();
            entity.HasOne(c => c.Fornecedor).WithMany().HasForeignKey(c => c.FornecedorId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ContratoItem>(entity =>
        {
            entity.Property(i => i.Codigo).HasMaxLength(50);
            entity.Property(i => i.Descricao).IsRequired().HasMaxLength(300);
            entity.Property(i => i.Unidade).IsRequired().HasMaxLength(20);
            entity.Property(i => i.QuantidadeContratada).HasPrecision(18, 6);
            entity.Property(i => i.ValorUnitario).HasPrecision(18, 2);
            entity.HasIndex(i => i.ContratoId);
            entity.HasOne(i => i.Contrato).WithMany(c => c.Itens).HasForeignKey(i => i.ContratoId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ContratoMedicaoConfig>(entity =>
        {
            entity.Property(m => m.TipoMedicao).IsRequired().HasMaxLength(30);
            entity.Property(m => m.MetodoProRata).HasMaxLength(30);
            entity.HasIndex(m => m.ContratoId).IsUnique();
            entity.HasOne(m => m.Contrato).WithOne().HasForeignKey<ContratoMedicaoConfig>(m => m.ContratoId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ContratoFaturamentoConfig>(entity =>
        {
            entity.HasIndex(f => f.ContratoId).IsUnique();
            entity.HasOne(f => f.Contrato).WithOne().HasForeignKey<ContratoFaturamentoConfig>(f => f.ContratoId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ContratoAnexo>(entity =>
        {
            entity.Property(a => a.NomeArquivo).IsRequired().HasMaxLength(255);
            entity.Property(a => a.TipoConteudo).IsRequired().HasMaxLength(100);
            entity.HasIndex(a => a.ContratoId);
            entity.HasOne(a => a.Contrato).WithMany().HasForeignKey(a => a.ContratoId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Aditivo>(entity =>
        {
            entity.Property(a => a.Descricao).IsRequired().HasMaxLength(500);
            entity.Property(a => a.DeltaValor).HasPrecision(18, 2);
            entity.Property(a => a.PercentualReajuste).HasPrecision(9, 4);
            entity.Property(a => a.Status).IsRequired().HasMaxLength(20);
            entity.Property(a => a.Observacao).HasMaxLength(2000);
            entity.HasIndex(a => new { a.ContratoId, a.Numero }).IsUnique();
            entity.HasOne(a => a.Contrato).WithMany().HasForeignKey(a => a.ContratoId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AditivoItem>(entity =>
        {
            entity.Property(i => i.DescricaoNovoItem).HasMaxLength(300);
            entity.Property(i => i.CodigoNovoItem).HasMaxLength(50);
            entity.Property(i => i.UnidadeNovoItem).HasMaxLength(20);
            entity.Property(i => i.DeltaQuantidade).HasPrecision(18, 6);
            entity.Property(i => i.NovoValorUnitario).HasPrecision(18, 2);
            entity.HasIndex(i => i.AditivoId);
            entity.HasOne(i => i.Aditivo).WithMany(a => a.Itens).HasForeignKey(i => i.AditivoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(i => i.ContratoItem).WithMany().HasForeignKey(i => i.ContratoItemId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MedicaoBm>(entity =>
        {
            entity.Property(m => m.Status).IsRequired().HasMaxLength(30);
            entity.Property(m => m.NumeroReferencia).HasMaxLength(50);
            entity.Property(m => m.ObservacaoAprovador).HasMaxLength(2000);
            entity.Property(m => m.ValorTotalMedido).HasPrecision(18, 2);
            entity.Property(m => m.Observacao).HasMaxLength(2000);
            entity.HasIndex(m => new { m.ContratoId, m.Numero }).IsUnique();
            entity.HasOne(m => m.Contrato).WithMany().HasForeignKey(m => m.ContratoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(m => m.Aprovador).WithMany().HasForeignKey(m => m.AprovadorId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MedicaoBmItem>(entity =>
        {
            entity.Property(i => i.DescricaoNoMomento).IsRequired().HasMaxLength(300);
            entity.Property(i => i.UnidadeNoMomento).IsRequired().HasMaxLength(20);
            entity.Property(i => i.QuantidadeContratadaNoMomento).HasPrecision(18, 6);
            entity.Property(i => i.QuantidadeJaMedidaAntes).HasPrecision(18, 6);
            entity.Property(i => i.SaldoAntes).HasPrecision(18, 6);
            entity.Property(i => i.QuantidadeMedidaNestaBm).HasPrecision(18, 6);
            entity.Property(i => i.SaldoDepois).HasPrecision(18, 6);
            entity.Property(i => i.ValorUnitarioNoMomento).HasPrecision(18, 2);
            entity.Property(i => i.ValorTotalItem).HasPrecision(18, 2);
            entity.Property(i => i.SaldoValorAntes).HasPrecision(18, 2);
            entity.Property(i => i.SaldoValorDepois).HasPrecision(18, 2);
            entity.Property(i => i.PercentualProRata).HasPrecision(9, 4);
            entity.Property(i => i.AjusteManual).HasPrecision(18, 2);
            entity.Property(i => i.JustificativaAjuste).HasMaxLength(1000);
            entity.HasIndex(i => i.MedicaoBmId);
            entity.HasOne(i => i.MedicaoBm).WithMany(m => m.Itens).HasForeignKey(i => i.MedicaoBmId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(i => i.ContratoItem).WithMany().HasForeignKey(i => i.ContratoItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(i => i.AditivoItem).WithMany().HasForeignKey(i => i.AditivoItemId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MedicaoBmAnexo>(entity =>
        {
            entity.Property(a => a.NomeArquivo).IsRequired().HasMaxLength(255);
            entity.Property(a => a.TipoConteudo).IsRequired().HasMaxLength(100);
            entity.HasIndex(a => a.MedicaoBmId);
            entity.HasOne(a => a.MedicaoBm).WithMany().HasForeignKey(a => a.MedicaoBmId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MedicaoBmAcerto>(entity =>
        {
            entity.Property(a => a.Descricao).IsRequired().HasMaxLength(300);
            entity.Property(a => a.Unidade).HasMaxLength(20);
            entity.Property(a => a.Quantidade).HasPrecision(18, 6);
            entity.Property(a => a.PrecoUnitario).HasPrecision(18, 2);
            entity.Property(a => a.PrecoTotal).HasPrecision(18, 2);
            entity.HasIndex(a => a.MedicaoBmId);
            entity.HasOne(a => a.MedicaoBm).WithMany(m => m.Acertos).HasForeignKey(a => a.MedicaoBmId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(a => a.MedicaoBmItem).WithMany().HasForeignKey(a => a.MedicaoBmItemId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MedicaoBmImposto>(entity =>
        {
            entity.Property(i => i.Descricao).IsRequired().HasMaxLength(100);
            entity.Property(i => i.Aliquota).HasPrecision(9, 4);
            entity.Property(i => i.Base).HasPrecision(18, 2);
            entity.Property(i => i.ValorTotal).HasPrecision(18, 2);
            entity.HasIndex(i => i.MedicaoBmId);
            entity.HasOne(i => i.MedicaoBm).WithMany(m => m.Impostos).HasForeignKey(i => i.MedicaoBmId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(i => i.MedicaoBmItem).WithMany().HasForeignKey(i => i.MedicaoBmItemId).OnDelete(DeleteBehavior.Restrict);
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
