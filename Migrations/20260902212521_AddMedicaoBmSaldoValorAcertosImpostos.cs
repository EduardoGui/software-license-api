using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMedicaoBmSaldoValorAcertosImpostos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "SaldoValorAntes",
                table: "MedicaoBmItens",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SaldoValorDepois",
                table: "MedicaoBmItens",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "MedicaoBmAcertos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MedicaoBmId = table.Column<int>(type: "integer", nullable: false),
                    MedicaoBmItemId = table.Column<int>(type: "integer", nullable: true),
                    Descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Unidade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Quantidade = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    PrecoUnitario = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PrecoTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicaoBmAcertos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicaoBmAcertos_MedicaoBmItens_MedicaoBmItemId",
                        column: x => x.MedicaoBmItemId,
                        principalTable: "MedicaoBmItens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicaoBmAcertos_MedicaoBms_MedicaoBmId",
                        column: x => x.MedicaoBmId,
                        principalTable: "MedicaoBms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MedicaoBmImpostos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MedicaoBmId = table.Column<int>(type: "integer", nullable: false),
                    MedicaoBmItemId = table.Column<int>(type: "integer", nullable: true),
                    Descricao = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Aliquota = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    Base = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicaoBmImpostos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicaoBmImpostos_MedicaoBmItens_MedicaoBmItemId",
                        column: x => x.MedicaoBmItemId,
                        principalTable: "MedicaoBmItens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicaoBmImpostos_MedicaoBms_MedicaoBmId",
                        column: x => x.MedicaoBmId,
                        principalTable: "MedicaoBms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MedicaoBmAcertos_MedicaoBmId",
                table: "MedicaoBmAcertos",
                column: "MedicaoBmId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicaoBmAcertos_MedicaoBmItemId",
                table: "MedicaoBmAcertos",
                column: "MedicaoBmItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicaoBmImpostos_MedicaoBmId",
                table: "MedicaoBmImpostos",
                column: "MedicaoBmId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicaoBmImpostos_MedicaoBmItemId",
                table: "MedicaoBmImpostos",
                column: "MedicaoBmItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MedicaoBmAcertos");

            migrationBuilder.DropTable(
                name: "MedicaoBmImpostos");

            migrationBuilder.DropColumn(
                name: "SaldoValorAntes",
                table: "MedicaoBmItens");

            migrationBuilder.DropColumn(
                name: "SaldoValorDepois",
                table: "MedicaoBmItens");
        }
    }
}
