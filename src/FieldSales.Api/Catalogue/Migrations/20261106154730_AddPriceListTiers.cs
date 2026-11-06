using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FieldSales.Api.Catalogue.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceListTiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PriceTiers_PercentOff",
                table: "PriceTiers");

            migrationBuilder.AlterColumn<decimal>(
                name: "PercentOff",
                table: "PriceTiers",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)",
                oldPrecision: 5,
                oldScale: 2);

            migrationBuilder.AddColumn<string>(
                name: "Shape",
                table: "PriceTiers",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: false,
                // Every tier stored before WI-061 is a percentage tier (WI-061 D5).
                defaultValue: "Percentage");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PriceTiers_Terms",
                table: "PriceTiers",
                sql: "LEN([Name]) > 0 AND (([Shape] = 'Percentage' AND [PercentOff] IS NOT NULL AND [PercentOff] > 0 AND [PercentOff] < 100) OR ([Shape] = 'PriceList' AND [PercentOff] IS NULL))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PriceTiers_Terms",
                table: "PriceTiers");

            migrationBuilder.DropColumn(
                name: "Shape",
                table: "PriceTiers");

            migrationBuilder.AlterColumn<decimal>(
                name: "PercentOff",
                table: "PriceTiers",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)",
                oldPrecision: 5,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PriceTiers_PercentOff",
                table: "PriceTiers",
                sql: "[PercentOff] > 0 AND [PercentOff] < 100 AND LEN([Name]) > 0");
        }
    }
}
