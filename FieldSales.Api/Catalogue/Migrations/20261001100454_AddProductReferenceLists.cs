using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FieldSales.Api.Catalogue.Migrations
{
    /// <inheritdoc />
    public partial class AddProductReferenceLists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProductProfileId",
                table: "Products",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierId",
                table: "Products",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AttributeNames",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Latin1_General_100_CI_AS"),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttributeNames", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Latin1_General_100_CI_AS"),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Latin1_General_100_CI_AS"),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductAttributeValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttributeNameId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductAttributeValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductAttributeValues_AttributeNames_AttributeNameId",
                        column: x => x.AttributeNameId,
                        principalTable: "AttributeNames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductAttributeValues_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_ProductProfileId",
                table: "Products",
                column: "ProductProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_SupplierId",
                table: "Products",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "UX_AttributeNames_Name",
                table: "AttributeNames",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductAttributeValues_AttributeNameId",
                table: "ProductAttributeValues",
                column: "AttributeNameId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductAttributeValues_ProductId_Position",
                table: "ProductAttributeValues",
                columns: new[] { "ProductId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ProductProfiles_Name",
                table: "ProductProfiles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Suppliers_Name",
                table: "Suppliers",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_ProductProfiles_ProductProfileId",
                table: "Products",
                column: "ProductProfileId",
                principalTable: "ProductProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Suppliers_SupplierId",
                table: "Products",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Preserve every legacy name/value row and its array order before replacing JSON storage.
            // Invalid legacy input aborts the migration transaction; never truncate or discard values.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM Products WHERE ISJSON(Attributes) <> 1
                    OR LEFT(LTRIM(Attributes), 1) <> '[')
                    THROW 51000, 'Product attributes must be a valid JSON array before migration.', 1;
                IF EXISTS (SELECT 1 FROM Products p CROSS APPLY OPENJSON(p.Attributes) j WHERE j.[type] <> 5)
                    THROW 51000, 'Each legacy product attribute must be a name/value object.', 1;

                CREATE TABLE #LegacyProductAttributes
                    (ProductId uniqueidentifier NOT NULL, Position int NOT NULL,
                     Name nvarchar(max) COLLATE Latin1_General_100_CI_AS NULL, Value nvarchar(max) NULL);
                INSERT INTO #LegacyProductAttributes (ProductId, Position, Name, Value)
                SELECT p.Id, CONVERT(int, j.[key]), LTRIM(RTRIM(a.Name)), a.Value
                FROM Products p CROSS APPLY OPENJSON(p.Attributes) j
                CROSS APPLY OPENJSON(j.[value]) WITH (Name nvarchar(max) '$.Name', Value nvarchar(max) '$.Value') a;
                IF EXISTS (SELECT 1 FROM #LegacyProductAttributes
                    WHERE Name IS NULL OR Name = '' OR DATALENGTH(Name) > 400 OR Value IS NULL)
                    THROW 51000, 'Legacy attribute names and values must satisfy the reference-list contract.', 1;

                INSERT INTO AttributeNames (Id, Name, IsArchived)
                SELECT NEWID(), names.Name, 0 FROM
                    (SELECT DISTINCT CONVERT(nvarchar(200), Name) AS Name FROM #LegacyProductAttributes) names;
                INSERT INTO ProductAttributeValues (Id, ProductId, AttributeNameId, Value, Position)
                SELECT NEWID(), legacy.ProductId, names.Id, legacy.Value, legacy.Position
                FROM #LegacyProductAttributes legacy JOIN AttributeNames names ON names.Name = legacy.Name;
                IF (SELECT COUNT_BIG(*) FROM ProductAttributeValues) <> (SELECT COUNT_BIG(*) FROM #LegacyProductAttributes)
                    THROW 51000, 'Legacy attribute row counts did not match after migration.', 1;
                DROP TABLE #LegacyProductAttributes;
                """);
            migrationBuilder.DropColumn(name: "Attributes", table: "Products");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "Attributes", table: "Products",
                type: "nvarchar(max)", nullable: false, defaultValue: "[]");
            migrationBuilder.Sql("""
                UPDATE p SET Attributes = (
                    SELECT names.Name, valuesRow.Value
                    FROM ProductAttributeValues valuesRow JOIN AttributeNames names ON names.Id = valuesRow.AttributeNameId
                    WHERE valuesRow.ProductId = p.Id ORDER BY valuesRow.Position FOR JSON PATH)
                FROM Products p;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_Products_ProductProfiles_ProductProfileId",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Suppliers_SupplierId",
                table: "Products");

            migrationBuilder.DropTable(
                name: "ProductAttributeValues");

            migrationBuilder.DropTable(
                name: "ProductProfiles");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropTable(
                name: "AttributeNames");

            migrationBuilder.DropIndex(
                name: "IX_Products_ProductProfileId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_SupplierId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ProductProfileId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "Products");

        }
    }
}
