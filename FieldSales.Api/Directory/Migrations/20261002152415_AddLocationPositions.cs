using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FieldSales.Api.Directory.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationPositions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Latitude",
                table: "Locations",
                type: "decimal(10,7)",
                precision: 10,
                scale: 7,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                table: "Locations",
                type: "decimal(10,7)",
                precision: 10,
                scale: 7,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PositionPrecision",
                table: "Locations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PositionSourceEircode",
                table: "Locations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PositionSourceTownId",
                table: "Locations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PositionedAt",
                table: "Locations",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LocationPositionHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Latitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: false),
                    Longitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: false),
                    Precision = table.Column<int>(type: "int", nullable: false),
                    SourceTownId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceEircode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PositionedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReplacedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationPositionHistory", x => x.Id);
                    table.CheckConstraint("CK_LocationPositionHistory_Position", "[Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180 AND\n(([Precision] = 0 AND [SourceTownId] IS NOT NULL AND [SourceEircode] IS NULL)\n OR ([Precision] = 1 AND [SourceTownId] IS NOT NULL AND [SourceEircode] IS NOT NULL AND LEN([SourceEircode]) > 0)\n OR ([Precision] = 2 AND [SourceTownId] IS NULL AND [SourceEircode] IS NULL))");
                    table.ForeignKey(
                        name: "FK_LocationPositionHistory_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Locations_Position",
                table: "Locations",
                sql: "([Latitude] IS NULL AND [Longitude] IS NULL AND [PositionPrecision] IS NULL\n  AND [PositionSourceTownId] IS NULL AND [PositionSourceEircode] IS NULL AND [PositionedAt] IS NULL)\nOR\n([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [PositionPrecision] IS NOT NULL\n  AND [Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180\n  AND [PositionedAt] IS NOT NULL AND\n  (([PositionPrecision] = 0 AND [PositionSourceTownId] IS NOT NULL AND [PositionSourceEircode] IS NULL)\n   OR ([PositionPrecision] = 1 AND [PositionSourceTownId] IS NOT NULL\n       AND [PositionSourceEircode] IS NOT NULL AND LEN([PositionSourceEircode]) > 0)\n   OR ([PositionPrecision] = 2 AND [PositionSourceTownId] IS NULL AND [PositionSourceEircode] IS NULL)))");

            migrationBuilder.CreateIndex(
                name: "IX_LocationPositionHistory_LocationId_ReplacedAt",
                table: "LocationPositionHistory",
                columns: new[] { "LocationId", "ReplacedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LocationPositionHistory");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Locations_Position",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "PositionPrecision",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "PositionSourceEircode",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "PositionSourceTownId",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "PositionedAt",
                table: "Locations");
        }
    }
}
