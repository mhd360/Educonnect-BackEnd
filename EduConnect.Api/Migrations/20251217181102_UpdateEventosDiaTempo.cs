using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduConnect.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateEventosDiaTempo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Eventos_OfertaDisciplinaId_Inicio",
                table: "Eventos");

            migrationBuilder.DropColumn(
                name: "Fim",
                table: "Eventos");

            migrationBuilder.DropColumn(
                name: "Inicio",
                table: "Eventos");

            migrationBuilder.AddColumn<DateOnly>(
                name: "Data",
                table: "Eventos",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<bool>(
                name: "DiaInteiro",
                table: "Eventos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "HoraFim",
                table: "Eventos",
                type: "time(0)",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "HoraInicio",
                table: "Eventos",
                type: "time(0)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Eventos_OfertaDisciplinaId_Data",
                table: "Eventos",
                columns: new[] { "OfertaDisciplinaId", "Data" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Eventos_OfertaDisciplinaId_Data",
                table: "Eventos");

            migrationBuilder.DropColumn(
                name: "Data",
                table: "Eventos");

            migrationBuilder.DropColumn(
                name: "DiaInteiro",
                table: "Eventos");

            migrationBuilder.DropColumn(
                name: "HoraFim",
                table: "Eventos");

            migrationBuilder.DropColumn(
                name: "HoraInicio",
                table: "Eventos");

            migrationBuilder.AddColumn<DateTime>(
                name: "Fim",
                table: "Eventos",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "Inicio",
                table: "Eventos",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_Eventos_OfertaDisciplinaId_Inicio",
                table: "Eventos",
                columns: new[] { "OfertaDisciplinaId", "Inicio" });
        }
    }
}
