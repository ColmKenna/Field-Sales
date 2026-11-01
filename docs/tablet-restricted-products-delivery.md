# WI-043 — Restricted products on tablets

T-4.4.1 / REP-US-019, COV-US-009, PRD-US-004.
Branch: `feature/wi-043-keep-restricted-products-off-tablets`, from main `0806652`.

Snapshots now use each rep's current Restriction Permissions. The existing shared visibility rule
decides the included products; their prices, attributes, category paths and parent links follow the
same included set. The permission read and catalogue queries share one serializable transaction.
No new permission rule, upload rejection, endpoint, database schema or snapshot version was introduced.

A previously captured line remains readable with its captured name, quantity and price. When the
product is missing from the active catalogue, order views show **No longer available to you**. It
cannot be found or added again. Sync review now lets the current rep mark an In Progress order ready;
the next manual Sync sends it as captured. Review alone does not upload anything.

## Approved decisions and boundaries

- The developer approved the plan, explicitly requested the scenario draft, approved A1-A8/T1-T7,
  and approved each implementation increment separately before final verification/delivery.
- **Policy A was explicitly selected:** restrict new payloads and active reads; keep existing recovery
  snapshots. A previously authorized product may remain in an older local recovery generation.
  This item does not promise physical erasure of old snapshots or SQLite pages/WAL.
- **MI-45 remains unchanged:** an archived Restriction Group hides its products from every rep.
  Permissions are preserved; unarchive restores visibility only to prior permission holders.
- Captured Orders are an explicit exception to catalogue hiding. Current permissions do not rewrite
  or reject work that was valid when captured; upload logic remains unchanged.
- The minimal Mark ready action in existing Sync review was approved to complete S4's journey.
  General order editing/reopening remains WI-046.
- Order Pad, Stock Check/suggested lists, replacement and competitor-note pickers are later work.
  Their future UI must use the filtered snapshot. No test of an unbuilt surface is reported as passed.
- Current website catalogue endpoints remain Head Office-only. No website permissions or identity
  administration were changed. MI-62 (disabled accounts) remains outside this item.

## Changes

- `TabletSnapshotBuilder`: read `RestrictionPermissionReader.CurrentGroupIdsAsync` within the catalogue
  transaction and reuse `RestrictedProductVisibility.VisibleToRep` for products and related facts.
- Tablet Core: active-generation product identities and index membership support missing-product
  notices without reading older recovery copies or modifying captured JSON.
- `UnsentView`, NewOrder and Sync review: mark missing lines while retaining captured values; normal
  lines remain unmarked. Missing products have no add controls or search results.
- `TabletApp`/order store: the new review action checks the observed rep/session and current order
  state. Expired, signed-out, other-rep and stale same-rep sessions cannot change the Order. Rejected
  work continues through its existing separate review path.
- Responsive CSS: short narrow viewports exposed a last-line overlap under the fixed order footer.
  Extra narrow-screen clearance fixes it. No capture, pricing or upload behaviour changed.

## Acceptance verification

| Source criterion | Result and evidence |
|---|---|
| S1: "Without the 'Pharmacy-only medicines' permission, 'Controlled Pain Relief 30s' does not appear in Order entry, Stock Check or a Competitor Note picker, and no 'unavailable' entry is shown." | PASS within the approved current-surface boundary: serialized payload and active page/index/direct reads contain no denied product; live Order entry search has no match/add action. Stock Check and competitor-note pickers are explicit later-item hand-offs, not claimed implemented. |
| S2: "The product is not downloaded to the tablet at Sync." | PASS for new downloads and active reads under policy A. Actual authenticated payload tests exclude denied ids/names/codes, attributes, current/future prices, private-only categories and hidden parent references. A retained older authorized generation is explicitly allowed. |
| S3: "With the permission it appears and behaves like any other product." | PASS: no/A/B/both permission cases include the correct products and facts; a new grant appears on the next Sync using the same sign-in. Another rep's grants confer nothing. |
| S4: "An In Progress line added before removal shows 'No longer available to you' after Sync, can't be re-added, and the Order sends under valid-when-captured." | PASS: real tablet store/UI/session tests preserve values, mark only missing lines, refuse re-add/increase, and guard review actions. Real API round trips prove both grant removal and group archival: In Progress survives the first Sync, review/ready enables the next manual Sync, and the API stores the same order body as Pending with the tablet marking Sent and no Needs Attention. |
| MI-45: archived groups remain hidden while grants survive | PASS: archive hides product facts from holders and non-holders, stored grants remain unchanged, and unarchive restores only the holder's previous visibility. |

