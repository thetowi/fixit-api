using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FixIt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarConexionMercadoPagoPrestador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MercadoPagoAccessToken",
                table: "Usuarios",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MercadoPagoOAuthState",
                table: "Usuarios",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MercadoPagoOAuthStateExpira",
                table: "Usuarios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MercadoPagoRefreshToken",
                table: "Usuarios",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MercadoPagoTokenExpiraEn",
                table: "Usuarios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MercadoPagoUserId",
                table: "Usuarios",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TrabajosPagados",
                table: "Usuarios",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MercadoPagoAccessToken",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "MercadoPagoOAuthState",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "MercadoPagoOAuthStateExpira",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "MercadoPagoRefreshToken",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "MercadoPagoTokenExpiraEn",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "MercadoPagoUserId",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "TrabajosPagados",
                table: "Usuarios");
        }
    }
}
