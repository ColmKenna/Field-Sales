using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FieldSales.Api.Catalogue.Migrations
{
    /// <inheritdoc />
    public partial class AddProductQuantityBreaks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductQuantityBreaks",
                columns: table => new
                {
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromQuantity = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductQuantityBreaks", x => new { x.ProductId, x.FromQuantity });
                    table.CheckConstraint("CK_ProductQuantityBreaks_Positive", "[FromQuantity] > 0 AND [Price] > 0");
                    table.ForeignKey(
                        name: "FK_ProductQuantityBreaks_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductQuantityBreaks");
        }
    }
}
