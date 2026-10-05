using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FieldSales.Api.Directory.Migrations
{
    /// <inheritdoc />
    public partial class AddMainContactReplacement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RetiredMainContactId",
                table: "Locations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Locations_Id_RetiredMainContactId",
                table: "Locations",
                columns: new[] { "Id", "RetiredMainContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_Locations_RetiredMainContactId",
                table: "Locations",
                column: "RetiredMainContactId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Locations_MainOrRetired",
                table: "Locations",
                sql: "[MainContactId] IS NULL OR [RetiredMainContactId] IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Locations_RetiredMainContactLink",
                table: "Locations",
                columns: new[] { "Id", "RetiredMainContactId" },
                principalTable: "LocationContacts",
                principalColumns: new[] { "LocationId", "ContactId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [TR_Locations_MainGuard]
                ON [Locations] AFTER INSERT, UPDATE AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM inserted AS changed
                        JOIN [Contacts] AS contact WITH (UPDLOCK,HOLDLOCK) ON contact.Id=changed.MainContactId
                        WHERE contact.Status<>0
                    ) THROW 51001, 'Choose an Active Contact linked to this location as Main.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted AS changed
                        JOIN [LocationContacts] AS link WITH (UPDLOCK,HOLDLOCK) ON link.LocationId=changed.Id
                        JOIN [Contacts] AS contact WITH (UPDLOCK,HOLDLOCK) ON contact.Id=link.ContactId
                        WHERE changed.MainContactId IS NULL AND changed.RetiredMainContactId IS NULL AND contact.Status=0
                    ) THROW 51002, 'A location with Active Contacts must retain a Main Contact.', 1;

                    -- A new gap records the previous Main, never an arbitrary linked person.
                    IF EXISTS (
                        SELECT 1 FROM inserted AS changed
                        LEFT JOIN deleted AS previous ON previous.Id=changed.Id
                        WHERE changed.RetiredMainContactId IS NOT NULL
                          AND (previous.RetiredMainContactId IS NULL OR previous.RetiredMainContactId<>changed.RetiredMainContactId)
                          AND (previous.MainContactId IS NULL OR previous.MainContactId<>changed.RetiredMainContactId)
                    ) THROW 51004, 'A replacement gap must retain the outgoing Main contact.', 1;

                    -- A gap persists across reactivation and cannot be silently erased.
                    IF EXISTS (
                        SELECT 1 FROM inserted AS changed JOIN deleted AS previous ON previous.Id=changed.Id
                        WHERE previous.RetiredMainContactId IS NOT NULL
                          AND changed.RetiredMainContactId IS NULL AND changed.MainContactId IS NULL
                    ) THROW 51004, 'Choose an Active Main contact before clearing the replacement gap.', 1;
                END;
                """.ReplaceLineEndings("\n"));

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [TR_LocationContacts_FirstMain]
                ON [LocationContacts] AFTER INSERT, UPDATE AS
                BEGIN
                    SET NOCOUNT ON;
                    UPDATE location WITH (UPDLOCK,HOLDLOCK)
                    SET MainContactId=firstActive.ContactId
                    FROM [Locations] AS location
                    JOIN (SELECT DISTINCT LocationId FROM inserted) AS touched ON touched.LocationId=location.Id
                    CROSS APPLY (
                        SELECT TOP (1) link.ContactId FROM [LocationContacts] AS link WITH (UPDLOCK,HOLDLOCK)
                        JOIN [Contacts] AS contact WITH (UPDLOCK,HOLDLOCK) ON contact.Id=link.ContactId
                        WHERE link.LocationId=location.Id AND contact.Status=0 ORDER BY link.ContactId
                    ) AS firstActive
                    WHERE location.MainContactId IS NULL AND location.RetiredMainContactId IS NULL;
                END;
                """.ReplaceLineEndings("\n"));

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [TR_Contacts_MainStatus]
                ON [Contacts] AFTER INSERT, UPDATE AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM inserted AS changed
                        JOIN [Locations] AS location WITH (UPDLOCK,HOLDLOCK) ON location.MainContactId=changed.Id
                        WHERE changed.Status<>0
                    ) THROW 51003, 'This contact is Main at a location. Choose a replacement before marking them inactive.', 1;

                    UPDATE location WITH (UPDLOCK,HOLDLOCK)
                    SET MainContactId=firstActive.ContactId
                    FROM [Locations] AS location
                    JOIN (
                        SELECT DISTINCT link.LocationId FROM [LocationContacts] AS link WITH (UPDLOCK,HOLDLOCK)
                        JOIN inserted AS changed ON changed.Id=link.ContactId WHERE changed.Status=0
                    ) AS touched ON touched.LocationId=location.Id
                    CROSS APPLY (
                        SELECT TOP (1) link.ContactId FROM [LocationContacts] AS link WITH (UPDLOCK,HOLDLOCK)
                        JOIN [Contacts] AS contact WITH (UPDLOCK,HOLDLOCK) ON contact.Id=link.ContactId
                        WHERE link.LocationId=location.Id AND contact.Status=0 ORDER BY link.ContactId
                    ) AS firstActive
                    WHERE location.MainContactId IS NULL AND location.RetiredMainContactId IS NULL;
                END;
                """.ReplaceLineEndings("\n"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [Locations] WHERE RetiredMainContactId IS NOT NULL) THROW 51004, 'Resolve replacement gaps before reverting this migration.', 1;");
            RestorePredecessorGuards(migrationBuilder);
            migrationBuilder.DropForeignKey(
                name: "FK_Locations_RetiredMainContactLink",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Locations_Id_RetiredMainContactId",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Locations_RetiredMainContactId",
                table: "Locations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Locations_MainOrRetired",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "RetiredMainContactId",
                table: "Locations");
        }

        private static void RestorePredecessorGuards(MigrationBuilder migrationBuilder)
        {
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
    }
}
