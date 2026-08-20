using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddReembolsoDespesaTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReembolsosDespesa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    SetorId = table.Column<int>(type: "integer", nullable: true),
                    DataSolicitacao = table.Column<DateOnly>(type: "date", nullable: false),
                    Finalidade = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    FormaPagamento = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AprovadorId = table.Column<int>(type: "integer", nullable: true),
                    ObservacaoAprovador = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DataDecisao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReembolsosDespesa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReembolsosDespesa_Setores_SetorId",
                        column: x => x.SetorId,
                        principalTable: "Setores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReembolsosDespesa_Usuarios_AprovadorId",
                        column: x => x.AprovadorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReembolsosDespesa_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReembolsoDespesaItens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReembolsoDespesaId = table.Column<int>(type: "integer", nullable: false),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    TipoDespesaId = table.Column<int>(type: "integer", nullable: false),
                    Descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    NumeroDocumento = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReembolsoDespesaItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReembolsoDespesaItens_ReembolsosDespesa_ReembolsoDespesaId",
                        column: x => x.ReembolsoDespesaId,
                        principalTable: "ReembolsosDespesa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReembolsoDespesaItens_TiposDespesa_TipoDespesaId",
                        column: x => x.TipoDespesaId,
                        principalTable: "TiposDespesa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReembolsoDespesaItens_ReembolsoDespesaId",
                table: "ReembolsoDespesaItens",
                column: "ReembolsoDespesaId");

            migrationBuilder.CreateIndex(
                name: "IX_ReembolsoDespesaItens_TipoDespesaId",
                table: "ReembolsoDespesaItens",
                column: "TipoDespesaId");

            migrationBuilder.CreateIndex(
                name: "IX_ReembolsosDespesa_AprovadorId",
                table: "ReembolsosDespesa",
                column: "AprovadorId");

            migrationBuilder.CreateIndex(
                name: "IX_ReembolsosDespesa_SetorId",
                table: "ReembolsosDespesa",
                column: "SetorId");

            migrationBuilder.CreateIndex(
                name: "IX_ReembolsosDespesa_Status",
                table: "ReembolsosDespesa",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ReembolsosDespesa_UsuarioId",
                table: "ReembolsosDespesa",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReembolsoDespesaItens");

            migrationBuilder.DropTable(
                name: "ReembolsosDespesa");
        }
    }
}
