using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FieldSales.Api.Catalogue.Migrations
{
    /// <inheritdoc />
    public partial class AddBrandsAndProductReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PrimaryBrandId",
                table: "Products",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Brands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Latin1_General_100_CI_AS"),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Brands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductAlternativeBrands",
                columns: table => new
                {
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductAlternativeBrands", x => new { x.ProductId, x.BrandId });
                    table.ForeignKey(
                        name: "FK_ProductAlternativeBrands_Brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductAlternativeBrands_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_PrimaryBrandId",
                table: "Products",
                column: "PrimaryBrandId");

            migrationBuilder.CreateIndex(
                name: "UX_Brands_Name",
                table: "Brands",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductAlternativeBrands_BrandId",
                table: "ProductAlternativeBrands",
                column: "BrandId");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Brands_PrimaryBrandId",
                table: "Products",
                column: "PrimaryBrandId",
                principalTable: "Brands",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Brands_PrimaryBrandId",
                table: "Products");

            migrationBuilder.DropTable(
                name: "ProductAlternativeBrands");

            migrationBuilder.DropTable(
                name: "Brands");

            migrationBuilder.DropIndex(
                name: "IX_Products_PrimaryBrandId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PrimaryBrandId",
                table: "Products");
        }
    }
}
