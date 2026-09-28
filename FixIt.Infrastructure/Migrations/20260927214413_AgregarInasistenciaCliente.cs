using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FixIt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarInasistenciaCliente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InasistenciaClienteComentario",
                table: "Ordenes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "InasistenciaClienteReportadaEn",
                table: "Ordenes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InasistenciaResolucion",
                table: "Ordenes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "InasistenciaResueltaEn",
                table: "Ordenes",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InasistenciaClienteComentario",
                table: "Ordenes");

            migrationBuilder.DropColumn(
                name: "InasistenciaClienteReportadaEn",
                table: "Ordenes");

            migrationBuilder.DropColumn(
                name: "InasistenciaResolucion",
                table: "Ordenes");

            migrationBuilder.DropColumn(
                name: "InasistenciaResueltaEn",
                table: "Ordenes");
        }
    }
}
