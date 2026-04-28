using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduConnect.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOfertaDisciplina : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OfertaDisciplinas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DisciplinaId = table.Column<int>(type: "int", nullable: false),
                    ProfessorId = table.Column<int>(type: "int", nullable: false),
                    TurmaId = table.Column<int>(type: "int", nullable: true),
                    Ano = table.Column<int>(type: "int", nullable: false),
                    Semestre = table.Column<byte>(type: "tinyint", nullable: false),
                    Periodo = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    Ativa = table.Column<bool>(type: "bit", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfertaDisciplinas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OfertaDisciplinas_Disciplinas_DisciplinaId",
                        column: x => x.DisciplinaId,
                        principalTable: "Disciplinas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OfertaDisciplinas_Professores_ProfessorId",
                        column: x => x.ProfessorId,
                        principalTable: "Professores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OfertaDisciplinas_Turmas_TurmaId",
                        column: x => x.TurmaId,
                        principalTable: "Turmas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OfertaAlunos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OfertaDisciplinaId = table.Column<int>(type: "int", nullable: false),
                    AlunoId = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    DataVinculo = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfertaAlunos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OfertaAlunos_Alunos_AlunoId",
                        column: x => x.AlunoId,
                        principalTable: "Alunos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OfertaAlunos_OfertaDisciplinas_OfertaDisciplinaId",
                        column: x => x.OfertaDisciplinaId,
                        principalTable: "OfertaDisciplinas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OfertaAlunos_AlunoId",
                table: "OfertaAlunos",
                column: "AlunoId");

            migrationBuilder.CreateIndex(
                name: "IX_OfertaAlunos_OfertaDisciplinaId_AlunoId",
                table: "OfertaAlunos",
                columns: new[] { "OfertaDisciplinaId", "AlunoId" },
                unique: true,
                filter: "[Ativo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_OfertaDisciplinas_DisciplinaId_ProfessorId_Ano_Semestre_Periodo_TurmaId",
                table: "OfertaDisciplinas",
                columns: new[] { "DisciplinaId", "ProfessorId", "Ano", "Semestre", "Periodo", "TurmaId" },
                unique: true,
                filter: "[TurmaId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OfertaDisciplinas_ProfessorId",
                table: "OfertaDisciplinas",
                column: "ProfessorId");

            migrationBuilder.CreateIndex(
                name: "IX_OfertaDisciplinas_TurmaId",
                table: "OfertaDisciplinas",
                column: "TurmaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OfertaAlunos");

            migrationBuilder.DropTable(
                name: "OfertaDisciplinas");
        }
    }
}
