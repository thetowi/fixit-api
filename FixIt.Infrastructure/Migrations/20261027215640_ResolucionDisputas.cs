using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FixIt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ResolucionDisputas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisputaNotaAdmin",
                table: "Ordenes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisputaResolucion",
                table: "Ordenes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DisputaResueltaEn",
                table: "Ordenes",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisputaNotaAdmin",
                table: "Ordenes");

            migrationBuilder.DropColumn(
                name: "DisputaResolucion",
                table: "Ordenes");

            migrationBuilder.DropColumn(
                name: "DisputaResueltaEn",
                table: "Ordenes");
        }
    }
}
