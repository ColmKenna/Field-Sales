# WI-021 — Owner read integration checkpoint

Increment 3 follows the developer's **Continue** after the reviewed resolver.
WI-021 remains active. Review this increment before authorizing history work.

## Current behavior

Coverage reads join the saved Location → Town → County → Region hierarchy and
resolve the current assignment snapshot with the approved most-specific rule.
They return the exact rep subject, winning assignment ID, typed target and current
source name. An existing Unassigned Location returns `200` with `owner: null`;
an absent Location returns `404`.

A first or additional Location saved inside an assigned County immediately
inherits that County's rep on the next read. A Town edit resolves against the
new hierarchy on the next read. Ownership requires no cached fields or refresh
job. Existing position, Eircode, type, contact and rowversion behavior continues
through the existing Customer/Location stores.

Each read uses the existing serializable transaction/retry pattern. Rep Location
lists load one joined path batch and one assignment batch, including assignments
belonging to other reps so that carve-outs stay correct. A single-Location read
loads its joined path and only the assignments that can apply to that path.
Existing assignments under archived geography continue to resolve.

## Read contract

| GET endpoint | Response |
|---|---|
| `/coverage/reps/{repSubject}/assignments` | Assigned units with current names, assignment IDs, typed targets and concurrency versions |
| `/coverage/reps/{repSubject}/locations` | Effective Locations for that rep, each with its owner and source |
| `/coverage/locations/{id}/owner` | The saved Location's identity/name and resolved owner/source, or null owner |

`AssignedTerritoryDetails` wraps the existing `TerritoryAssignmentDetails` and
the current unit name. `LocationCoverageDetails` contains `LocationId`, `Name`
and nullable `EffectiveOwnerDetails`. `CoverageSourceDetails` supplies
`AssignmentId`, `Target` and `Name`.

Rep lists sort by current Location name and stable Location ID. Assigned units
sort by level, name and unit ID. Subjects remain opaque and case-sensitive.
The trusted staff lookup and rep display labels arrive with the approved
increment 5 integration; these reads expose subjects rather than guessed labels.

## Access

The `ManageCoverage` policy requires an authenticated current Head Office User
or Sales Manager with the API scope. Existing bearer validation replaces token
business-role claims using the current identity role lookup on every request.
An unavailable lookup returns `503`.

Head Office may read all coverage. A manager may read a rep's assignments and
effective Locations only when the business reporting line names that manager.
A manager's single-Location read requires the effective owner to belong to that
team. Foreign-team and Unassigned Location reads return `403`; the later
Unassigned work item owns that browsing flow. The team check shares the read
transaction, and a reporting-line change affects the next request. Extra query
parameters cannot supply team authority. Coverage access does not grant general
directory edit privileges.

## Agreed scenario evidence

Existing Customer, geography, type, contact and Town coordinate scenarios passed
127/127 before code changes, and Location coordinate scenarios passed 16/16.
These characterize required first Location, duplicate confirmation, stale/invalid
writes, retained archived links, positions, contacts and type behavior.

The new SQL-backed API cases cover:

- Wicklow's 140 Locations resolve to Colm via Wicklow using a bounded query batch.
- Rathdrum's 23 resolve to Aoife while Laragh's 117 stay Colm; removing the Town
  restores all 140 to Colm. Removing the parent retains the 23-shop carve-out.
- Removing Brian's Wexford assignment leaves all 96 readable as Unassigned.
- First and additional Locations created later inherit County ownership and
  retain Town position defaults across restart.
- A saved Town edit immediately changes the resolved owner; a stale edit is
  rejected and cannot move it back.
- All four assignment levels and successive removals resolve through the same
  fallback chain. Assigned-unit names work at every level.
- Archived Region, County and Town references retain their owners. Renaming a
  County changes the current source label while preserving its stable identity.
- Current team and role changes affect reads; foreign teams, case-spoofed
  subjects, extra authority parameters and broader directory access are denied.
- Unassigned/absent Locations remain distinct. Missing authentication/scope and
  unavailable current-role lookup fail at the existing security boundary.

Assignment add/remove in these cases is database fixture setup. Production
handlers and one-entry-per-changed-Location history are still the approved
increments 4–5; this checkpoint does not claim those acceptance gates complete.

Validation completed on 2026-10-03:

| Check | Passed |
|---|---:|
| Characterisation before integration | 143 |
| Focused coverage API cases | 14 |
| Full .NET solution regression | 1,727 (API 233, Identity Admin 1,046, Web 448) |
| Existing JavaScript suites | 34 |

No failures or skipped cases. Solution build passed with zero warnings/errors.
Detailed commands and TRX evidence are in `plan_docs/.agent-notes/WI-021.md`.

## Review boundary

Review the read contracts, team restriction and new-Location inheritance. The
delivery skill requires Human Tight-Loop work to execute “one increment at a
time, stopping after each.” Continue with **Continue increment 4** to authorize
the append-only history increment only, followed by its own review stop.
