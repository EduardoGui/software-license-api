using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgendaTarefaUnica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "TarefaRecorrenteId",
                table: "TarefaOcorrencias",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "Titulo",
                table: "TarefaOcorrencias",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            // Preenche o título das ocorrências já existentes a partir da tarefa recorrente que as gerou
            // (o campo passa a ser copiado no momento da criação daqui em diante, não mais lido ao vivo do pai).
            migrationBuilder.Sql(
                "UPDATE \"TarefaOcorrencias\" o SET \"Titulo\" = t.\"Titulo\" " +
                "FROM \"TarefasRecorrentes\" t WHERE o.\"TarefaRecorrenteId\" = t.\"Id\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Titulo",
                table: "TarefaOcorrencias");

            migrationBuilder.AlterColumn<int>(
                name: "TarefaRecorrenteId",
                table: "TarefaOcorrencias",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
