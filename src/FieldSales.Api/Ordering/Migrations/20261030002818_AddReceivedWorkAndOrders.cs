using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FieldSales.Api.Ordering.Migrations
{
    /// <inheritdoc />
    public partial class AddReceivedWorkAndOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReceivedWork",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    RepSubject = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ReceivedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ContentSha256 = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResentWithChangesCount = table.Column<int>(type: "int", nullable: false),
                    LastResentWithChangesUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceivedWork", x => x.Id);
                    table.CheckConstraint("CK_ReceivedWork_Item", "[Id] <> '00000000-0000-0000-0000-000000000000' AND LEN([RepSubject]) > 0\nAND LEN([ContentSha256]) = 64 AND [ResentWithChangesCount] >= 0 AND DATEPART(TZOFFSET, [ReceivedUtc]) = 0");
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapturingRep = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false, collation: "Latin1_General_100_BIN2"),
                    TakenOn = table.Column<DateOnly>(type: "date", nullable: false),
                    CallId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SnapshotVersion = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    SnapshotBuiltUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReceivedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.CheckConstraint("CK_Orders_Received", "[LocationId] <> '00000000-0000-0000-0000-000000000000' AND [OrderedById] <> '00000000-0000-0000-0000-000000000000'\nAND LEN([CapturingRep]) > 0 AND LEN([SnapshotVersion]) > 0 AND DATEPART(TZOFFSET, [ReceivedUtc]) = 0");
                    table.ForeignKey(
                        name: "FK_Orders_ReceivedWork_Id",
                        column: x => x.Id,
                        principalTable: "ReceivedWork",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status_ReceivedUtc",
                table: "Orders",
                columns: new[] { "Status", "ReceivedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "ReceivedWork");
        }
    }
}
