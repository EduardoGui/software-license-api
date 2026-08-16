using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipamentosModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotasFiscaisEntrada",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Numero = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DataEntrada = table.Column<DateOnly>(type: "date", nullable: false),
                    FornecedorNome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotasFiscaisEntrada", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TiposEquipamento",
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
                    table.PrimaryKey("PK_TiposEquipamento", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotasFiscaisItens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NotaFiscalEntradaId = table.Column<int>(type: "integer", nullable: false),
                    TipoEquipamentoId = table.Column<int>(type: "integer", nullable: false),
                    Descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Quantidade = table.Column<int>(type: "integer", nullable: false),
                    ValorUnitario = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotasFiscaisItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotasFiscaisItens_NotasFiscaisEntrada_NotaFiscalEntradaId",
                        column: x => x.NotaFiscalEntradaId,
                        principalTable: "NotasFiscaisEntrada",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotasFiscaisItens_TiposEquipamento_TipoEquipamentoId",
                        column: x => x.TipoEquipamentoId,
                        principalTable: "TiposEquipamento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Equipamentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TipoEquipamentoId = table.Column<int>(type: "integer", nullable: false),
                    NotaFiscalItemId = table.Column<int>(type: "integer", nullable: true),
                    Marca = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Modelo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NumeroSerie = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Patrimonio = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Origem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FornecedorNome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ValorMensal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    DataInicioContrato = table.Column<DateOnly>(type: "date", nullable: true),
                    DataFimContrato = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DataBaixa = table.Column<DateOnly>(type: "date", nullable: true),
                    Observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipamentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Equipamentos_NotasFiscaisItens_NotaFiscalItemId",
                        column: x => x.NotaFiscalItemId,
                        principalTable: "NotasFiscaisItens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Equipamentos_TiposEquipamento_TipoEquipamentoId",
                        column: x => x.TipoEquipamentoId,
                        principalTable: "TiposEquipamento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EquipamentoAlocacoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EquipamentoId = table.Column<int>(type: "integer", nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    DataInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    DataFim = table.Column<DateOnly>(type: "date", nullable: true),
                    Observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipamentoAlocacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipamentoAlocacoes_Equipamentos_EquipamentoId",
                        column: x => x.EquipamentoId,
                        principalTable: "Equipamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipamentoAlocacoes_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EquipamentoAlocacoes_DataFim",
                table: "EquipamentoAlocacoes",
                column: "DataFim");

            migrationBuilder.CreateIndex(
                name: "IX_EquipamentoAlocacoes_DataInicio",
                table: "EquipamentoAlocacoes",
                column: "DataInicio");

            migrationBuilder.CreateIndex(
                name: "IX_EquipamentoAlocacoes_EquipamentoId",
                table: "EquipamentoAlocacoes",
                column: "EquipamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipamentoAlocacoes_UsuarioId",
                table: "EquipamentoAlocacoes",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Equipamentos_NotaFiscalItemId",
                table: "Equipamentos",
                column: "NotaFiscalItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Equipamentos_Patrimonio",
                table: "Equipamentos",
                column: "Patrimonio",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Equipamentos_Status",
                table: "Equipamentos",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Equipamentos_TipoEquipamentoId",
                table: "Equipamentos",
                column: "TipoEquipamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasFiscaisEntrada_Numero",
                table: "NotasFiscaisEntrada",
                column: "Numero");

            migrationBuilder.CreateIndex(
                name: "IX_NotasFiscaisItens_NotaFiscalEntradaId",
                table: "NotasFiscaisItens",
                column: "NotaFiscalEntradaId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasFiscaisItens_TipoEquipamentoId",
                table: "NotasFiscaisItens",
                column: "TipoEquipamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_TiposEquipamento_Nome",
                table: "TiposEquipamento",
                column: "Nome",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EquipamentoAlocacoes");

            migrationBuilder.DropTable(
                name: "Equipamentos");

            migrationBuilder.DropTable(
                name: "NotasFiscaisItens");

            migrationBuilder.DropTable(
                name: "NotasFiscaisEntrada");

            migrationBuilder.DropTable(
                name: "TiposEquipamento");
        }
    }
}
