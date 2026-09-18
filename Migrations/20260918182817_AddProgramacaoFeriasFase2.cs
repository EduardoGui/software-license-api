using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramacaoFeriasFase2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProgramacaoFeriasId",
                table: "MovimentacoesSaldoFerias",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProgramacoesFerias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PeriodoFeriasId = table.Column<int>(type: "integer", nullable: false),
                    Sequencia = table.Column<int>(type: "integer", nullable: false),
                    DataInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    DataFim = table.Column<DateOnly>(type: "date", nullable: false),
                    QuantidadeDias = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SolicitanteId = table.Column<int>(type: "integer", nullable: true),
                    DataSolicitacao = table.Column<DateOnly>(type: "date", nullable: true),
                    AprovadorId = table.Column<int>(type: "integer", nullable: true),
                    DataDecisao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ObservacaoAprovador = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AdiantamentoDecimoTerceiro = table.Column<bool>(type: "boolean", nullable: false),
                    AbonoPecuniario = table.Column<bool>(type: "boolean", nullable: false),
                    DiasAbono = table.Column<int>(type: "integer", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramacoesFerias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramacoesFerias_PeriodosFerias_PeriodoFeriasId",
                        column: x => x.PeriodoFeriasId,
                        principalTable: "PeriodosFerias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgramacoesFerias_Usuarios_AprovadorId",
                        column: x => x.AprovadorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgramacoesFerias_Usuarios_SolicitanteId",
                        column: x => x.SolicitanteId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MovimentacoesSaldoFerias_ProgramacaoFeriasId",
                table: "MovimentacoesSaldoFerias",
                column: "ProgramacaoFeriasId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramacoesFerias_AprovadorId",
                table: "ProgramacoesFerias",
                column: "AprovadorId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramacoesFerias_PeriodoFeriasId",
                table: "ProgramacoesFerias",
                column: "PeriodoFeriasId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramacoesFerias_SolicitanteId",
                table: "ProgramacoesFerias",
                column: "SolicitanteId");

            migrationBuilder.AddForeignKey(
                name: "FK_MovimentacoesSaldoFerias_ProgramacoesFerias_ProgramacaoFeri~",
                table: "MovimentacoesSaldoFerias",
                column: "ProgramacaoFeriasId",
                principalTable: "ProgramacoesFerias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MovimentacoesSaldoFerias_ProgramacoesFerias_ProgramacaoFeri~",
                table: "MovimentacoesSaldoFerias");

            migrationBuilder.DropTable(
                name: "ProgramacoesFerias");

            migrationBuilder.DropIndex(
                name: "IX_MovimentacoesSaldoFerias_ProgramacaoFeriasId",
                table: "MovimentacoesSaldoFerias");

            migrationBuilder.DropColumn(
                name: "ProgramacaoFeriasId",
                table: "MovimentacoesSaldoFerias");
        }
    }
}
