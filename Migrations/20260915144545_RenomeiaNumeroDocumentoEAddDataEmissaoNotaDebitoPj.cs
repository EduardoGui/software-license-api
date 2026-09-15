using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class RenomeiaNumeroDocumentoEAddDataEmissaoNotaDebitoPj : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "NumeroDocumento",
                table: "NotasDebitoPj",
                newName: "NumeroFatura");

            migrationBuilder.AddColumn<DateOnly>(
                name: "DataEmissao",
                table: "NotasDebitoPj",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataEmissao",
                table: "NotasDebitoPj");

            migrationBuilder.RenameColumn(
                name: "NumeroFatura",
                table: "NotasDebitoPj",
                newName: "NumeroDocumento");
        }
    }
}
