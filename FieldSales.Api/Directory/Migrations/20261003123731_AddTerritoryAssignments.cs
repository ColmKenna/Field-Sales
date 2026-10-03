using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FieldSales.Api.Directory.Migrations
{
    /// <inheritdoc />
    public partial class AddTerritoryAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RepReportingLines",
                columns: table => new
                {
                    RepSubject = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ManagerSubject = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepReportingLines", x => x.RepSubject);
                    table.CheckConstraint("CK_RepReportingLines_Subjects", "LEN([RepSubject]) > 0 AND LEN([ManagerSubject]) > 0");
                });

            migrationBuilder.CreateTable(
                name: "TerritoryAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepSubject = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false, collation: "Latin1_General_100_BIN2"),
                    RegionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CountyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TownId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TerritoryAssignments", x => x.Id);
                    table.CheckConstraint("CK_TerritoryAssignments_OneTarget", "(CASE WHEN [RegionId] IS NULL THEN 0 ELSE 1 END\n + CASE WHEN [CountyId] IS NULL THEN 0 ELSE 1 END\n + CASE WHEN [TownId] IS NULL THEN 0 ELSE 1 END\n + CASE WHEN [LocationId] IS NULL THEN 0 ELSE 1 END) = 1\nAND ([RegionId] IS NULL OR [RegionId] <> '00000000-0000-0000-0000-000000000000')\nAND ([CountyId] IS NULL OR [CountyId] <> '00000000-0000-0000-0000-000000000000')\nAND ([TownId] IS NULL OR [TownId] <> '00000000-0000-0000-0000-000000000000')\nAND ([LocationId] IS NULL OR [LocationId] <> '00000000-0000-0000-0000-000000000000')");
                    table.ForeignKey(
                        name: "FK_TerritoryAssignments_Counties_CountyId",
                        column: x => x.CountyId,
                        principalTable: "Counties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TerritoryAssignments_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TerritoryAssignments_Regions_RegionId",
                        column: x => x.RegionId,
                        principalTable: "Regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TerritoryAssignments_RepReportingLines_RepSubject",
                        column: x => x.RepSubject,
                        principalTable: "RepReportingLines",
                        principalColumn: "RepSubject",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TerritoryAssignments_Towns_TownId",
                        column: x => x.TownId,
                        principalTable: "Towns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RepReportingLines_ManagerSubject",
                table: "RepReportingLines",
                column: "ManagerSubject");

            migrationBuilder.CreateIndex(
                name: "IX_TerritoryAssignments_CountyId",
                table: "TerritoryAssignments",
                column: "CountyId",
                unique: true,
                filter: "[CountyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TerritoryAssignments_LocationId",
                table: "TerritoryAssignments",
                column: "LocationId",
                unique: true,
                filter: "[LocationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TerritoryAssignments_RegionId",
                table: "TerritoryAssignments",
                column: "RegionId",
                unique: true,
                filter: "[RegionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TerritoryAssignments_RepSubject",
                table: "TerritoryAssignments",
                column: "RepSubject");

            migrationBuilder.CreateIndex(
                name: "IX_TerritoryAssignments_TownId",
                table: "TerritoryAssignments",
                column: "TownId",
                unique: true,
                filter: "[TownId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TerritoryAssignments");

            migrationBuilder.DropTable(
                name: "RepReportingLines");
        }
    }
}
