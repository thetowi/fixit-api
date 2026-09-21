using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FixIt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarRecuperacionPassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CodigoRecuperacionExpira",
                table: "Usuarios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodigoRecuperacionPassword",
                table: "Usuarios",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CodigoRecuperacionExpira",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "CodigoRecuperacionPassword",
                table: "Usuarios");
        }
    }
}
