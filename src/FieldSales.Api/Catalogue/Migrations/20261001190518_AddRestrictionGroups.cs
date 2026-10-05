using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FieldSales.Api.Catalogue.Migrations
{
    /// <inheritdoc />
    public partial class AddRestrictionGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RestrictionGroupId",
                table: "Products",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RestrictionGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Latin1_General_100_CI_AS"),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestrictionGroups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_RestrictionGroupId",
                table: "Products",
                column: "RestrictionGroupId");

            migrationBuilder.CreateIndex(
                name: "UX_RestrictionGroups_Name",
                table: "RestrictionGroups",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_RestrictionGroups_RestrictionGroupId",
                table: "Products",
                column: "RestrictionGroupId",
                principalTable: "RestrictionGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_RestrictionGroups_RestrictionGroupId",
                table: "Products");

            migrationBuilder.DropTable(
                name: "RestrictionGroups");

            migrationBuilder.DropIndex(
                name: "IX_Products_RestrictionGroupId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "RestrictionGroupId",
                table: "Products");
        }
    }
}
