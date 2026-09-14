using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FixIt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CambiarCalificacionACriterios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Puntuacion",
                table: "Calificaciones",
                newName: "Puntualidad");

            migrationBuilder.AddColumn<short>(
                name: "Calidad",
                table: "Calificaciones",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "Comunicacion",
                table: "Calificaciones",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "Garantia",
                table: "Calificaciones",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "Limpieza",
                table: "Calificaciones",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "Precio",
                table: "Calificaciones",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Calidad",
                table: "Calificaciones");

            migrationBuilder.DropColumn(
                name: "Comunicacion",
                table: "Calificaciones");

            migrationBuilder.DropColumn(
                name: "Garantia",
                table: "Calificaciones");

            migrationBuilder.DropColumn(
                name: "Limpieza",
                table: "Calificaciones");

            migrationBuilder.DropColumn(
                name: "Precio",
                table: "Calificaciones");

            migrationBuilder.RenameColumn(
                name: "Puntualidad",
                table: "Calificaciones",
                newName: "Puntuacion");
        }
    }
}
