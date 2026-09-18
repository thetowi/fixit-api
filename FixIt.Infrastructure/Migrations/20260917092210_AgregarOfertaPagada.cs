using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FixIt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarOfertaPagada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MensajeOfertaId",
                table: "Ordenes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OfertaPagada",
                table: "Mensajes",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MensajeOfertaId",
                table: "Ordenes");

            migrationBuilder.DropColumn(
                name: "OfertaPagada",
                table: "Mensajes");
        }
    }
}
