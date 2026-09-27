using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FixIt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarFotosEnResenasYRepost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CalificacionFotoId",
                table: "FotosTrabajo",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CalificacionFotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CalificacionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EstadoRepost = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RepostSolicitadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RepostRespondidoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalificacionFotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalificacionFotos_Calificaciones_CalificacionId",
                        column: x => x.CalificacionId,
                        principalTable: "Calificaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FotosTrabajo_CalificacionFotoId",
                table: "FotosTrabajo",
                column: "CalificacionFotoId");

            migrationBuilder.CreateIndex(
                name: "IX_CalificacionFotos_CalificacionId",
                table: "CalificacionFotos",
                column: "CalificacionId");

            migrationBuilder.CreateIndex(
                name: "IX_CalificacionFotos_EstadoRepost",
                table: "CalificacionFotos",
                column: "EstadoRepost");

            migrationBuilder.AddForeignKey(
                name: "FK_FotosTrabajo_CalificacionFotos_CalificacionFotoId",
                table: "FotosTrabajo",
                column: "CalificacionFotoId",
                principalTable: "CalificacionFotos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FotosTrabajo_CalificacionFotos_CalificacionFotoId",
                table: "FotosTrabajo");

            migrationBuilder.DropTable(
                name: "CalificacionFotos");

            migrationBuilder.DropIndex(
                name: "IX_FotosTrabajo_CalificacionFotoId",
                table: "FotosTrabajo");

            migrationBuilder.DropColumn(
                name: "CalificacionFotoId",
                table: "FotosTrabajo");
        }
    }
}
