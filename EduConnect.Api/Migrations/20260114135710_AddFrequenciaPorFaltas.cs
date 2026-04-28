using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduConnect.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFrequenciaPorFaltas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TotalAulas",
                table: "OfertaDisciplinas",
                type: "int",
                nullable: false,
                defaultValue: 16);

            migrationBuilder.CreateTable(
                name: "FaltaOfertaAlunos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OfertaDisciplinaId = table.Column<int>(type: "int", nullable: false),
                    AlunoId = table.Column<int>(type: "int", nullable: false),
                    NumeroAula = table.Column<int>(type: "int", nullable: false),
                    DataMarcacao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Ativa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaltaOfertaAlunos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FaltaOfertaAlunos_Alunos_AlunoId",
                        column: x => x.AlunoId,
                        principalTable: "Alunos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FaltaOfertaAlunos_OfertaDisciplinas_OfertaDisciplinaId",
                        column: x => x.OfertaDisciplinaId,
                        principalTable: "OfertaDisciplinas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FaltaOfertaAlunos_AlunoId",
                table: "FaltaOfertaAlunos",
                column: "AlunoId");

            migrationBuilder.CreateIndex(
                name: "IX_FaltaOfertaAlunos_OfertaDisciplinaId_AlunoId_NumeroAula",
                table: "FaltaOfertaAlunos",
                columns: new[] { "OfertaDisciplinaId", "AlunoId", "NumeroAula" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FaltaOfertaAlunos");

            migrationBuilder.DropColumn(
                name: "TotalAulas",
                table: "OfertaDisciplinas");
        }
    }
}
