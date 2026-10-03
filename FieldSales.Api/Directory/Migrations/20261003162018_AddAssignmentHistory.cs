using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FieldSales.Api.Directory.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssignmentHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActorSubject = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ActorName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Cause = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PreviousRepSubject = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true, collation: "Latin1_General_100_BIN2"),
                    PreviousRepName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    PreviousAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PreviousLevel = table.Column<int>(type: "int", nullable: true),
                    PreviousUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PreviousSourceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NewRepSubject = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true, collation: "Latin1_General_100_BIN2"),
                    NewRepName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    NewAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NewLevel = table.Column<int>(type: "int", nullable: true),
                    NewUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NewSourceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentHistory", x => x.Id);
                    table.CheckConstraint("CK_AssignmentHistory_Change", "[Id] <> '00000000-0000-0000-0000-000000000000'\nAND [OperationId] <> '00000000-0000-0000-0000-000000000000'\nAND [LocationId] <> '00000000-0000-0000-0000-000000000000'\nAND LEN([LocationName]) > 0 AND LEN([ActorSubject]) > 0 AND LEN([ActorName]) > 0\nAND [Cause] IN (0, 1, 2) AND DATEPART(TZOFFSET, [ChangedAt]) = 0\nAND ([PreviousRepSubject] IS NOT NULL OR [NewRepSubject] IS NOT NULL)\nAND ([PreviousRepSubject] IS NULL OR [NewRepSubject] IS NULL OR [PreviousRepSubject] <> [NewRepSubject])");
                    table.CheckConstraint("CK_AssignmentHistory_NewOwner", "([NewRepSubject] IS NULL AND [NewRepName] IS NULL AND [NewAssignmentId] IS NULL\n  AND [NewLevel] IS NULL AND [NewUnitId] IS NULL AND [NewSourceName] IS NULL)\nOR\n([NewRepSubject] IS NOT NULL AND LEN([NewRepSubject]) > 0\n  AND [NewRepName] IS NOT NULL AND LEN([NewRepName]) > 0\n  AND [NewAssignmentId] IS NOT NULL AND [NewAssignmentId] <> '00000000-0000-0000-0000-000000000000'\n  AND [NewLevel] IS NOT NULL AND [NewLevel] IN (0, 1, 2, 3)\n  AND [NewUnitId] IS NOT NULL AND [NewUnitId] <> '00000000-0000-0000-0000-000000000000'\n  AND [NewSourceName] IS NOT NULL AND LEN([NewSourceName]) > 0)");
                    table.CheckConstraint("CK_AssignmentHistory_PreviousOwner", "([PreviousRepSubject] IS NULL AND [PreviousRepName] IS NULL AND [PreviousAssignmentId] IS NULL\n  AND [PreviousLevel] IS NULL AND [PreviousUnitId] IS NULL AND [PreviousSourceName] IS NULL)\nOR\n([PreviousRepSubject] IS NOT NULL AND LEN([PreviousRepSubject]) > 0\n  AND [PreviousRepName] IS NOT NULL AND LEN([PreviousRepName]) > 0\n  AND [PreviousAssignmentId] IS NOT NULL AND [PreviousAssignmentId] <> '00000000-0000-0000-0000-000000000000'\n  AND [PreviousLevel] IS NOT NULL AND [PreviousLevel] IN (0, 1, 2, 3)\n  AND [PreviousUnitId] IS NOT NULL AND [PreviousUnitId] <> '00000000-0000-0000-0000-000000000000'\n  AND [PreviousSourceName] IS NOT NULL AND LEN([PreviousSourceName]) > 0)");
                    table.ForeignKey(
                        name: "FK_AssignmentHistory_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentHistory_LocationId_Sequence",
                table: "AssignmentHistory",
                columns: new[] { "LocationId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentHistory_NewRepSubject_Sequence",
                table: "AssignmentHistory",
                columns: new[] { "NewRepSubject", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentHistory_OperationId_LocationId",
                table: "AssignmentHistory",
                columns: new[] { "OperationId", "LocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentHistory_PreviousRepSubject_Sequence",
                table: "AssignmentHistory",
                columns: new[] { "PreviousRepSubject", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentHistory_Sequence",
                table: "AssignmentHistory",
                column: "Sequence",
                unique: true);

            migrationBuilder.Sql("""
                CREATE TRIGGER [TR_AssignmentHistory_AppendOnly]
                ON [AssignmentHistory] INSTEAD OF UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51021, 'Assignment history is append-only.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssignmentHistory");
        }
    }
}
