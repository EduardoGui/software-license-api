using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAditivoModulo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Aditivos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContratoId = table.Column<int>(type: "integer", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    Descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DataAssinatura = table.Column<DateOnly>(type: "date", nullable: false),
                    DataEfeito = table.Column<DateOnly>(type: "date", nullable: false),
                    DeltaValor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    NovaDataFimVigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    PercentualReajuste = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DataFormalizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Observacao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Aditivos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Aditivos_Contratos_ContratoId",
                        column: x => x.ContratoId,
                        principalTable: "Contratos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AditivoItens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AditivoId = table.Column<int>(type: "integer", nullable: false),
                    ContratoItemId = table.Column<int>(type: "integer", nullable: true),
                    DescricaoNovoItem = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CodigoNovoItem = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UnidadeNovoItem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DeltaQuantidade = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    NovoValorUnitario = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AditivoItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AditivoItens_Aditivos_AditivoId",
                        column: x => x.AditivoId,
                        principalTable: "Aditivos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AditivoItens_ContratoItens_ContratoItemId",
                        column: x => x.ContratoItemId,
                        principalTable: "ContratoItens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AditivoItens_AditivoId",
                table: "AditivoItens",
                column: "AditivoId");

            migrationBuilder.CreateIndex(
                name: "IX_AditivoItens_ContratoItemId",
                table: "AditivoItens",
                column: "ContratoItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Aditivos_ContratoId_Numero",
                table: "Aditivos",
                columns: new[] { "ContratoId", "Numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AditivoItens");

            migrationBuilder.DropTable(
                name: "Aditivos");
        }
    }
}
