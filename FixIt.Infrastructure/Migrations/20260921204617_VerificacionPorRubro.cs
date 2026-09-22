using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FixIt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VerificacionPorRubro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MatriculaUrl",
                table: "Usuarios");

            migrationBuilder.AddColumn<int>(
                name: "EstadoVerificacion",
                table: "PrestadorCategorias",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MatriculaUrl",
                table: "PrestadorCategorias",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoRechazoVerificacion",
                table: "PrestadorCategorias",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VerificacionEnviadaEn",
                table: "PrestadorCategorias",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstadoVerificacion",
                table: "PrestadorCategorias");

            migrationBuilder.DropColumn(
                name: "MatriculaUrl",
                table: "PrestadorCategorias");

            migrationBuilder.DropColumn(
                name: "MotivoRechazoVerificacion",
                table: "PrestadorCategorias");

            migrationBuilder.DropColumn(
                name: "VerificacionEnviadaEn",
                table: "PrestadorCategorias");

            migrationBuilder.AddColumn<string>(
                name: "MatriculaUrl",
                table: "Usuarios",
                type: "text",
                nullable: true);
        }
    }
}
