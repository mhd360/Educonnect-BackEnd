using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduConnect.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFrequenciaBoletim : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Faltas",
                table: "OfertaAlunos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalAulas",
                table: "Disciplinas",
                type: "int",
                nullable: false,
                defaultValue: 16);

            migrationBuilder.CreateTable(
                name: "OfertaAlunoFaltas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OfertaAlunoId = table.Column<int>(type: "int", nullable: false),
                    NumeroAula = table.Column<int>(type: "int", nullable: false),
                    DataMarcacao = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfertaAlunoFaltas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OfertaAlunoFaltas_OfertaAlunos_OfertaAlunoId",
                        column: x => x.OfertaAlunoId,
                        principalTable: "OfertaAlunos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OfertaAlunoFaltas_OfertaAlunoId_NumeroAula",
                table: "OfertaAlunoFaltas",
                columns: new[] { "OfertaAlunoId", "NumeroAula" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OfertaAlunoFaltas");

            migrationBuilder.DropColumn(
                name: "Faltas",
                table: "OfertaAlunos");

            migrationBuilder.DropColumn(
                name: "TotalAulas",
                table: "Disciplinas");
        }
    }
}
