using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddNotaDebitoPjItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotasDebitoPjItens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NotaDebitoPjId = table.Column<int>(type: "integer", nullable: false),
                    DependenteId = table.Column<int>(type: "integer", nullable: true),
                    NomeBeneficiario = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ValorMensalidade = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorCoparticipacao = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotasDebitoPjItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotasDebitoPjItens_Dependentes_DependenteId",
                        column: x => x.DependenteId,
                        principalTable: "Dependentes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotasDebitoPjItens_NotasDebitoPj_NotaDebitoPjId",
                        column: x => x.NotaDebitoPjId,
                        principalTable: "NotasDebitoPj",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotasDebitoPjItens_DependenteId",
                table: "NotasDebitoPjItens",
                column: "DependenteId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasDebitoPjItens_NotaDebitoPjId",
                table: "NotasDebitoPjItens",
                column: "NotaDebitoPjId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotasDebitoPjItens");
        }
    }
}
