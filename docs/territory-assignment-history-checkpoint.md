# WI-021 — Assignment history checkpoint

Increment 4 implements the approved history plan and sequencing amendment.
WI-021 remains active. Assignment mutation handlers, reporting-line management
and the Head Office reporting page remain increment 5.

## History behavior

The additive `20261003162018_AddAssignmentHistory` migration stores one entry
per Location whose effective rep changes. Entries snapshot previous/new rep
subjects and display labels, both winning sources (assignment ID, target level,
unit ID and name), actor subject/label, Location name, UTC time, cause and optional
reason. A nullable owner represents Unassigned. Source-only changes and unchanged
owners produce no entry. No earlier ownership or actor is guessed during upgrade.

New first and additional Locations inherit their owner through the existing
resolver. Assigned creation writes Unassigned → owner history in the same
serializable transaction as the Customer/Location and position. Unassigned
creation writes none. A saved Town edit snapshots the before/after owners and
records `GeographyChange` only when the effective rep changes. Existing stale-write,
duplicate confirmation, archive, type, contact and position behavior is retained.
If Town deletion wins between form validation and the preflight ownership read,
the save returns the existing Town validation error without staging history.

The history writer supports assignment before/after snapshots for increment 5.
SQL tests exercise the agreed 140/23/117 Wicklow carve-out and 96-shop Wexford
removal; their assignment changes are fixture setup, not production endpoints.
`DirectLocationAssignment` and `TerritoryAssignment` are supported causes.

History labels never refresh from current staff or geography data. Display strings
use invariant minute-level UTC timestamps, for example:

> 17 Sep 2026 14:02 UTC — Unassigned → Colm — via Wicklow — territory assignment — by M. Byrne — New rep

Full stored timestamps and an indexed identity sequence preserve chronology;
history reads return newest sequence first. A unique operation/Location pair
prevents duplicate entries within an operation. Failed saves roll back both the
business changes and history. An application retry can then write one entry.

## Trusted identities

The brought-forward staff directory reads existing identity accounts through the
protected `/staff/directory` roster and `/staff/directory/lookup` bounded lookup.
Responses contain only subject, display name, current business roles and account
availability. FullName is preferred, with UserName as the existing fallback.
Roster candidates have business roles; exact-subject lookup can retain a historical
account's label after role removal. Credentials and account administration remain
in Identity.

Both endpoints require the API bearer audience/scope and a currently available
Head Office User or Sales Manager in the identity database. Token role claims
cannot replace current membership. Subject matching on the wire is exact, even
where the identity database collation is case-insensitive. The HTTP client rejects
invalid, duplicate or unrequested identities. History requires a trusted label for
the actor and every assigned before/after rep. Missing, unavailable or invalid
required identity data fails the save with `503`; request-supplied label fields
cannot alter the snapshot.

Identity network reads occur before the DirectoryDb write transaction. Ownership
is resolved again inside that transaction. If a concurrent assignment exposes a
rep absent from the prepared identity snapshot, the write fails and rolls back.
Existing historical rep labels may be retained for unavailable accounts; current
eligibility for new assignment writes belongs to the increment 5 handlers.

## Read access and immutability

| GET endpoint | Response |
|---|---|
| `/coverage/locations/{id}/history` | Immutable entries for the authorized Location; absent Location returns 404 |
| `/coverage/reps/{repSubject}/history` | Entries where that rep was the previous or new owner |

These reuse the current-role coverage policy and transactional reporting-line
checks. Head Office can read all. A Sales Manager's rep history requires a current
team reporting line; Location history requires a current effective owner in that
team. Foreign-team and Unassigned Location reads return `403`. Reads use stored
labels and require no staff-directory call.

There are no history update/delete endpoints. EF rejects tracked modifications
and deletions. SQL's append-only trigger rejects UPDATE/DELETE, including bulk EF
and direct SQL attempts. Check constraints require complete valid owner snapshots,
a changed owner and UTC time. The Location FK is restrictive; old assignment,
geography and identity IDs are snapshots without live FKs so removals and renames
cannot rewrite history. Privileged test reset uses TRUNCATE between scenarios;
the append-only DML guard stays enabled throughout each test.

## Validation

Validation completed on 2026-10-03:

| Check | Passed |
|---|---:|
| Characterisation before integration | 157 Web/API + 1 Identity |
| Focused API history/client/model/position checks | 34 |
| Expanded Customer/history/coverage API checks | 66 |
| Focused Identity staff-directory/current-role checks | 4 |
| Full .NET solution regression | 1,764 (API 254, Identity Admin 1,049, Web 461) |
| Existing JavaScript suites | 34 |

The solution builds with zero warnings/errors. Final tests have zero failures or
skips. The agreed checks cover immutable labels/restart, schema upgrade,
carve-outs/removals, no-op behavior, first/additional creation, Town changes,
stale writes, late-save rollback, trusted identity failures and team/current-role
enforcement. Existing tests and assertions are retained. Regression caught a new
fixture's shared-database contamination and the ownership preflight Town-deletion
race; both were corrected before this final full run. Detailed commands, TRX
evidence and the earlier failures are in `plan_docs/.agent-notes/WI-021.md`.

## Review boundary

Review this increment's schema, trusted label source, history contracts and
atomic Location integration. The [delivery skill](/Users/colmkenna/.codex/skills/console-delivery-next-item/SKILL.md)
requires Human Tight-Loop work to execute “one increment at a time, stopping
after each.” **Continue increment 5** authorizes the remaining assignment,
reporting and final acceptance integration, followed by its own review stop.
