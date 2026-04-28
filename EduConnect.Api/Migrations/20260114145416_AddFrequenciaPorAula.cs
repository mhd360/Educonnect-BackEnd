using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduConnect.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFrequenciaPorAula : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "NotaExame",
                table: "OfertaAlunos",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OfertaFaltas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OfertaDisciplinaId = table.Column<int>(type: "int", nullable: false),
                    AlunoId = table.Column<int>(type: "int", nullable: false),
                    AulaNumero = table.Column<int>(type: "int", nullable: false),
                    DataLancamento = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Ativa = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfertaFaltas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OfertaFaltas_Alunos_AlunoId",
                        column: x => x.AlunoId,
                        principalTable: "Alunos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OfertaFaltas_OfertaDisciplinas_OfertaDisciplinaId",
                        column: x => x.OfertaDisciplinaId,
                        principalTable: "OfertaDisciplinas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OfertaFaltas_AlunoId",
                table: "OfertaFaltas",
                column: "AlunoId");

            migrationBuilder.CreateIndex(
                name: "IX_OfertaFaltas_OfertaDisciplinaId_AlunoId_AulaNumero",
                table: "OfertaFaltas",
                columns: new[] { "OfertaDisciplinaId", "AlunoId", "AulaNumero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OfertaFaltas");

            migrationBuilder.DropColumn(
                name: "NotaExame",
                table: "OfertaAlunos");
        }
    }
}
