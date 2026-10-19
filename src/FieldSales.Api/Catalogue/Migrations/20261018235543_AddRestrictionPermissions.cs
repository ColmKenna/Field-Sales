using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FieldSales.Api.Catalogue.Migrations
{
    /// <inheritdoc />
    public partial class AddRestrictionPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RestrictionPermissionChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RepSubject = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false, collation: "Latin1_General_100_BIN2"),
                    RepName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    RestrictionGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestrictionGroupName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Change = table.Column<int>(type: "int", nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActorSubject = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ActorName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestrictionPermissionChanges", x => x.Id);
                    table.CheckConstraint("CK_RestrictionPermissionChanges_Change", "[Id] <> '00000000-0000-0000-0000-000000000000'\nAND [RestrictionGroupId] <> '00000000-0000-0000-0000-000000000000'\nAND LEN([RepSubject]) > 0 AND LEN([RepName]) > 0 AND LEN([RestrictionGroupName]) > 0\nAND LEN([ActorSubject]) > 0 AND LEN([ActorName]) > 0 AND [ActorSubject] <> [RepSubject]\nAND [Change] IN (0, 1) AND DATEPART(TZOFFSET, [ChangedAt]) = 0\nAND ([Reason] IS NULL OR LEN([Reason]) > 0)");
                });

            migrationBuilder.CreateTable(
                name: "RestrictionPermissions",
                columns: table => new
                {
                    RepSubject = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false, collation: "Latin1_General_100_BIN2"),
                    RestrictionGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestrictionPermissions", x => new { x.RepSubject, x.RestrictionGroupId });
                    table.CheckConstraint("CK_RestrictionPermissions_Grant", "LEN([RepSubject]) > 0 AND [RestrictionGroupId] <> '00000000-0000-0000-0000-000000000000'");
                    table.ForeignKey(
                        name: "FK_RestrictionPermissions_RestrictionGroups_RestrictionGroupId",
                        column: x => x.RestrictionGroupId,
                        principalTable: "RestrictionGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RestrictionPermissionChanges_RepSubject_RestrictionGroupId_Sequence",
                table: "RestrictionPermissionChanges",
                columns: new[] { "RepSubject", "RestrictionGroupId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_RestrictionPermissionChanges_RestrictionGroupId_Sequence",
                table: "RestrictionPermissionChanges",
                columns: new[] { "RestrictionGroupId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_RestrictionPermissionChanges_Sequence",
                table: "RestrictionPermissionChanges",
                column: "Sequence",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RestrictionPermissions_RestrictionGroupId",
                table: "RestrictionPermissions",
                column: "RestrictionGroupId");

            migrationBuilder.Sql("""
                CREATE TRIGGER [TR_RestrictionPermissionChanges_AppendOnly]
                ON [RestrictionPermissionChanges] INSTEAD OF UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51029, 'Restriction permission changes are append-only.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RestrictionPermissionChanges");

            migrationBuilder.DropTable(
                name: "RestrictionPermissions");
        }
    }
}
