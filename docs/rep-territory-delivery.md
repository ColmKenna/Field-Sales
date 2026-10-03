# Rep territory page — WI-023

A manager can open a rep's territory from **Coverage**, see assignments at all
four levels, expand a County into its Towns, and distinguish all shops in an
area from the shops the rep effectively owns. Add/removal actions retain the
selected rep through the existing impact preview and explicit confirmation.

## Acceptance evidence

| Source criterion | Evidence |
|---|---|
| "Colm's page lists his Region, County, Town and Location assignments." | Mixed-level real-SQL fixture asserts all four ordered levels and stable target IDs. Region/County/Town overlap does not inflate Primary count. The page labels direct Location assignments and disambiguates matching Town names by County/Region. |
| "Wicklow (County) expands to its Towns, each with its Location count." | County fixture returns 140 Locations, Rathdrum 23 and Laragh 117. Razor renders native details/summary and a table with named row/column headers. Browser keyboard Enter on the County summary exposes the Town table. |
| "Towns carved out to another rep are marked with that rep's name." | Rathdrum's Town assignment names Aoife, with 23 lost Locations and Colm's Primary count 117. A direct assignment of Murphy's Pharmacy back to Colm yields 118 Primary / 22 lost. A foreign direct Location override is shown as a shop count, without declaring the whole Town assigned to that rep. Zero-shop Town assignments retain the named carve-out. |
| "Add and remove actions use T-3.1.1's handlers and T-3.1.2's preview." | Both roles exercise the existing Preview/Save handlers and API writes. Preview and Cancel write nothing; stale preview refreshes without saving; a new explicit confirmation saves and returns to Territory with new counts. Remove includes the saved row version, CSRF and explicit Unassigned confirmation. |
| "Only reps the manager manages are editable; Head Office Users can edit any." | Current role/team checks guard API and BFF reads/writes. Manager rep chooser excludes Brian and his direct page is forbidden; Head Office can open it. Reporting-line and role changes deny the next request. A removal submitted for the wrong selected rep is rejected before preview/save. |

## Read and action contracts

- `GET /coverage/reps/{repSubject}/territory` returns `RepTerritoryPage`: trusted
  rep/manager labels, unique effective Primary count, assignments with typed
  targets/versions, full area totals, named effective carve-out counts, contextual
  geography names, archive flags and County/Town children.
- All geography, reporting authority, assignments and Location paths are read
  inside one serializable SQL snapshot. The existing `EffectiveOwnerResolver`
  resolves every Location once; totals aggregate its answers. No schema or
  ownership-rule change was required.
- Staff directory requests occur before the SQL transaction. Missing required
  labels or unavailable identity data returns 503 instead of subject IDs or
  invented names. Staff names refresh on subsequent reads; history snapshots
  retain their existing independent semantics.
- BFF `/Coverage/Territory` is shared through `ManageCoverage`, with entry links
  in the existing manager and Head Office areas. Foreign owner labels are scoped
  to carve-out context on an authorized rep's page; the existing manager read
  boundary is not widened.
- Filtering is a GET with no writes. Matching Towns retain their County context;
  the rep's overall Primary total remains unchanged.
- `ReturnToTerritory` is a boolean selecting a known local destination, not an
  arbitrary return URL. Existing `/Coverage/Assignments` behavior without that
  flag remains supported. Both preview and save load current rep assignments
  before allowing a removal, so its assignment must belong to the selected rep.
- The client rejects malformed territory responses, including negative/inflated
  counts, duplicate IDs, invalid targets, missing children and a response for a
  different rep. Writes are not automatically retried.

## Design and scope

The existing Razor Pages shell and CSS conventions implement M-06's rep heading,
Reports to, summary, nearby Add assignment, filter, hierarchical assignment list
and direct Location wording. Assigned archived geography remains visible because
archive does not revoke coverage; Add choices retain their active-only behavior.

Transfer, Take over, rep History and Specialist counts are later slices. This
page does not display dead controls or fabricate those data. WI-024 owns transfer
and roll-ups, WI-025 the pull/transfer offer, E14 visit handover, E16 rep history
and E20 specialists. The source S3 dead-end copy was superseded by M6.5 and is not
introduced as new behavior.

The developer's **Approved and Apply**, no-stops, push/PR/merge authorization
explicitly waives WI-023's review pause. The actual sample rendering still proves
the County, named Town carve-out and direct Location checkpoint.

## Verification

- Warning-free full solution build (`--no-restore -m:1 -warnaserror`).
- Existing coverage characterization: 81/81 (67 mutation/preview, 14 reads).
- Final focused coverage run: 106/106 (81 existing + 25 new tests: 15 real-SQL/BFF
  scenarios and 10 API-response boundary cases).
- JavaScript: 34/34, using cached Node and identity test copies verified byte-for-
  byte against repository sources; no test changes or skips.
- Final full .NET regression after the prerequisite correction: **1860/1860**
  (API 256, Identity 1049, Web 555), no failures or skips. All three TRX summaries
  are Completed; every individual result is Passed with zero abort/error/timeout/
  unexecuted outcomes. Evidence: `.artifacts/wi023/regression-corrected/`.
  No application/test changes after the corrected warning-free build.
- Customer/coordinate regression: **70/70**, including both deterministic
  preflight cases and the unchanged late-coordinate conflict assertions.

Focused evidence: `.artifacts/wi023/focused-final/`. Rendered real Razor responses
and desktop/narrow screenshots: `.artifacts/wi023/rendered/`. These test-only
artifacts are ignored. The browser preview serves captured markup; actual API,
authentication, CSRF, filtering and mutation behavior are covered by real-SQL/BFF
tests, rather than by submitting forms to the static preview.

Browser checks: native County disclosure responds to keyboard Enter; Town counts,
Aoife's name and direct shop assignment remain visible. At a 390 px viewport the
page had no horizontal document overflow. The temporary viewport was reset.
An initial restricted test-host launch could not bind its local socket and is
excluded from passing evidence; its authorized rerun completed successfully.

All five acceptance criteria pass. The scoped layout matches M-06; the actual
changes are reflected in the committed read-model and page summaries. WI-024 is
next by console order and has not been started.

## CI prerequisite correction

PR #4's final-head CI exposed a pre-existing customer creation/deletion race:
when a Town disappeared between eligibility validation and the position
preflight read, a missing Town was reported as 409 rather than the existing
invalid-Town 400. The unchanged concurrent-retirement test caught that response.

Two deterministic real-SQL/API cases reproduced the failure for first/additional
Locations before the source fix. Position preflight now raises the existing Town
validation exception with `FirstLocation.TownId` or `TownId`. No records or
history are written for those failures. Later coordinate-version conflicts keep
409 and their existing rollback behavior; existing assertions remain unchanged.

Corrected full verification passed 1860/1860; focused customer/coordinate tests
passed 70/70. Final-head CI succeeded before merge (run 37159772774, job 111310620311). The failed CI run
37158239416 (job 111306041082) is excluded from successful evidence.

## Integration

[PR #4](https://github.com/ColmKenna/Field-Sales/pull/4) merged as
`8fd5654eb46ae170f26ad584883de629968b5be3` after
[final-head CI](https://github.com/ColmKenna/Field-Sales/actions/runs/37159772774)
passed on `b87a7f4f50b0a1cdd0928b95ac0b644fbfdc4c36`. CI confirms all 1860 tests,
including the original retirement race and both deterministic regression cases.
Local main was synchronized to the merge; the feature branch is preserved.
This integration handover changes documentation only.
