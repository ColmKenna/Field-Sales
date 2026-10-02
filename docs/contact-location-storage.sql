-- WI-018 model checkpoint: REVIEW ONLY; not installed or run by the application.
-- After Continue T-2.4.1, the EF migration creates Contacts, LocationContacts
-- and nullable Locations.MainContactId using ContactModelConfiguration.
-- It then installs each CREATE TRIGGER batch below with MigrationBuilder.Sql
-- (without GO). Do not run this against the current WI-017 schema.
--
-- The mapping writes these declarative constraints:
-- PRIMARY KEY LocationContacts(LocationId, ContactId)
-- FOREIGN KEY LocationContacts.LocationId -> Locations.Id, NO ACTION
-- FOREIGN KEY LocationContacts.ContactId -> Contacts.Id, NO ACTION
-- FOREIGN KEY Contacts.ContactTypeId -> ContactTypes.Id, NO ACTION
-- CHECK Contacts.Status IN (0,1) -- Active=0, Inactive=1
-- FOREIGN KEY Locations(Id, MainContactId)
--     -> LocationContacts(LocationId, ContactId), NO ACTION
--
-- Main is a single scalar on each unique Location row, not independent flags
-- on links. The FK requires membership in the SAME Location's roster.
-- The nullable slot permits Locations without Active Contacts. The guards
-- below fill the first Active slot and reject clearing one with Active links.

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
GO

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
GO

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
GO
