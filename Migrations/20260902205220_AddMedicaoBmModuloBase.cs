using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMedicaoBmModuloBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MedicaoBms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContratoId = table.Column<int>(type: "integer", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    PeriodoInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodoFim = table.Column<DateOnly>(type: "date", nullable: false),
                    DataEnvio = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AprovadorId = table.Column<int>(type: "integer", nullable: true),
                    ObservacaoAprovador = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DataDecisao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ValorTotalMedido = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Observacao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicaoBms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicaoBms_Contratos_ContratoId",
                        column: x => x.ContratoId,
                        principalTable: "Contratos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicaoBms_Usuarios_AprovadorId",
                        column: x => x.AprovadorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MedicaoBmAnexos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MedicaoBmId = table.Column<int>(type: "integer", nullable: false),
                    NomeArquivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    TipoConteudo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Tamanho = table.Column<long>(type: "bigint", nullable: false),
                    Conteudo = table.Column<byte[]>(type: "bytea", nullable: false),
                    DataUpload = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicaoBmAnexos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicaoBmAnexos_MedicaoBms_MedicaoBmId",
                        column: x => x.MedicaoBmId,
                        principalTable: "MedicaoBms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MedicaoBmItens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MedicaoBmId = table.Column<int>(type: "integer", nullable: false),
                    ContratoItemId = table.Column<int>(type: "integer", nullable: true),
                    AditivoItemId = table.Column<int>(type: "integer", nullable: true),
                    DescricaoNoMomento = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    UnidadeNoMomento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    QuantidadeContratadaNoMomento = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    QuantidadeJaMedidaAntes = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    SaldoAntes = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    QuantidadeMedidaNestaBm = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    SaldoDepois = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    ValorUnitarioNoMomento = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorTotalItem = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PeriodoOriginalInicio = table.Column<DateOnly>(type: "date", nullable: true),
                    PeriodoOriginalFim = table.Column<DateOnly>(type: "date", nullable: true),
                    InicioEfetivo = table.Column<DateOnly>(type: "date", nullable: true),
                    FimEfetivo = table.Column<DateOnly>(type: "date", nullable: true),
                    DiasBase = table.Column<int>(type: "integer", nullable: true),
                    DiasMedidos = table.Column<int>(type: "integer", nullable: true),
                    PercentualProRata = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    AjusteManual = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    JustificativaAjuste = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicaoBmItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicaoBmItens_AditivoItens_AditivoItemId",
                        column: x => x.AditivoItemId,
                        principalTable: "AditivoItens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicaoBmItens_ContratoItens_ContratoItemId",
                        column: x => x.ContratoItemId,
                        principalTable: "ContratoItens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicaoBmItens_MedicaoBms_MedicaoBmId",
                        column: x => x.MedicaoBmId,
                        principalTable: "MedicaoBms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MedicaoBmAnexos_MedicaoBmId",
                table: "MedicaoBmAnexos",
                column: "MedicaoBmId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicaoBmItens_AditivoItemId",
                table: "MedicaoBmItens",
                column: "AditivoItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicaoBmItens_ContratoItemId",
                table: "MedicaoBmItens",
                column: "ContratoItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicaoBmItens_MedicaoBmId",
                table: "MedicaoBmItens",
                column: "MedicaoBmId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicaoBms_AprovadorId",
                table: "MedicaoBms",
                column: "AprovadorId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicaoBms_ContratoId_Numero",
                table: "MedicaoBms",
                columns: new[] { "ContratoId", "Numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MedicaoBmAnexos");

            migrationBuilder.DropTable(
                name: "MedicaoBmItens");

            migrationBuilder.DropTable(
                name: "MedicaoBms");
        }
    }
}
