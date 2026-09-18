using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRecessoCorporativoFase3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RecessoColaboradorId",
                table: "MovimentacoesSaldoFerias",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RecessosCorporativos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DataInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    DataFim = table.Column<DateOnly>(type: "date", nullable: false),
                    DiasCorridos = table.Column<int>(type: "integer", nullable: false),
                    DiasADescontar = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecessosCorporativos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecessosColaborador",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecessoCorporativoId = table.Column<int>(type: "integer", nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    PeriodoFeriasId = table.Column<int>(type: "integer", nullable: false),
                    SaldoAnterior = table.Column<int>(type: "integer", nullable: false),
                    DiasAbatidos = table.Column<int>(type: "integer", nullable: false),
                    SaldoPosterior = table.Column<int>(type: "integer", nullable: false),
                    Situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecessosColaborador", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecessosColaborador_PeriodosFerias_PeriodoFeriasId",
                        column: x => x.PeriodoFeriasId,
                        principalTable: "PeriodosFerias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecessosColaborador_RecessosCorporativos_RecessoCorporativo~",
                        column: x => x.RecessoCorporativoId,
                        principalTable: "RecessosCorporativos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecessosColaborador_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MovimentacoesSaldoFerias_RecessoColaboradorId",
                table: "MovimentacoesSaldoFerias",
                column: "RecessoColaboradorId");

            migrationBuilder.CreateIndex(
                name: "IX_RecessosColaborador_PeriodoFeriasId",
                table: "RecessosColaborador",
                column: "PeriodoFeriasId");

            migrationBuilder.CreateIndex(
                name: "IX_RecessosColaborador_RecessoCorporativoId",
                table: "RecessosColaborador",
                column: "RecessoCorporativoId");

            migrationBuilder.CreateIndex(
                name: "IX_RecessosColaborador_UsuarioId",
                table: "RecessosColaborador",
                column: "UsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_MovimentacoesSaldoFerias_RecessosColaborador_RecessoColabor~",
                table: "MovimentacoesSaldoFerias",
                column: "RecessoColaboradorId",
                principalTable: "RecessosColaborador",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MovimentacoesSaldoFerias_RecessosColaborador_RecessoColabor~",
                table: "MovimentacoesSaldoFerias");

            migrationBuilder.DropTable(
                name: "RecessosColaborador");

            migrationBuilder.DropTable(
                name: "RecessosCorporativos");

            migrationBuilder.DropIndex(
                name: "IX_MovimentacoesSaldoFerias_RecessoColaboradorId",
                table: "MovimentacoesSaldoFerias");

            migrationBuilder.DropColumn(
                name: "RecessoColaboradorId",
                table: "MovimentacoesSaldoFerias");
        }
    }
}
