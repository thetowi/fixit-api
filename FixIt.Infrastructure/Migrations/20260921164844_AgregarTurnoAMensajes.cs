using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FixIt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarTurnoAMensajes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TurnoDuracionMinutos",
                table: "Mensajes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TurnoFechaHora",
                table: "Mensajes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TurnoOrdenId",
                table: "Mensajes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TurnoVigente",
                table: "Mensajes",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TurnoDuracionMinutos",
                table: "Mensajes");

            migrationBuilder.DropColumn(
                name: "TurnoFechaHora",
                table: "Mensajes");

            migrationBuilder.DropColumn(
                name: "TurnoOrdenId",
                table: "Mensajes");

            migrationBuilder.DropColumn(
                name: "TurnoVigente",
                table: "Mensajes");
        }
    }
}
