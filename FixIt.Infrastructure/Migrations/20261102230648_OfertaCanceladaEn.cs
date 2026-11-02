using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FixIt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OfertaCanceladaEn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OfertaCanceladaEn",
                table: "Mensajes",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OfertaCanceladaEn",
                table: "Mensajes");
        }
    }
}
