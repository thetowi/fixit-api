using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FixIt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarVerificacionPrestadores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AntecedentesPenalesUrl",
                table: "Usuarios",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstadoVerificacion",
                table: "Usuarios",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MatriculaUrl",
                table: "Usuarios",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoRechazoVerificacion",
                table: "Usuarios",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VerificacionEnviadaEn",
                table: "Usuarios",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AntecedentesPenalesUrl",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "EstadoVerificacion",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "MatriculaUrl",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "MotivoRechazoVerificacion",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "VerificacionEnviadaEn",
                table: "Usuarios");
        }
    }
}
