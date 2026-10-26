using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FixIt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NuevoSistemaCobros : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CobroSuspendidoEn",
                table: "Usuarios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FundadorDesde",
                table: "Usuarios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FundadorHasta",
                table: "Usuarios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CheckInDistanciaMetros",
                table: "Ordenes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CheckInEn",
                table: "Ordenes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckInLat",
                table: "Ordenes",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckInLng",
                table: "Ordenes",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodigoCierre",
                table: "Ordenes",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfirmacionTipo",
                table: "Ordenes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IntentosCodigoFallidos",
                table: "Ordenes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ProblemaClienteComentario",
                table: "Ordenes",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProblemaClienteReportadoEn",
                table: "Ordenes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TerminadoPorPrestadorEn",
                table: "Ordenes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CargosTrabajo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrdenId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrestadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Periodo = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    Monto = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    MontoTarifa = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Bonificado = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CargosTrabajo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CargosTrabajo_Ordenes_OrdenId",
                        column: x => x.OrdenId,
                        principalTable: "Ordenes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CargosTrabajo_Usuarios_PrestadorId",
                        column: x => x.PrestadorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TarifasCobro",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MontoPorTrabajo = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    TopeMensual = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    VigenteDesde = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TarifasCobro", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "TarifasCobro",
                columns: new[] { "Id", "CreadoEn", "MontoPorTrabajo", "TopeMensual", "VigenteDesde" },
                values: new object[] { new Guid("6f1c2a7e-3b0d-4c55-9a1e-2d7b8f3c0a01"), new DateTimeOffset(new DateTime(2026, 10, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 3000m, 30000m, new DateTimeOffset(new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.CreateIndex(
                name: "IX_Ordenes_TerminadoPorPrestadorEn",
                table: "Ordenes",
                column: "TerminadoPorPrestadorEn");

            migrationBuilder.CreateIndex(
                name: "IX_CargosTrabajo_OrdenId",
                table: "CargosTrabajo",
                column: "OrdenId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CargosTrabajo_PrestadorId_Periodo",
                table: "CargosTrabajo",
                columns: new[] { "PrestadorId", "Periodo" });

            migrationBuilder.CreateIndex(
                name: "IX_TarifasCobro_VigenteDesde",
                table: "TarifasCobro",
                column: "VigenteDesde");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CargosTrabajo");

            migrationBuilder.DropTable(
                name: "TarifasCobro");

            migrationBuilder.DropIndex(
                name: "IX_Ordenes_TerminadoPorPrestadorEn",
                table: "Ordenes");

            migrationBuilder.DropColumn(
                name: "CobroSuspendidoEn",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "FundadorDesde",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "FundadorHasta",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "CheckInDistanciaMetros",
                table: "Ordenes");

            migrationBuilder.DropColumn(
                name: "CheckInEn",
                table: "Ordenes");

            migrationBuilder.DropColumn(
                name: "CheckInLat",
                table: "Ordenes");

            migrationBuilder.DropColumn(
                name: "CheckInLng",
                table: "Ordenes");

            migrationBuilder.DropColumn(
                name: "CodigoCierre",
                table: "Ordenes");

            migrationBuilder.DropColumn(
                name: "ConfirmacionTipo",
                table: "Ordenes");

            migrationBuilder.DropColumn(
                name: "IntentosCodigoFallidos",
                table: "Ordenes");

            migrationBuilder.DropColumn(
                name: "ProblemaClienteComentario",
                table: "Ordenes");

            migrationBuilder.DropColumn(
                name: "ProblemaClienteReportadoEn",
                table: "Ordenes");

            migrationBuilder.DropColumn(
                name: "TerminadoPorPrestadorEn",
                table: "Ordenes");
        }
    }
}
