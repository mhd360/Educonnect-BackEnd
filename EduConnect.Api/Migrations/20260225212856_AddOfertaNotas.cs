using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduConnect.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOfertaNotas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OfertaNotas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OfertaDisciplinaId = table.Column<int>(type: "int", nullable: false),
                    AlunoId = table.Column<int>(type: "int", nullable: false),
                    A1 = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    A2 = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    A3 = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfertaNotas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OfertaNotas_Alunos_AlunoId",
                        column: x => x.AlunoId,
                        principalTable: "Alunos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OfertaNotas_OfertaDisciplinas_OfertaDisciplinaId",
                        column: x => x.OfertaDisciplinaId,
                        principalTable: "OfertaDisciplinas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OfertaNotas_AlunoId",
                table: "OfertaNotas",
                column: "AlunoId");

            migrationBuilder.CreateIndex(
                name: "IX_OfertaNotas_OfertaDisciplinaId_AlunoId",
                table: "OfertaNotas",
                columns: new[] { "OfertaDisciplinaId", "AlunoId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OfertaNotas");
        }
    }
}
