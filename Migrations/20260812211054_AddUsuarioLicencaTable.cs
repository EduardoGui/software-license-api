using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUsuarioLicencaTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UsuarioLicencas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    LicencaId = table.Column<int>(type: "integer", nullable: false),
                    DataInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    DataFim = table.Column<DateOnly>(type: "date", nullable: true),
                    Observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuarioLicencas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsuarioLicencas_Licencas_LicencaId",
                        column: x => x.LicencaId,
                        principalTable: "Licencas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UsuarioLicencas_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioLicencas_DataFim",
                table: "UsuarioLicencas",
                column: "DataFim");

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioLicencas_DataInicio",
                table: "UsuarioLicencas",
                column: "DataInicio");

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioLicencas_LicencaId",
                table: "UsuarioLicencas",
                column: "LicencaId");

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioLicencas_UsuarioId",
                table: "UsuarioLicencas",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UsuarioLicencas");
        }
    }
}
