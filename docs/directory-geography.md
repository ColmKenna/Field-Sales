# Maintaining geography

Sign in as a Head Office User, open **Head office → Manage geography**.

## Manual entry

1. Add a Region, for example Leinster.
2. Open that Region and add a County, for example Wicklow.
3. Open that County and add its Towns, for example Rathdrum.

Use the path above the list to return to a County, Region or the Regions
list. Each entry has a **Rename** control. Renaming keeps its identity and
parent relationship. If another user has changed the entry, reload the page
before retrying your rename.

Names are required, trimmed and limited to 200 characters. Names differing
only by case count as duplicates under the same parent. Towns in different
Counties may share a name and are distinguished by County in town choices.

## CSV import

Open **Import towns from CSV**, choose the file and select **Import geography**.
Save the file as UTF-8 CSV with these exact column headings:

```csv
Region,County,Town
Leinster,Wicklow,Rathdrum
Leinster,Wicklow,Arklow
Leinster,Wicklow,Laragh
```

Every data row needs a Region, County and Town. Enclose names containing
commas or quotation marks in standard CSV quotes. The limit is 1 MiB and
10,000 Town records per file. A UTF-8 BOM is supported.

Import adds missing entries and reuses existing matching paths. Repeating
the file creates no duplicates and keeps existing names and identities.
It does not rename, move or remove places. A failed validation saves nothing
and identifies the problem row. Fix the file and import it again.

Manual entry is available whether or not a CSV file has been supplied.

## Deployment

The API now needs a `DirectoryDb` SQL Server connection alongside
`CatalogueDb`. Aspire provisions and supplies both references. For standalone
or production hosting, configure `ConnectionStrings:DirectoryDb` through
the existing configuration/secret mechanism and apply the Directory context
migration using your deployment process. Development migrates automatically;
production refuses startup with unapplied migrations.

The migration is `20261002091702_InitialDirectory`, scoped to
`DirectoryDbContext`. For EF deployment commands specify `--context
DirectoryDbContext --project FieldSales.Api`; provide the intended database
connection explicitly using `--connection` when applying migrations rather
than relying on the design-time local database.

Geography provides the town choices for the Location creation work item,
WI-016. This item does not introduce Location creation, coordinates,
archiving or geography moves. The implementation and acceptance evidence
are recorded in `directory-geography-checkpoint.md`.
