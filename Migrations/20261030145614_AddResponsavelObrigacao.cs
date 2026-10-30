using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddResponsavelObrigacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DataAtribuicao",
                table: "Obrigacoes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResponsavelUsuarioId",
                table: "Obrigacoes",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Obrigacoes_ResponsavelUsuarioId",
                table: "Obrigacoes",
                column: "ResponsavelUsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_Obrigacoes_Usuarios_ResponsavelUsuarioId",
                table: "Obrigacoes",
                column: "ResponsavelUsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Obrigacoes_Usuarios_ResponsavelUsuarioId",
                table: "Obrigacoes");

            migrationBuilder.DropIndex(
                name: "IX_Obrigacoes_ResponsavelUsuarioId",
                table: "Obrigacoes");

            migrationBuilder.DropColumn(
                name: "DataAtribuicao",
                table: "Obrigacoes");

            migrationBuilder.DropColumn(
                name: "ResponsavelUsuarioId",
                table: "Obrigacoes");
        }
    }
}
