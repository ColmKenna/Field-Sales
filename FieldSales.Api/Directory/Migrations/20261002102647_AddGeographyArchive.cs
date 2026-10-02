using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FieldSales.Api.Directory.Migrations
{
    /// <inheritdoc />
    public partial class AddGeographyArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Towns",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Regions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Counties",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Towns");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Regions");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Counties");
        }
    }
}
