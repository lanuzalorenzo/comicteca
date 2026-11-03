using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comicteca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Series",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Titulo = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Editorial = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Series", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tomos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SerieId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Numero = table.Column<int>(type: "INTEGER", nullable: true),
                    Edicion = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tomos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tomos_Series_SerieId",
                        column: x => x.SerieId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Series_Titulo_Editorial",
                table: "Series",
                columns: new[] { "Titulo", "Editorial" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tomos_SerieId_Numero",
                table: "Tomos",
                columns: new[] { "SerieId", "Numero" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tomos");

            migrationBuilder.DropTable(
                name: "Series");
        }
    }
}
