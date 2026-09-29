using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FixIt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarAvisoPagoVisto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AvisoPagoVistoClienteEn",
                table: "Conversaciones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AvisoPagoVistoPrestadorEn",
                table: "Conversaciones",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvisoPagoVistoClienteEn",
                table: "Conversaciones");

            migrationBuilder.DropColumn(
                name: "AvisoPagoVistoPrestadorEn",
                table: "Conversaciones");
        }
    }
}
