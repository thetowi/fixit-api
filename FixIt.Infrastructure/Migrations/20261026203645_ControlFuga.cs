using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FixIt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ControlFuga : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ContactoOculto",
                table: "Mensajes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "IntentosContactoOcultos",
                table: "Conversaciones",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UltimoIntentoContactoEn",
                table: "Conversaciones",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContactoOculto",
                table: "Mensajes");

            migrationBuilder.DropColumn(
                name: "IntentosContactoOcultos",
                table: "Conversaciones");

            migrationBuilder.DropColumn(
                name: "UltimoIntentoContactoEn",
                table: "Conversaciones");
        }
    }
}
