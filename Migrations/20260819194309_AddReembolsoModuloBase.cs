using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddReembolsoModuloBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Agencia",
                table: "Usuarios",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Banco",
                table: "Usuarios",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cargo",
                table: "Usuarios",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChavePix",
                table: "Usuarios",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContaBancaria",
                table: "Usuarios",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cpf",
                table: "Usuarios",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SetorId",
                table: "Usuarios",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Setores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Setores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TiposDespesa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TiposDespesa", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SetorAprovadores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SetorId = table.Column<int>(type: "integer", nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SetorAprovadores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SetorAprovadores_Setores_SetorId",
                        column: x => x.SetorId,
                        principalTable: "Setores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SetorAprovadores_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_SetorId",
                table: "Usuarios",
                column: "SetorId");

            migrationBuilder.CreateIndex(
                name: "IX_SetorAprovadores_SetorId_UsuarioId",
                table: "SetorAprovadores",
                columns: new[] { "SetorId", "UsuarioId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SetorAprovadores_UsuarioId",
                table: "SetorAprovadores",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Setores_Nome",
                table: "Setores",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TiposDespesa_Nome",
                table: "TiposDespesa",
                column: "Nome",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Usuarios_Setores_SetorId",
                table: "Usuarios",
                column: "SetorId",
                principalTable: "Setores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Usuarios_Setores_SetorId",
                table: "Usuarios");

            migrationBuilder.DropTable(
                name: "SetorAprovadores");

            migrationBuilder.DropTable(
                name: "TiposDespesa");

            migrationBuilder.DropTable(
                name: "Setores");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_SetorId",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "Agencia",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "Banco",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "Cargo",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "ChavePix",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "ContaBancaria",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "Cpf",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "SetorId",
                table: "Usuarios");
        }
    }
}
