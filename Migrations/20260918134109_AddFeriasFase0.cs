using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFeriasFase0 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GestorImediatoId",
                table: "Usuarios",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Feriados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    Descricao = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Abrangencia = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    Municipio = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Feriados", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PoliticasFerias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TipoVinculo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DiasDireitoPorAno = table.Column<int>(type: "integer", nullable: false),
                    MaxFracionamentos = table.Column<int>(type: "integer", nullable: false),
                    DiasMinimoUltimoFracionamento = table.Column<int>(type: "integer", nullable: false),
                    DiasMinimoDemaisFracionamentos = table.Column<int>(type: "integer", nullable: false),
                    DiasAntecedenciaRemarcacao = table.Column<int>(type: "integer", nullable: false),
                    DiasAntecedenciaMarcacaoCompulsoria = table.Column<int>(type: "integer", nullable: false),
                    PermiteAbonoPecuniario = table.Column<bool>(type: "boolean", nullable: false),
                    MaxDiasAbono = table.Column<int>(type: "integer", nullable: false),
                    DiasMinimosAntesFeriadoOuFimDeSemana = table.Column<int>(type: "integer", nullable: false),
                    Ativa = table.Column<bool>(type: "boolean", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoliticasFerias", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_GestorImediatoId",
                table: "Usuarios",
                column: "GestorImediatoId");

            migrationBuilder.CreateIndex(
                name: "IX_Feriados_Data",
                table: "Feriados",
                column: "Data");

            migrationBuilder.AddForeignKey(
                name: "FK_Usuarios_Usuarios_GestorImediatoId",
                table: "Usuarios",
                column: "GestorImediatoId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Usuarios_Usuarios_GestorImediatoId",
                table: "Usuarios");

            migrationBuilder.DropTable(
                name: "Feriados");

            migrationBuilder.DropTable(
                name: "PoliticasFerias");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_GestorImediatoId",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "GestorImediatoId",
                table: "Usuarios");
        }
    }
}
