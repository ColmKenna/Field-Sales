using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FieldSales.Api.Ordering.Migrations
{
    /// <inheritdoc />
    public partial class AddReceivedCallsAndStockEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReceivedCalls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapturingRep = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false, collation: "Latin1_General_100_BIN2"),
                    CalledOn = table.Column<DateOnly>(type: "date", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Channel = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    ReceivedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceivedCalls", x => x.Id);
                    table.CheckConstraint("CK_ReceivedCalls_Received", "[LocationId] <> '00000000-0000-0000-0000-000000000000' AND LEN([CapturingRep]) > 0\nAND DATEPART(TZOFFSET, [ReceivedUtc]) = 0");
                    table.ForeignKey(
                        name: "FK_ReceivedCalls_ReceivedWork_Id",
                        column: x => x.Id,
                        principalTable: "ReceivedWork",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReceivedCallStockEntries",
                columns: table => new
                {
                    CallId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Unit = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Count = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceivedCallStockEntries", x => new { x.CallId, x.ProductId });
                    table.CheckConstraint("CK_ReceivedCallStockEntries_Entry", "[ProductId] <> '00000000-0000-0000-0000-000000000000' AND LEN([Unit]) > 0 AND ([Count] IS NULL OR [Count] >= 0)");
                    table.ForeignKey(
                        name: "FK_ReceivedCallStockEntries_ReceivedCalls_CallId",
                        column: x => x.CallId,
                        principalTable: "ReceivedCalls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_LocationId_Status_TakenOn",
                table: "Orders",
                columns: new[] { "LocationId", "Status", "TakenOn" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceivedCalls_LocationId_Channel_SavedAt",
                table: "ReceivedCalls",
                columns: new[] { "LocationId", "Channel", "SavedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReceivedCallStockEntries");

            migrationBuilder.DropTable(
                name: "ReceivedCalls");

            migrationBuilder.DropIndex(
                name: "IX_Orders_LocationId_Status_TakenOn",
                table: "Orders");
        }
    }
}
