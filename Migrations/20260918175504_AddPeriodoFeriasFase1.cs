using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPeriodoFeriasFase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PeriodosFerias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    InicioAquisitivo = table.Column<DateOnly>(type: "date", nullable: false),
                    FimAquisitivo = table.Column<DateOnly>(type: "date", nullable: false),
                    InicioConcessivo = table.Column<DateOnly>(type: "date", nullable: false),
                    FimConcessivo = table.Column<DateOnly>(type: "date", nullable: false),
                    DiasDireito = table.Column<int>(type: "integer", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodosFerias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PeriodosFerias_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovimentacoesSaldoFerias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PeriodoFeriasId = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Quantidade = table.Column<int>(type: "integer", nullable: false),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    UsuarioResponsavelId = table.Column<int>(type: "integer", nullable: true),
                    Observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimentacoesSaldoFerias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimentacoesSaldoFerias_PeriodosFerias_PeriodoFeriasId",
                        column: x => x.PeriodoFeriasId,
                        principalTable: "PeriodosFerias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimentacoesSaldoFerias_Usuarios_UsuarioResponsavelId",
                        column: x => x.UsuarioResponsavelId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MovimentacoesSaldoFerias_PeriodoFeriasId",
                table: "MovimentacoesSaldoFerias",
                column: "PeriodoFeriasId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentacoesSaldoFerias_UsuarioResponsavelId",
                table: "MovimentacoesSaldoFerias",
                column: "UsuarioResponsavelId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodosFerias_UsuarioId",
                table: "PeriodosFerias",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MovimentacoesSaldoFerias");

            migrationBuilder.DropTable(
                name: "PeriodosFerias");
        }
    }
}
