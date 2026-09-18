using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FixIt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarVerificacionDireccion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "DireccionLat",
                table: "Usuarios",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DireccionLon",
                table: "Usuarios",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DireccionVerificada",
                table: "Usuarios",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DireccionLat",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "DireccionLon",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "DireccionVerificada",
                table: "Usuarios");
        }
    }
}
