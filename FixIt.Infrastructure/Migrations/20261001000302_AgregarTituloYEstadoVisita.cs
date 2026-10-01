using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FixIt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarTituloYEstadoVisita : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Titulo",
                table: "Visitas",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VisitaEstado",
                table: "Mensajes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VisitaTitulo",
                table: "Mensajes",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Titulo",
                table: "Visitas");

            migrationBuilder.DropColumn(
                name: "VisitaEstado",
                table: "Mensajes");

            migrationBuilder.DropColumn(
                name: "VisitaTitulo",
                table: "Mensajes");
        }
    }
}
