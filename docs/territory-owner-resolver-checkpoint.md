# WI-021 increment 2: effective-owner resolver checkpoint

The developer approved the schema checkpoint with **Continue increment 2** on
2026-10-03. This increment implements the pure ownership rule and its agreed
unit scenarios. Read integration, creation history and authorized mutations
remain separate increments.

## Rule and result

`EffectiveOwnerResolver` copies assignment identities, rep subjects and typed
targets into an index once. `Resolve(LocationOwnershipPath)` checks the following
targets in order, stopping at the first match:

| Priority | Assignment target | Display source comes from |
|---|---|---|
| 1 | Location | Current Location name |
| 2 | Town | Current Town name |
| 3 | County | Current County name |
| 4 | Region | Current Region name |

An assigned result holds an `EffectiveOwner` with the exact `RepSubject` and an
`EffectiveOwnerSource` containing the winning assignment ID, `TerritoryTarget`
(level and unit ID) and source name. No match returns null, meaning Unassigned;
it cannot accidentally carry a rep without a source or a source without a rep.
The later read service supplies the rep's current identity display name to render
`Colm (via Wicklow)` rather than treating the subject as a display label.

The caller supplies the Location's current authoritative hierarchy. The resolver
does not fetch geography, validate staff eligibility, query a database or write
history. A renamed unit's supplied current name appears in the result; future
history takes its own stable display-label snapshot at the time of the change.
History compares previous/new rep subjects for ownership changes, rather than
treating a source-only difference as an ownership change.

## Snapshot behavior

The index keys by both level and unit ID. Equal GUID values in distinct geography
tables do not conflict. Source assignment identities and rep subjects are copied
instead of retaining mutable input collections. Resolving the same path again
produces the same result even after an assignment is removed from the original
collection. A new proposed state gets a new resolver built from that state's
assignment snapshot.

Duplicate assignments to the same unit at the same level are rejected when the
index is built. An invalid snapshot never chooses whichever rep happened to
appear first. Assignment order otherwise has no effect. Wider overlaps remain
valid, consistent with the increment 1 schema. Existing archived assignments
remain applicable: the resolver has no archive filter that could silently change
ownership. Later handlers enforce active choices for new assignments.

Construction is linear in the supplied assignment count; each Location then
needs at most four index lookups. Later read/snapshot services can reuse one
index for the whole batch, keeping one copy of the most-specific rule.

## Agreed scenario coverage

The pure tests in `FieldSales.Api.Tests/EffectiveOwnerResolverTests.cs` verify:

- Wicklow's 140 Locations resolve to Colm via the County.
- Rathdrum's 23 resolve to Aoife via the Town while Laragh's 117 stay with Colm;
  Murphy's Pharmacy and Doyle's Shop are checked explicitly.
- Removing Rathdrum returns exactly 23 Locations to Colm and preserves the old
  snapshot; removing Brian's Wexford leaves all 96 Unassigned.
- Removing a parent County retains its Town carve-outs.
- Each precedence pair wins in either input order and falls back on removal.
- With all four levels, removal walks Location → Town → County → Region →
  Unassigned. This is checked with distinct reps and one rep holding all levels,
  and with equal GUIDs across levels.
- Empty snapshots and assignments to unrelated units resolve Unassigned.
- Two reps assigned the same unit at the same level are rejected for all four
  levels, matching the approved duplicate-assignment scenario family.

Ownership history assertions for the 140/23/96 cases remain for increment 4;
the unit tests here demonstrate only the pure rule's outcome.

## Verification and next review

Verification completed: 20/20 focused resolver/model checks; full regression
passes 1,713 .NET tests (API 233, Identity Admin 1,046, Web 434) and 34 JavaScript
tests. Zero failures/skips; solution build has zero warnings/errors. Results and
commits are recorded in `plan_docs/.agent-notes/WI-021.md`.
Review the precedence order, source representation, Unassigned result and the
agreed scenarios. **Continue increment 3** authorizes read integration and new
Location ownership only; stop again at that increment's review boundary.
