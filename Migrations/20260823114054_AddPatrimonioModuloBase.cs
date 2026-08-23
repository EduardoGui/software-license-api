using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPatrimonioModuloBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "TipoEquipamentoId",
                table: "NotasFiscaisItens",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "Destino",
                table: "NotasFiscaisItens",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Equipamento");

            migrationBuilder.AddColumn<int>(
                name: "LocalId",
                table: "NotasFiscaisItens",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TipoPatrimonioId",
                table: "NotasFiscaisItens",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NotaFiscalEntradaId",
                table: "Licencas",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TiposPatrimonio",
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
                    table.PrimaryKey("PK_TiposPatrimonio", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PatrimonioItens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NotaFiscalItemId = table.Column<int>(type: "integer", nullable: false),
                    TipoPatrimonioId = table.Column<int>(type: "integer", nullable: false),
                    Descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    NumeroPatrimonio = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LocalId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatrimonioItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatrimonioItens_Locais_LocalId",
                        column: x => x.LocalId,
                        principalTable: "Locais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatrimonioItens_NotasFiscaisItens_NotaFiscalItemId",
                        column: x => x.NotaFiscalItemId,
                        principalTable: "NotasFiscaisItens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatrimonioItens_TiposPatrimonio_TipoPatrimonioId",
                        column: x => x.TipoPatrimonioId,
                        principalTable: "TiposPatrimonio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatrimonioItemAnexos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatrimonioItemId = table.Column<int>(type: "integer", nullable: false),
                    NomeArquivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    TipoConteudo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Tamanho = table.Column<long>(type: "bigint", nullable: false),
                    Conteudo = table.Column<byte[]>(type: "bytea", nullable: false),
                    DataUpload = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatrimonioItemAnexos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatrimonioItemAnexos_PatrimonioItens_PatrimonioItemId",
                        column: x => x.PatrimonioItemId,
                        principalTable: "PatrimonioItens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotasFiscaisItens_LocalId",
                table: "NotasFiscaisItens",
                column: "LocalId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasFiscaisItens_TipoPatrimonioId",
                table: "NotasFiscaisItens",
                column: "TipoPatrimonioId");

            migrationBuilder.CreateIndex(
                name: "IX_Licencas_NotaFiscalEntradaId",
                table: "Licencas",
                column: "NotaFiscalEntradaId");

            migrationBuilder.CreateIndex(
                name: "IX_PatrimonioItemAnexos_PatrimonioItemId",
                table: "PatrimonioItemAnexos",
                column: "PatrimonioItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PatrimonioItens_LocalId",
                table: "PatrimonioItens",
                column: "LocalId");

            migrationBuilder.CreateIndex(
                name: "IX_PatrimonioItens_NotaFiscalItemId",
                table: "PatrimonioItens",
                column: "NotaFiscalItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PatrimonioItens_NumeroPatrimonio",
                table: "PatrimonioItens",
                column: "NumeroPatrimonio",
                unique: true,
                filter: "\"NumeroPatrimonio\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PatrimonioItens_Status",
                table: "PatrimonioItens",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PatrimonioItens_TipoPatrimonioId",
                table: "PatrimonioItens",
                column: "TipoPatrimonioId");

            migrationBuilder.CreateIndex(
                name: "IX_TiposPatrimonio_Nome",
                table: "TiposPatrimonio",
                column: "Nome",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Licencas_NotasFiscaisEntrada_NotaFiscalEntradaId",
                table: "Licencas",
                column: "NotaFiscalEntradaId",
                principalTable: "NotasFiscaisEntrada",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NotasFiscaisItens_Locais_LocalId",
                table: "NotasFiscaisItens",
                column: "LocalId",
                principalTable: "Locais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NotasFiscaisItens_TiposPatrimonio_TipoPatrimonioId",
                table: "NotasFiscaisItens",
                column: "TipoPatrimonioId",
                principalTable: "TiposPatrimonio",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Licencas_NotasFiscaisEntrada_NotaFiscalEntradaId",
                table: "Licencas");

            migrationBuilder.DropForeignKey(
                name: "FK_NotasFiscaisItens_Locais_LocalId",
                table: "NotasFiscaisItens");

            migrationBuilder.DropForeignKey(
                name: "FK_NotasFiscaisItens_TiposPatrimonio_TipoPatrimonioId",
                table: "NotasFiscaisItens");

            migrationBuilder.DropTable(
                name: "PatrimonioItemAnexos");

            migrationBuilder.DropTable(
                name: "PatrimonioItens");

            migrationBuilder.DropTable(
                name: "TiposPatrimonio");

            migrationBuilder.DropIndex(
                name: "IX_NotasFiscaisItens_LocalId",
                table: "NotasFiscaisItens");

            migrationBuilder.DropIndex(
                name: "IX_NotasFiscaisItens_TipoPatrimonioId",
                table: "NotasFiscaisItens");

            migrationBuilder.DropIndex(
                name: "IX_Licencas_NotaFiscalEntradaId",
                table: "Licencas");

            migrationBuilder.DropColumn(
                name: "Destino",
                table: "NotasFiscaisItens");

            migrationBuilder.DropColumn(
                name: "LocalId",
                table: "NotasFiscaisItens");

            migrationBuilder.DropColumn(
                name: "TipoPatrimonioId",
                table: "NotasFiscaisItens");

            migrationBuilder.DropColumn(
                name: "NotaFiscalEntradaId",
                table: "Licencas");

            migrationBuilder.AlterColumn<int>(
                name: "TipoEquipamentoId",
                table: "NotasFiscaisItens",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
