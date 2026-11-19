using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comicteca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Auditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Auditoria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Operacion = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Entidad = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    EntidadId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SerieId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TomosAfectados = table.Column<int>(type: "INTEGER", nullable: false),
                    Detalle = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Cuenta = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Origen = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auditoria", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_FechaUtc",
                table: "Auditoria",
                column: "FechaUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_SerieId",
                table: "Auditoria",
                column: "SerieId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Auditoria");
        }
    }
}
