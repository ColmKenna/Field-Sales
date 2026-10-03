# WI-021 — Territory assignment delivery

This completes the approved five-increment plan for T-3.1.1 / COV-US-001.
The final increment adds production assignment mutations, Head Office management
of business reporting lines and assignment-aware geography retirement protection.
The final validation results and publication status are recorded in
`plan_docs/.agent-notes/WI-021.md` before WI-021 is marked done.

## Behavior and contracts

Effective ownership is computed through the single pure resolver:
Location > Town > County > Region, or Unassigned. Territory assignment uniqueness
is enforced by SQL per typed target. A narrower assignment can carve out a parent
territory. Removing it exposes the next assignment; removing a parent retains
its carve-outs. Assignment add does not silently replace an existing holder.

| Endpoint | Behavior |
|---|---|
| `GET /coverage/reps` | Current eligible rep subjects and display names, filtered to a manager's current team; Head Office sees all |
| `POST /coverage/assignments` | Add an assignment for an eligible rep and active target; returns assignment identity and concurrency version |
| `POST /coverage/assignments/{id}/remove` | Remove the assignment using its saved version; returns the number of changed Locations |
| `GET /coverage/reps/{repSubject}/assignments` | Current assigned units, names and versions |
| `GET /coverage/reps/{repSubject}/locations` | Effective Locations with rep subject and winning assignment/source |
| `GET /coverage/locations/{id}/owner` | Current owner and source, or null for Unassigned |
| `GET /coverage/locations/{id}/history` | Immutable history for the authorized Location |
| `GET /coverage/reps/{repSubject}/history` | Immutable history involving that rep |
| `GET /coverage/reporting-lines` | Head Office staff choices and current named reporting lines |
| `PUT /coverage/reporting-lines/{repSubject}` | Head Office sets an eligible Sales Manager with optimistic concurrency |

The add contract contains `RepSubject`, typed `Target` and optional `Reason`.
Removal contains `Version` and optional `Reason`. Reporting-line writes contain
`ManagerSubject` and `Version` (null for a new line). Assignment IDs, snapshot labels, actor, team,
timestamp and authority are derived by server services. Invalid targets/versions
and ineligible staff return `400`; existing holders and stale/conflicting saves
return `409`; absent removals return `404`; denied authority returns `403`;
unavailable or untrusted required identity data returns `503`.

Current eligible rep names come from `/coverage/reps`; owner/source contracts
provide the rep identity and winning unit name for later territory UI consumers.
For example Colm plus the County source Wicklow is displayed as Colm (via Wicklow).
This item supplies those read contracts; the full rep territory page is WI-023.

## Authorization and transactions

Coverage requires API scope and the current Head Office or Sales Manager role.
Current roles are resolved through Identity rather than trusting token role
claims. Business reporting lines live in DirectoryDb; Head Office manages them.
The trusted staff snapshot must also retain the role being used for authority;
if it reports only Manager after the request initially saw Head Office, the
operation returns `403` instead of using the earlier Head Office authority.
Managers can add/remove only their own reps. An add also protects an existing
unit holder from a foreign manager, even when the requested new rep belongs to
that manager. Broader Customer/directory writes remain Head Office only.

Identity labels and eligibility are read before the SQL transaction. Preparation
includes suppressed parent assignments because removal can expose their reps.
The same serializable transaction then checks current team, target activity,
existing holder or removal version, resolves before/after owners, writes the
assignment and stages one history row per changed rep. Both saves commit together.
Late-save failures roll back assignment and history; a retry creates one entry.
If concurrent data introduces an identity outside the prepared label snapshot,
the write fails closed and rolls back rather than guessing a label.

New assignments require an available Field Salesperson and an active complete
target hierarchy. Region, County or Town archive preserves existing assignments
and owners. Removal remains possible under archived geography. The required
batched `territory-assignments` usage source prevents deletion of an assigned unit
even when it has no shops; absent/invalid usage data blocks retirement. Existing
hierarchy/Location usage sources remain required in that same transaction.

## Head Office reporting page

`/HeadOffice/Coverage/ReportingLines` is linked from Head Office. It lists current
rep/manager labels, allows choosing a rep and an available Sales Manager, then
saves through the BFF using the server-held bearer token. Native labeled selects,
Razor antiforgery, validation summary, current line version and redirect-on-success
follow the existing Head Office forms. A stale save retains submitted values and
offers Reload reporting line. Empty staff/manager/line states are explicit.
Managers cannot access the page or its management API. A reporting-line change
updates team authorization on the next request without changing owners or history.
Account/role administration remains in Identity.

## History and inherited ownership

The previous history increment supplies the additive migration, EF and SQL
append-only guards and immutable display-label snapshots. Assignment writes now
use that same writer. Location assignments have `DirectLocationAssignment` cause;
Region/County/Town mutations have `TerritoryAssignment` cause. Source-only or
unchanged-rep changes write no ownership entry. Optional reasons are validated.
UTC time displays to the minute; full stored timestamps and sequence order remain.

First/additional assigned Location creation writes initial Unassigned → owner
history atomically with Location/position changes. Unassigned creation writes none.
A changed Town records `GeographyChange` only when the effective rep changes.
Existing stale-write, duplicate, archive, type, position and contact behavior is
preserved. History reads retain stored rep, actor, Location and source names
through renames/removal/restart; they never replace them with current labels.

## Acceptance evidence

| Quoted T-3.1.1 criterion | Evidence |
|---|---|
| “Assigning County Wicklow (140 Locations, none assigned) to Colm makes all 140 show \"Colm (via Wicklow)\" (S1)” | Production County POST resolves all 140 to Colm/Wicklow and writes 140 initial entries; eligible rep reads supply Colm's display label |
| “Assigning Town Rathdrum to Aoife inside Colm's Wicklow makes Murphy's Pharmacy \"Aoife (via Rathdrum)\" while Doyle's Shop (Laragh) stays \"Colm (via Wicklow)\" (S2)” | Production Town POST changes the 23 Rathdrum owners to Aoife; Laragh's 117 retain Colm/Wicklow; existing pure/read scenarios cover named shops |
| “Removing Aoife's Rathdrum assignment returns those 23 to Colm via Wicklow (S4)” | Production removal returns 23 changed Locations, restores all 140 County owners and writes exactly 23 fallback entries |
| “Removing Wexford, assigned only to Brian, leaves 96 Locations Unassigned (S5)” | Production Wexford removal returns 96 changes and leaves all 96 owners null across restart |
| “Each Location whose owner changes gets an Assignment History entry (CV001-H)” | Exact 140/23/117/96 counts, all-four fallback and direct cause, inherited Location/Town changes, no entries for same-rep source changes, SQL immutability, stale/failed writes and late-save rollback/retry |

Characterisation before the final integration passed 108/108. New production
assignment/reporting scenarios passed 32/32 in the final 95-case focused suite.
The final full solution regression passed **1,796/1,796**: API 254, Identity
1,049 and Web 493, with zero failures or skips. Existing JavaScript checks passed
**34/34**. The final solution build passed with zero warnings/errors. Exact
commands and TRX paths are in the agent notes; existing assertions were preserved.

## Scope boundary

WI-022 supplies assignment impact preview/confirmation; WI-023 supplies the rep
territory screen. Specialists, restrictions, visits/handover, batches, tablet Sync,
attribution and geography reparenting remain their later work items. No new broad
territory mutation UI, identity administration, owner cache or main integration
is introduced. Completing WI-021 does not start WI-022.
