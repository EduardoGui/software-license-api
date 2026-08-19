using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLicencaValorTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LicencaValores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LicencaId = table.Column<int>(type: "integer", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Periodicidade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DataVigenciaInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LicencaValores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LicencaValores_Licencas_LicencaId",
                        column: x => x.LicencaId,
                        principalTable: "Licencas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LicencaValores_LicencaId_DataVigenciaInicio",
                table: "LicencaValores",
                columns: new[] { "LicencaId", "DataVigenciaInicio" });

            // Licenças cadastradas antes deste campo existir recebem um valor simbólico (999,99/Mensal),
            // propositalmente chamativo, até o usuário corrigir cada uma com o valor de contrato real.
            migrationBuilder.Sql(
                """
                INSERT INTO "LicencaValores" ("LicencaId", "Valor", "Periodicidade", "DataVigenciaInicio", "DataCriacao")
                SELECT "Id", 999.99, 'Mensal', "DataInicio", now()
                FROM "Licencas";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LicencaValores");
        }
    }
}
