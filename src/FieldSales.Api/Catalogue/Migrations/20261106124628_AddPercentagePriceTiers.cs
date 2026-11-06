using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FieldSales.Api.Catalogue.Migrations
{
    /// <inheritdoc />
    public partial class AddPercentagePriceTiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PriceTiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS"),
                    PercentOff = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceTiers", x => x.Id);
                    table.CheckConstraint("CK_PriceTiers_PercentOff", "[PercentOff] > 0 AND [PercentOff] < 100 AND LEN([Name]) > 0");
                });

            migrationBuilder.CreateTable(
                name: "PriceTierOverrides",
                columns: table => new
                {
                    PriceTierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PercentOff = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    FixedPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SetOn = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceTierOverrides", x => new { x.PriceTierId, x.ProductId });
                    table.CheckConstraint("CK_PriceTierOverrides_Term", "([PercentOff] IS NOT NULL AND [FixedPrice] IS NULL AND [PercentOff] > 0 AND [PercentOff] < 100) OR ([PercentOff] IS NULL AND [FixedPrice] IS NOT NULL AND [FixedPrice] > 0)");
                    table.ForeignKey(
                        name: "FK_PriceTierOverrides_PriceTiers_PriceTierId",
                        column: x => x.PriceTierId,
                        principalTable: "PriceTiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PriceTierOverrides_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PriceTierOverrides_ProductId",
                table: "PriceTierOverrides",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "UX_PriceTiers_Name",
                table: "PriceTiers",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PriceTierOverrides");

            migrationBuilder.DropTable(
                name: "PriceTiers");
        }
    }
}
