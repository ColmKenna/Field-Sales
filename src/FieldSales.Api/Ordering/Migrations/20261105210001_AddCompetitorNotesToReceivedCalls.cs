using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FieldSales.Api.Ordering.Migrations
{
    /// <inheritdoc />
    public partial class AddCompetitorNotesToReceivedCalls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReceivedCallCompetitorNotes",
                columns: table => new
                {
                    CallId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    LinkTo = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    RangeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceivedCallCompetitorNotes", x => new { x.CallId, x.Id });
                    table.CheckConstraint("CK_ReceivedCallCompetitorNotes_Link", "LEN([Text]) > 0 AND (\n    ([LinkTo] = 'Range' AND [RangeId] IS NOT NULL AND [CategoryId] IS NULL)\n    OR ([LinkTo] = 'Category' AND [CategoryId] IS NOT NULL AND [RangeId] IS NULL)\n    OR ([LinkTo] IN ('None', 'Products') AND [RangeId] IS NULL AND [CategoryId] IS NULL))");
                    table.ForeignKey(
                        name: "FK_ReceivedCallCompetitorNotes_ReceivedCalls_CallId",
                        column: x => x.CallId,
                        principalTable: "ReceivedCalls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReceivedCallCompetitorNoteProducts",
                columns: table => new
                {
                    CallId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceivedCallCompetitorNoteProducts", x => new { x.CallId, x.NoteId, x.ProductId });
                    table.CheckConstraint("CK_ReceivedCallCompetitorNoteProducts_Product", "[ProductId] <> '00000000-0000-0000-0000-000000000000'");
                    table.ForeignKey(
                        name: "FK_ReceivedCallCompetitorNoteProducts_ReceivedCallCompetitorNotes_CallId_NoteId",
                        columns: x => new { x.CallId, x.NoteId },
                        principalTable: "ReceivedCallCompetitorNotes",
                        principalColumns: new[] { "CallId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReceivedCallCompetitorNoteProducts");

            migrationBuilder.DropTable(
                name: "ReceivedCallCompetitorNotes");
        }
    }
}