## Approved scenario accounting

| Matrix | Evidence |
|---|---|
| A1 — independent permission sets and rep isolation | `TabletSnapshotApiTests.Should_IncludeOnlyPermittedProducts_When_ARepHoldsSomeRestrictionGroups`, four theory cases. Existing temporary WI-032 exclusion expectation was intentionally replaced; unrelated archive/fact checks were retained. |
| A2 — serialized payload guard | `TabletRestrictionSnapshotTests`: checks the actual decompressed HTTP payload, including both positive permitted facts and absent denied data/count correctness. |
| A3 — hidden parent/category references | Dedicated endpoint test retains visible children/ancestors and clears denied parent references. |
| A4/A5 — grant/removal takes effect at next Sync | Endpoint tests reuse the same authenticated HttpClient before and after changing the stored grant. |
| A6 — product newly assigned to an unpermitted group | Endpoint test verifies its product facts disappear on the next snapshot. |
| A7/A8 — archive/unarchive | Endpoint and existing shared-rule tests check visibility and unchanged stored grants. |
| T1 — active reads, policy A | `RestrictedTabletTests` proves current page/search/direct reads omit the product while an older recovery row still exists. |
| T2 — failed replacement | Focused filtered-snapshot failure test plus existing atomic-activation/interruption suite preserve the old day and captured work. |
| T3/T4 — captured values and no re-add | Real SQLite/order-store tests assert unchanged captured bytes/identity/values and refusal of both new additions and increases. |
| T5 — only missing lines marked | Real Unsent view model and actual NewOrder/Sync components assert exact wording and preserved values; live browser checks cover the rendered views. |
| T6 — review/ready/send | Component events exercise the In Progress review action without starting Sync; actual app boundary has a positive current-session case; real API end-to-end theory covers removal and archival through Sent/Pending. |
| T7 — expired/different sessions | Actual app guard tests cover expiry, sign-out, another rep and a new session for the same rep; component events also reject an old page's action. |

T1-T4 characterisations passed before production changes. The new model/app tests initially failed
because the features were absent. Existing Tablet Core and component suites were green before changes.
No test was weakened, deleted or skipped to achieve green. The full named matrix and approval history
are recorded in `plan_docs/.agent-notes/WI-043.md`.

## Verification

- Clean Release solution build with warnings treated as errors and the repository's existing
  NU1902/NU1903 exception: **0 warnings, 0 errors**.
- Increment checks: **38 snapshot/visibility tests**, **237 Tablet Core tests**, **83 component tests**,
  and **4 real API upload round trips** passed, with zero failed/skipped.
- JavaScript: console status-store **11**, tablet sign-out **4**, identity admin UI **18**, product-unit form **5** passed (**38 total**).
- Demo shell-script tests: **5 passed** under WSL on a temporary LF-normalized copy. The initial Windows/CRLF invocations failed before mock execution; no repository test or script was changed.
- Android Release publish succeeded, then was repeated to include the footer-clearance correction.
  Final APK SHA-256: `6292795B34CD72A1BA5E353227311C7AB1D0E3CF4B51D6AB8EC2E6A1D2E9BAFB`.
- Full application suite: **2,926 passed across all 12 test projects**, zero failed/skipped, serially against SQL Server (exit 0).

Interactive visual verification used the actual tablet Razor components with synthetic data in a
temporary localhost Blazor host. NewOrder and Sync review were inspected at 390, 768 and 1280 px.
Denied-product search returned no add action; the warning appeared only on the captured missing line;
captured name/quantity/price remained visible; review marked the Order ready; no browser errors remained.

The short-height check initially found the last line 36 px behind the footer at 390x640. After the
CSS correction, the line clears it by 64 px at 390x640 and 390x844; 768x640 and 1280x800 also pass.
No horizontal overflow; the review Mark ready control is 56 px high. Screenshots and logs are local
artifacts under `.artifacts/wi043/`. No physical-tablet install/sign-in was performed for this item.

The temporary preview initially lacked its framework script assets; only its scratch host configuration
changed to enable the interactive check. This did not change the application's hosting configuration.

## Delivery checks

- PASS: S1-S4 demonstrated within the explicitly approved scope and policy A, with evidence above.
- PASS: provisional commit messages reconciled with the actual implementation/test diff.
- General order editing and later pickers remain with their own work items; no requirement is silently
  claimed as implemented on a surface that does not exist yet.
- Console changes are restricted to `workItemStatus.WI-043`. No merge/rebase/fast-forward/force-push or
  branch deletion is part of this delivery. Existing unrelated local edits are excluded from commits.
