using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FieldSales.Api.Directory.Migrations
{
    /// <inheritdoc />
    public partial class AddContactsAndMainContact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MainContactId",
                table: "Locations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContactTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contacts", x => x.Id);
                    table.CheckConstraint("CK_Contacts_Status", "[Status] IN (0,1)");
                    table.ForeignKey(
                        name: "FK_Contacts_ContactTypes_ContactTypeId",
                        column: x => x.ContactTypeId,
                        principalTable: "ContactTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LocationContacts",
                columns: table => new
                {
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationContacts", x => new { x.LocationId, x.ContactId });
                    table.ForeignKey(
                        name: "FK_LocationContacts_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LocationContacts_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Locations_Id_MainContactId",
                table: "Locations",
                columns: new[] { "Id", "MainContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_Locations_MainContactId",
                table: "Locations",
                column: "MainContactId");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_ContactTypeId",
                table: "Contacts",
                column: "ContactTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_Name",
                table: "Contacts",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_LocationContacts_ContactId",
                table: "LocationContacts",
                column: "ContactId");

            migrationBuilder.AddForeignKey(
                name: "FK_Locations_MainContactLink",
                table: "Locations",
                columns: new[] { "Id", "MainContactId" },
                principalTable: "LocationContacts",
                principalColumns: new[] { "LocationId", "ContactId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [TR_Locations_MainGuard]
                ON [Locations]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM inserted AS changed
                        JOIN [Contacts] AS contact WITH (UPDLOCK, HOLDLOCK) ON contact.Id = changed.MainContactId
                        WHERE contact.Status <> 0
                    )
                        THROW 51001, 'Choose an Active Contact linked to this location as Main.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted AS changed
                        JOIN [LocationContacts] AS link WITH (UPDLOCK, HOLDLOCK) ON link.LocationId = changed.Id
                        JOIN [Contacts] AS contact WITH (UPDLOCK, HOLDLOCK) ON contact.Id = link.ContactId
                        WHERE changed.MainContactId IS NULL AND contact.Status = 0
                    )
                        THROW 51002, 'A location with Active Contacts must retain a Main Contact.', 1;
                END;
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [TR_LocationContacts_FirstMain]
                ON [LocationContacts]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    -- The update locks each Location; simultaneous first links serialize here.
                    -- For a multi-row batch, choose one deterministically. Ordinary commands
                    -- link one person per shop and the sole first Active person is selected.
                    UPDATE location WITH (UPDLOCK, HOLDLOCK)
                    SET MainContactId = firstActive.ContactId
                    FROM [Locations] AS location
                    JOIN (SELECT DISTINCT LocationId FROM inserted) AS touched ON touched.LocationId = location.Id
                    CROSS APPLY (
                        SELECT TOP (1) link.ContactId
                        FROM [LocationContacts] AS link WITH (UPDLOCK, HOLDLOCK)
                        JOIN [Contacts] AS contact WITH (UPDLOCK, HOLDLOCK) ON contact.Id = link.ContactId
                        WHERE link.LocationId = location.Id AND contact.Status = 0
                        ORDER BY link.ContactId
                    ) AS firstActive
                    WHERE location.MainContactId IS NULL;
                END;
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [TR_Contacts_MainStatus]
                ON [Contacts]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    -- A global status edit cannot silently retire a Main at ANY linked shop.
                    IF EXISTS (
                        SELECT 1 FROM inserted AS changed
                        JOIN [Locations] AS location WITH (UPDLOCK, HOLDLOCK) ON location.MainContactId = changed.Id
                        WHERE changed.Status <> 0
                    )
                        THROW 51003, 'This contact is Main at a location. Choose a replacement before marking them inactive.', 1;

                    -- An inactive-only legacy roster has no Main. If its first Active person
                    -- becomes available through activation, fill the same empty slot rule.
                    -- WI-020 must extend these guards for explicit Replacement Needed gaps
                    -- and its rule that reactivation does not restore the outgoing Main.
                    UPDATE location WITH (UPDLOCK, HOLDLOCK)
                    SET MainContactId = firstActive.ContactId
                    FROM [Locations] AS location
                    JOIN (
                        SELECT DISTINCT link.LocationId
                        FROM [LocationContacts] AS link WITH (UPDLOCK, HOLDLOCK)
                        JOIN inserted AS changed ON changed.Id = link.ContactId
                        WHERE changed.Status = 0
                    ) AS touched ON touched.LocationId = location.Id
                    CROSS APPLY (
                        SELECT TOP (1) link.ContactId
                        FROM [LocationContacts] AS link WITH (UPDLOCK, HOLDLOCK)
                        JOIN [Contacts] AS contact WITH (UPDLOCK, HOLDLOCK) ON contact.Id = link.ContactId
                        WHERE link.LocationId = location.Id AND contact.Status = 0
                        ORDER BY link.ContactId
                    ) AS firstActive
                    WHERE location.MainContactId IS NULL;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_LocationContacts_FirstMain]; DROP TRIGGER IF EXISTS [TR_Contacts_MainStatus]; DROP TRIGGER IF EXISTS [TR_Locations_MainGuard];");

            migrationBuilder.DropForeignKey(
                name: "FK_Locations_MainContactLink",
                table: "Locations");

            migrationBuilder.DropTable(
                name: "LocationContacts");

            migrationBuilder.DropTable(
                name: "Contacts");

            migrationBuilder.DropIndex(
                name: "IX_Locations_Id_MainContactId",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Locations_MainContactId",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "MainContactId",
                table: "Locations");
        }
    }
}
