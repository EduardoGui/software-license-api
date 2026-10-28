using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMarcosDocumentais : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DataChegadaMaterial",
                table: "OrdensCompra",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DataEnvioAssinatura",
                table: "OrdensCompra",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DataEnvioFornecedor",
                table: "OrdensCompra",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DataEnvioAssinatura",
                table: "MedicaoBms",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DataLiberacaoNf",
                table: "MedicaoBms",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DataSolicitacaoEspelho",
                table: "MedicaoBms",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataChegadaMaterial",
                table: "OrdensCompra");

            migrationBuilder.DropColumn(
                name: "DataEnvioAssinatura",
                table: "OrdensCompra");

            migrationBuilder.DropColumn(
                name: "DataEnvioFornecedor",
                table: "OrdensCompra");

            migrationBuilder.DropColumn(
                name: "DataEnvioAssinatura",
                table: "MedicaoBms");

            migrationBuilder.DropColumn(
                name: "DataLiberacaoNf",
                table: "MedicaoBms");

            migrationBuilder.DropColumn(
                name: "DataSolicitacaoEspelho",
                table: "MedicaoBms");
        }
    }
}
