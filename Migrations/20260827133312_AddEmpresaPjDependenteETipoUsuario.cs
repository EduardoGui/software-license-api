using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEmpresaPjDependenteETipoUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmpresaPjId",
                table: "Usuarios",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tipo",
                table: "Usuarios",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Dependentes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dependentes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Dependentes_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmpresasPj",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RazaoSocial = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Cnpj = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Ativa = table.Column<bool>(type: "boolean", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmpresasPj", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_EmpresaPjId",
                table: "Usuarios",
                column: "EmpresaPjId");

            migrationBuilder.CreateIndex(
                name: "IX_Dependentes_UsuarioId",
                table: "Dependentes",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_EmpresasPj_Cnpj",
                table: "EmpresasPj",
                column: "Cnpj",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Usuarios_EmpresasPj_EmpresaPjId",
                table: "Usuarios",
                column: "EmpresaPjId",
                principalTable: "EmpresasPj",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Usuarios_EmpresasPj_EmpresaPjId",
                table: "Usuarios");

            migrationBuilder.DropTable(
                name: "Dependentes");

            migrationBuilder.DropTable(
                name: "EmpresasPj");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_EmpresaPjId",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "EmpresaPjId",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "Usuarios");
        }
    }
}
