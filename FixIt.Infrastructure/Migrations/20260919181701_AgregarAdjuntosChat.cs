using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FixIt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarAdjuntosChat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ImagenUrl",
                table: "Mensajes",
                newName: "ArchivoUrl");

            migrationBuilder.AddColumn<int>(
                name: "DuracionSegundos",
                table: "Mensajes",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DuracionSegundos",
                table: "Mensajes");

            migrationBuilder.RenameColumn(
                name: "ArchivoUrl",
                table: "Mensajes",
                newName: "ImagenUrl");
        }
    }
}
