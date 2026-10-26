using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FixIt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CobrosResumenMensual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FundadorCategoriaId",
                table: "Usuarios",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ResumenesMensuales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PrestadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Periodo = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    CantidadTrabajos = table.Column<int>(type: "integer", nullable: false),
                    TrabajosBonificados = table.Column<int>(type: "integer", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Tope = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Ahorro = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    VenceEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MpPreferenceId = table.Column<string>(type: "text", nullable: true),
                    LinkPago = table.Column<string>(type: "text", nullable: true),
                    MpPaymentId = table.Column<string>(type: "text", nullable: true),
                    PagadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MedioPago = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    NotaAdmin = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResumenesMensuales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResumenesMensuales_Usuarios_PrestadorId",
                        column: x => x.PrestadorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResumenesMensuales_Estado_VenceEn",
                table: "ResumenesMensuales",
                columns: new[] { "Estado", "VenceEn" });

            migrationBuilder.CreateIndex(
                name: "IX_ResumenesMensuales_PrestadorId_Periodo",
                table: "ResumenesMensuales",
                columns: new[] { "PrestadorId", "Periodo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResumenesMensuales");

            migrationBuilder.DropColumn(
                name: "FundadorCategoriaId",
                table: "Usuarios");
        }
    }
}
