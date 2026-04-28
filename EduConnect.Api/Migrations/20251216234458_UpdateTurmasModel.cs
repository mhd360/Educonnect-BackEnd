using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduConnect.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTurmasModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Turmas_Nome_AnoLetivo",
                table: "Turmas");

            migrationBuilder.DropColumn(
                name: "Descricao",
                table: "Turmas");

            migrationBuilder.RenameColumn(
                name: "AnoLetivo",
                table: "Turmas",
                newName: "Ano");

            migrationBuilder.AlterColumn<string>(
                name: "Nome",
                table: "Turmas",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "Periodo",
                table: "Turmas",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<byte>(
                name: "Semestre",
                table: "Turmas",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.CreateIndex(
                name: "IX_Turmas_Ano_Semestre_Periodo",
                table: "Turmas",
                columns: new[] { "Ano", "Semestre", "Periodo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Turmas_Nome",
                table: "Turmas",
                column: "Nome",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Turmas_Ano_Semestre_Periodo",
                table: "Turmas");

            migrationBuilder.DropIndex(
                name: "IX_Turmas_Nome",
                table: "Turmas");

            migrationBuilder.DropColumn(
                name: "Periodo",
                table: "Turmas");

            migrationBuilder.DropColumn(
                name: "Semestre",
                table: "Turmas");

            migrationBuilder.RenameColumn(
                name: "Ano",
                table: "Turmas",
                newName: "AnoLetivo");

            migrationBuilder.AlterColumn<string>(
                name: "Nome",
                table: "Turmas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AddColumn<string>(
                name: "Descricao",
                table: "Turmas",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Turmas_Nome_AnoLetivo",
                table: "Turmas",
                columns: new[] { "Nome", "AnoLetivo" },
                unique: true);
        }
    }
}
