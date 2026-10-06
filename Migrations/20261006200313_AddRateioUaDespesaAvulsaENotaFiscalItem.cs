using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRateioUaDespesaAvulsaENotaFiscalItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DespesaAvulsaRateiosUa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DespesaAvulsaId = table.Column<int>(type: "integer", nullable: false),
                    UnidadeOrcamentariaId = table.Column<int>(type: "integer", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DespesaAvulsaRateiosUa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DespesaAvulsaRateiosUa_DespesasAvulsas_DespesaAvulsaId",
                        column: x => x.DespesaAvulsaId,
                        principalTable: "DespesasAvulsas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DespesaAvulsaRateiosUa_UnidadesOrcamentarias_UnidadeOrcamen~",
                        column: x => x.UnidadeOrcamentariaId,
                        principalTable: "UnidadesOrcamentarias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotaFiscalItemRateiosUa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NotaFiscalItemId = table.Column<int>(type: "integer", nullable: false),
                    UnidadeOrcamentariaId = table.Column<int>(type: "integer", nullable: false),
                    Quantidade = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotaFiscalItemRateiosUa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotaFiscalItemRateiosUa_NotasFiscaisItens_NotaFiscalItemId",
                        column: x => x.NotaFiscalItemId,
                        principalTable: "NotasFiscaisItens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotaFiscalItemRateiosUa_UnidadesOrcamentarias_UnidadeOrcame~",
                        column: x => x.UnidadeOrcamentariaId,
                        principalTable: "UnidadesOrcamentarias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DespesaAvulsaRateiosUa_DespesaAvulsaId_UnidadeOrcamentariaId",
                table: "DespesaAvulsaRateiosUa",
                columns: new[] { "DespesaAvulsaId", "UnidadeOrcamentariaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DespesaAvulsaRateiosUa_UnidadeOrcamentariaId",
                table: "DespesaAvulsaRateiosUa",
                column: "UnidadeOrcamentariaId");

            migrationBuilder.CreateIndex(
                name: "IX_NotaFiscalItemRateiosUa_NotaFiscalItemId_UnidadeOrcamentari~",
                table: "NotaFiscalItemRateiosUa",
                columns: new[] { "NotaFiscalItemId", "UnidadeOrcamentariaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotaFiscalItemRateiosUa_UnidadeOrcamentariaId",
                table: "NotaFiscalItemRateiosUa",
                column: "UnidadeOrcamentariaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DespesaAvulsaRateiosUa");

            migrationBuilder.DropTable(
                name: "NotaFiscalItemRateiosUa");
        }
    }
}
