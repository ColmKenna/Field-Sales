using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FieldSales.Api.Catalogue.Migrations
{
    /// <inheritdoc />
    public partial class AddProductQuantityRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MinimumQuantity",
                table: "Products",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QuantityStep",
                table: "Products",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_QuantityRules",
                table: "Products",
                sql: "([Unit] = 'Each' AND [QuantityStep] IS NULL AND [MinimumQuantity] IS NULL) OR ([Unit] IN ('kg', 'litre', 'metre') AND [QuantityStep] IS NOT NULL AND [MinimumQuantity] IS NOT NULL AND [QuantityStep] > 0 AND [MinimumQuantity] > 0 AND [MinimumQuantity] % NULLIF([QuantityStep], 0) = 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_QuantityRules",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MinimumQuantity",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "QuantityStep",
                table: "Products");
        }
    }
}
