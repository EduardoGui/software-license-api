using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCampanhaEntregaItens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampanhaEntregaItens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CampanhaEntregaId = table.Column<int>(type: "integer", nullable: false),
                    Descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Tamanho = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Quantidade = table.Column<int>(type: "integer", nullable: false),
                    Validade = table.Column<DateOnly>(type: "date", nullable: true),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampanhaEntregaItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampanhaEntregaItens_CampanhasEntrega_CampanhaEntregaId",
                        column: x => x.CampanhaEntregaId,
                        principalTable: "CampanhasEntrega",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampanhaEntregaItens_CampanhaEntregaId",
                table: "CampanhaEntregaItens",
                column: "CampanhaEntregaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampanhaEntregaItens");
        }
    }
}
