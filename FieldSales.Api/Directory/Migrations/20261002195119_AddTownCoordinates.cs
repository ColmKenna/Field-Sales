using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FieldSales.Api.Directory.Migrations
{
    /// <inheritdoc />
    public partial class AddTownCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Latitude",
                table: "Towns",
                type: "decimal(10,7)",
                precision: 10,
                scale: 7,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                table: "Towns",
                type: "decimal(10,7)",
                precision: 10,
                scale: 7,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Towns_Coordinates",
                table: "Towns",
                sql: "([Latitude] IS NULL AND [Longitude] IS NULL) OR\n([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL\n  AND [Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Towns_Coordinates",
                table: "Towns");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Towns");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Towns");
        }
    }
}
