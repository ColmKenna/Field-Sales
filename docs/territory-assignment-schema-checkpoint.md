# WI-021 increment 1: territory assignment schema checkpoint

The approved first increment adds storage and contracts for territory assignments
and rep reporting lines. Effective-owner resolution, history, staff eligibility
checks, service operations and UI follow in separately reviewed increments.
No coverage endpoints or website actions are exposed by this change.

## Stored rules

`TerritoryAssignments` has a stable GUID key, opaque `RepSubject`, four nullable
target foreign keys and a SQL Server rowversion. Exactly one of `RegionId`,
`CountyId`, `TownId` and `LocationId` must be present and must not be an empty GUID.
Each targets its existing DirectoryDb table with restrictive deletion.

Four unique filtered indexes enforce one assignment per geography unit or
Location, including when two different reps request the same unit. Their filters
exclude nulls, allowing multiple assignments at other levels and ordinary
overlapping territories. The same GUID value may identify units in different
tables without creating a false conflict across levels. `TerritoryTarget` derives
level and unit from the single stored reference; no duplicate level column can
drift out of sync.

`RepReportingLines` uses the rep's identity subject as its primary key and stores
one required manager subject plus a rowversion. The manager index supports team
queries. An assignment's rep reference is a restrictive foreign key to this
business reporting row: a rep must have a reporting line before assignments,
and an assigned rep's reporting row cannot be silently removed. Updating their
manager preserves the rep key and existing assignments. This does not change
Location ownership or ownership history.

Subjects retain their exact case and use binary SQL collation. Their 450-character
limit follows the existing identity subject-key limit. Domain factories reject
missing, oversized, padded or control-character subjects without rewriting them.
The database rejects blank subjects/managers. No cross-database identity foreign
keys, accounts, credentials or duplicated role values are introduced.

Identity existence, current roles/account availability, Head Office permission
to change reporting lines, and manager team authorization require the trusted
identity lookup and handlers agreed for increment 5. A stored subject string is
not proof of identity or authority. Nothing in this increment bypasses the
existing staff authentication boundary.

## Migration and compatibility

Migration `20261003123731_AddTerritoryAssignments` creates the two new tables,
constraints and indexes. It
does not rewrite geography, Customer, Location, contact or position records.
The reviewed EF snapshot must match the new model. Existing owner state is not
materialised in this increment. Existing reps' reporting lines are supplied by
the later Head Office management flow; no guessed team or development-account
seed is introduced.

The migration's generated Down operation removes only the new tables, in
dependency order; it is not executed against application data during delivery.
The feature does not expose a history table until increment 4.

## Verification boundary

The increment's focused SQL Server scenarios exercise:

- Upgrade from the WI-020 DirectoryDb schema and restart persistence.
- Same-level duplicate rejection for all four target types.
- Overlapping assignments at all levels, even with equal GUID values across tables.
- Missing/multiple/empty/nonexistent targets and missing or wrong-case rep references.
- One manager per rep and rowversion conflict detection.
- Restrictive target/reporting-row references, including otherwise-unused geography.
- Blank manager rejection through direct SQL.

These instantiate the approved invalid-storage, reporting-line and schema
upgrade scenario families. Resolver, ownership history and authorization tests
remain for their approved increments. Test counts and artifacts are recorded in
`plan_docs/.agent-notes/WI-021.md` once verification finishes.

Verification completed: 23/23 focused SQL/model checks; the full regression suite
passes 1,694 .NET tests (API 214, Identity Admin 1,046, Web 434) and 34 JavaScript
tests. Zero failures/skips; solution build has zero warnings/errors. The notes
record the TRX files and JavaScript dependency setup for this Mac checkout.

## Human review

Review the typed targets, four filtered unique indexes, reporting-line prerequisite,
case-sensitive subjects and additive migration. Approval of this checkpoint
authorizes increment 2 only: the pure most-specific resolver and its agreed tests.
