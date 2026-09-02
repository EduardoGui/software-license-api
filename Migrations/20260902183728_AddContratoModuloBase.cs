using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddContratoModuloBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Contratos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Numero = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FornecedorId = table.Column<int>(type: "integer", nullable: false),
                    Objeto = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Natureza = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DataAssinatura = table.Column<DateOnly>(type: "date", nullable: false),
                    DataInicioVigencia = table.Column<DateOnly>(type: "date", nullable: false),
                    DataFimVigenciaOriginal = table.Column<DateOnly>(type: "date", nullable: false),
                    ValorOriginal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Observacoes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contratos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contratos_Fornecedores_FornecedorId",
                        column: x => x.FornecedorId,
                        principalTable: "Fornecedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContratoAnexos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContratoId = table.Column<int>(type: "integer", nullable: false),
                    NomeArquivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    TipoConteudo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Tamanho = table.Column<long>(type: "bigint", nullable: false),
                    Conteudo = table.Column<byte[]>(type: "bytea", nullable: false),
                    DataUpload = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContratoAnexos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContratoAnexos_Contratos_ContratoId",
                        column: x => x.ContratoId,
                        principalTable: "Contratos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContratoFaturamentoConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContratoId = table.Column<int>(type: "integer", nullable: false),
                    DiaInicialJanelaNf = table.Column<int>(type: "integer", nullable: false),
                    DiaFinalJanelaNf = table.Column<int>(type: "integer", nullable: false),
                    ExigeBmAprovado = table.Column<bool>(type: "boolean", nullable: false),
                    ExigeBmAssinado = table.Column<bool>(type: "boolean", nullable: false),
                    PrazoPagamentoDias = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContratoFaturamentoConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContratoFaturamentoConfigs_Contratos_ContratoId",
                        column: x => x.ContratoId,
                        principalTable: "Contratos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContratoItens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContratoId = table.Column<int>(type: "integer", nullable: false),
                    Codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Unidade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    QuantidadeContratada = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    ValorUnitario = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContratoItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContratoItens_Contratos_ContratoId",
                        column: x => x.ContratoId,
                        principalTable: "Contratos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContratoMedicaoConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContratoId = table.Column<int>(type: "integer", nullable: false),
                    TipoMedicao = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DiaInicioPeriodo = table.Column<int>(type: "integer", nullable: true),
                    DiaFimPeriodo = table.Column<int>(type: "integer", nullable: true),
                    ExigeBm = table.Column<bool>(type: "boolean", nullable: false),
                    ExigeAprovacao = table.Column<bool>(type: "boolean", nullable: false),
                    ExigeAssinatura = table.Column<bool>(type: "boolean", nullable: false),
                    PermiteProRata = table.Column<bool>(type: "boolean", nullable: false),
                    MetodoProRata = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    DiasAntecedenciaAlerta = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContratoMedicaoConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContratoMedicaoConfigs_Contratos_ContratoId",
                        column: x => x.ContratoId,
                        principalTable: "Contratos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContratoAnexos_ContratoId",
                table: "ContratoAnexos",
                column: "ContratoId");

            migrationBuilder.CreateIndex(
                name: "IX_ContratoFaturamentoConfigs_ContratoId",
                table: "ContratoFaturamentoConfigs",
                column: "ContratoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContratoItens_ContratoId",
                table: "ContratoItens",
                column: "ContratoId");

            migrationBuilder.CreateIndex(
                name: "IX_ContratoMedicaoConfigs_ContratoId",
                table: "ContratoMedicaoConfigs",
                column: "ContratoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contratos_FornecedorId",
                table: "Contratos",
                column: "FornecedorId");

            migrationBuilder.CreateIndex(
                name: "IX_Contratos_Numero",
                table: "Contratos",
                column: "Numero",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContratoAnexos");

            migrationBuilder.DropTable(
                name: "ContratoFaturamentoConfigs");

            migrationBuilder.DropTable(
                name: "ContratoItens");

            migrationBuilder.DropTable(
                name: "ContratoMedicaoConfigs");

            migrationBuilder.DropTable(
                name: "Contratos");
        }
    }
}
