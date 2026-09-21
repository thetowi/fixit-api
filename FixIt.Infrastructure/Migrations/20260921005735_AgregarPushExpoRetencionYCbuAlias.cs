using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FixIt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPushExpoRetencionYCbuAlias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CbuOAlias",
                table: "Usuarios",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitularCuentaCobro",
                table: "Usuarios",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoReembolso",
                table: "Pagos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TransferenciaPrestadorConfirmadaEn",
                table: "Pagos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SuscripcionesPushExpo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpoPushToken = table.Column<string>(type: "text", nullable: false),
                    CreadaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuscripcionesPushExpo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SuscripcionesPushExpo_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SuscripcionesPushExpo_ExpoPushToken",
                table: "SuscripcionesPushExpo",
                column: "ExpoPushToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SuscripcionesPushExpo_UsuarioId",
                table: "SuscripcionesPushExpo",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SuscripcionesPushExpo");

            migrationBuilder.DropColumn(
                name: "CbuOAlias",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "TitularCuentaCobro",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "MotivoReembolso",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "TransferenciaPrestadorConfirmadaEn",
                table: "Pagos");
        }
    }
}
