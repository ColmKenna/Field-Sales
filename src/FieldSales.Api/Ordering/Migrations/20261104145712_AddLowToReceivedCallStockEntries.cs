using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FieldSales.Api.Ordering.Migrations
{
    /// <inheritdoc />
    public partial class AddLowToReceivedCallStockEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Low",
                table: "ReceivedCallStockEntries",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Low",
                table: "ReceivedCallStockEntries");
        }
    }
}
