using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FixIt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarEncuestaPrestadores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RespuestasEncuesta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Rubros = table.Column<List<string>>(type: "text[]", nullable: false),
                    RubroOtro = table.Column<string>(type: "text", nullable: true),
                    Antiguedad = table.Column<string>(type: "text", nullable: true),
                    CanalesTrabajo = table.Column<List<string>>(type: "text[]", nullable: false),
                    CanalOtro = table.Column<string>(type: "text", nullable: true),
                    FrecuenciaSinTrabajo = table.Column<string>(type: "text", nullable: true),
                    InteresApp = table.Column<string>(type: "text", nullable: true),
                    ValorEsperado = table.Column<List<string>>(type: "text[]", nullable: false),
                    ValorOtro = table.Column<string>(type: "text", nullable: true),
                    DispuestoComision = table.Column<string>(type: "text", nullable: true),
                    PorcentajeComision = table.Column<string>(type: "text", nullable: true),
                    FormaCobroPreferida = table.Column<string>(type: "text", nullable: true),
                    PrioridadCobro = table.Column<string>(type: "text", nullable: true),
                    OpinionSena = table.Column<string>(type: "text", nullable: true),
                    OpinionPrepagoComision = table.Column<string>(type: "text", nullable: true),
                    OpinionGarantia = table.Column<string>(type: "text", nullable: true),
                    Dudas = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: true),
                    InteresProbar = table.Column<string>(type: "text", nullable: true),
                    Nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    WhatsApp = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Localidad = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RespuestasEncuesta", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RespuestasEncuesta_CreadoEn",
                table: "RespuestasEncuesta",
                column: "CreadoEn");

            migrationBuilder.CreateIndex(
                name: "IX_RespuestasEncuesta_WhatsApp",
                table: "RespuestasEncuesta",
                column: "WhatsApp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RespuestasEncuesta");
        }
    }
}
