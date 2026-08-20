using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LocalId",
                table: "ReembolsosDespesa",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Locais",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Endereco = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locais", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReembolsosDespesa_LocalId",
                table: "ReembolsosDespesa",
                column: "LocalId");

            migrationBuilder.CreateIndex(
                name: "IX_Locais_Nome",
                table: "Locais",
                column: "Nome",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ReembolsosDespesa_Locais_LocalId",
                table: "ReembolsosDespesa",
                column: "LocalId",
                principalTable: "Locais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReembolsosDespesa_Locais_LocalId",
                table: "ReembolsosDespesa");

            migrationBuilder.DropTable(
                name: "Locais");

            migrationBuilder.DropIndex(
                name: "IX_ReembolsosDespesa_LocalId",
                table: "ReembolsosDespesa");

            migrationBuilder.DropColumn(
                name: "LocalId",
                table: "ReembolsosDespesa");
        }
    }
}
