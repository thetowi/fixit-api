using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FixIt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SepararCbuYAlias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CbuOAlias",
                table: "Usuarios",
                newName: "Cbu");

            migrationBuilder.AddColumn<string>(
                name: "Alias",
                table: "Usuarios",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Alias",
                table: "Usuarios");

            migrationBuilder.RenameColumn(
                name: "Cbu",
                table: "Usuarios",
                newName: "CbuOAlias");
        }
    }
}
