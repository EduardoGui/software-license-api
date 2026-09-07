using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddObrigacaoModuloBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Obrigacoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TipoMovimento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MedicaoBmId = table.Column<int>(type: "integer", nullable: true),
                    OrdemCompraId = table.Column<int>(type: "integer", nullable: true),
                    DespesaAvulsaId = table.Column<int>(type: "integer", nullable: true),
                    FornecedorId = table.Column<int>(type: "integer", nullable: false),
                    Competencia = table.Column<DateOnly>(type: "date", nullable: false),
                    ValorPrevisto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DataNf = table.Column<DateOnly>(type: "date", nullable: true),
                    NumeroNf = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ValorNota = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Vencimento = table.Column<DateOnly>(type: "date", nullable: true),
                    DataRequisicao = table.Column<DateOnly>(type: "date", nullable: true),
                    DataAssistPgto = table.Column<DateOnly>(type: "date", nullable: true),
                    DataEnvioFinanceiro = table.Column<DateOnly>(type: "date", nullable: true),
                    DataPrevistaPagamento = table.Column<DateOnly>(type: "date", nullable: true),
                    Pago = table.Column<bool>(type: "boolean", nullable: false),
                    Cancelada = table.Column<bool>(type: "boolean", nullable: false),
                    Observacoes = table.Column<string>(type: "text", nullable: true),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Obrigacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Obrigacoes_DespesasAvulsas_DespesaAvulsaId",
                        column: x => x.DespesaAvulsaId,
                        principalTable: "DespesasAvulsas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Obrigacoes_Fornecedores_FornecedorId",
                        column: x => x.FornecedorId,
                        principalTable: "Fornecedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Obrigacoes_MedicaoBms_MedicaoBmId",
                        column: x => x.MedicaoBmId,
                        principalTable: "MedicaoBms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Obrigacoes_OrdensCompra_OrdemCompraId",
                        column: x => x.OrdemCompraId,
                        principalTable: "OrdensCompra",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Obrigacoes_Competencia",
                table: "Obrigacoes",
                column: "Competencia");

            migrationBuilder.CreateIndex(
                name: "IX_Obrigacoes_DespesaAvulsaId",
                table: "Obrigacoes",
                column: "DespesaAvulsaId",
                unique: true,
                filter: "\"DespesaAvulsaId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Obrigacoes_FornecedorId",
                table: "Obrigacoes",
                column: "FornecedorId");

            migrationBuilder.CreateIndex(
                name: "IX_Obrigacoes_MedicaoBmId",
                table: "Obrigacoes",
                column: "MedicaoBmId",
                unique: true,
                filter: "\"MedicaoBmId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Obrigacoes_OrdemCompraId",
                table: "Obrigacoes",
                column: "OrdemCompraId",
                unique: true,
                filter: "\"OrdemCompraId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Obrigacoes");
        }
    }
}
