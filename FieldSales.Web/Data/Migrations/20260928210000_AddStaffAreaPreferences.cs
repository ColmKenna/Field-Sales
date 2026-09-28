using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FieldSales.Web.Data.Migrations;

public partial class AddStaffAreaPreferences : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable(
            name: "StaffAreaPreferences",
            columns: table => new
            {
                SubjectId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                Area = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_StaffAreaPreferences", x => x.SubjectId));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "StaffAreaPreferences");
}
