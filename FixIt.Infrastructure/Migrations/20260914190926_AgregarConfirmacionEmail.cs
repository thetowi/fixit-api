using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FixIt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarConfirmacionEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodigoVerificacionEmail",
                table: "Usuarios",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CodigoVerificacionExpira",
                table: "Usuarios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EmailConfirmado",
                table: "Usuarios",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CodigoVerificacionEmail",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "CodigoVerificacionExpira",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "EmailConfirmado",
                table: "Usuarios");
        }
    }
}
