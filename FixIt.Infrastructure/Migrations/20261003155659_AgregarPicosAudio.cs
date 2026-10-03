using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FixIt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPicosAudio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PicosJson",
                table: "Mensajes",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PicosJson",
                table: "Mensajes");
        }
    }
}
