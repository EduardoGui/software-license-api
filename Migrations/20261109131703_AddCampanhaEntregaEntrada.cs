using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCampanhaEntregaEntrada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampanhaEntregaEntradas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CampanhaEntregaItemId = table.Column<int>(type: "integer", nullable: false),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    Quantidade = table.Column<int>(type: "integer", nullable: false),
                    Observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UsuarioId = table.Column<int>(type: "integer", nullable: true),
                    RegistradoPorNome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampanhaEntregaEntradas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampanhaEntregaEntradas_CampanhaEntregaItens_CampanhaEntreg~",
                        column: x => x.CampanhaEntregaItemId,
                        principalTable: "CampanhaEntregaItens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampanhaEntregaEntradas_CampanhaEntregaItemId",
                table: "CampanhaEntregaEntradas",
                column: "CampanhaEntregaItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampanhaEntregaEntradas");
        }
    }
}
