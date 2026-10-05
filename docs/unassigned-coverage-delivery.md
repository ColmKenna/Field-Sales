# WI-028 — Unassigned Locations

A Sales Manager or Head Office User can open **Coverage → Unassigned Locations** to find shops with no effective Primary Rep, grouped by Town. Each row shows its Customer and leads with **Assign <Town> (Town) to...**, the Town’s unassigned count, and **Assign just this shop to...** alongside it.

Both choices reuse the existing Location assignment chooser, impact preview, explicit confirmation, authorization and atomic assignment/history writes. Cancel returns to the list without saving. A confirmed save returns to the refreshed list with the receiving rep/source in a status notice; resolved shops leave the list. Ordinary Location-page entry keeps its existing navigation.

## Acceptance evidence

The agreed U1–U5 scenarios are implemented in `FieldSales.Web.Tests/UnassignedCoverageEndToEndTests.cs` and `UnassignedCoveragePageTests.cs`, using real authenticated BFF/API requests and SQL Server fixtures for persistence.

| Criterion (source T-3.3.1) | Evidence |
|---|---|
| “Unassigned Locations are grouped by Town, each with its Customer (and Location Profiles once E12 exists).” | U1 verifies five unassigned shops in two Towns, Customer labels and all-shop visibility for both manager roles. Foreign-owned shops are excluded by effective ownership, and identical Town names remain separate by ID with County/Region context. Location Profile data is absent from the integrated baseline; E12 remains the conditional follow-up. |
| “Each row leads with ‘Assign <Town> (Town) to...’ and the Town's unassigned count, with ‘Assign just this shop to...’ beside it.” | U2 asserts exact labels, count and action order; desktop rendering places both actions on the row and phone rendering wraps within the viewport. Singular counts and archived-hierarchy disabled actions are covered. |
| “Assigning Walsh's Shop to Colm shows ‘Colm (assigned directly)’ and it leaves the list.” | U3 follows the row through preview/confirmation, asserts a direct Location assignment and captured actor/reason history, the exact owner/source notice and absence of Walsh’s from the refreshed list; neighbouring shops remain unassigned. |
| “Assigning Laragh to Colm resolves every unassigned Laragh Location to ‘Colm (via Laragh)’.” | U4 confirms Town assignment from a row, checks all five unassigned Laragh shops inherit Colm, verifies the notice/list removal and preserves an existing direct override in a separate case. |
| “With none unassigned the list reads ‘All Locations have a responsible rep’.” | U5 checks the exact empty state with ownership at Region, County, Town and Location levels. |

The list reader uses the existing `EffectiveOwnerResolver` and four queries inside one serializable transaction, so labels, membership, counts and geography availability share a snapshot. It does not fetch staff labels for unassigned rows or query once per shop. The typed client rejects malformed groups, owner-bearing rows, wrong Town context/counts, duplicate identities, missing labels and unavailable responses instead of showing a misleading empty list. Actual Razor rendering encodes names and keeps access tokens out of HTML. Tampered/stale entry proofs, wrong shop IDs, unmanaged recipients and missing antiforgery tokens produce no writes. Existing Location regression cases preserve confirmation, replay, current authority and stale-proof behavior.

## Verification

- Pre-change coverage characterization: **261/261 passed**, no failures/skips; every TRX result Passed and run Completed.
- Final affected suite: **214/214 passed**, including all **46 new cases** (21 authenticated BFF/API/SQL and 25 typed-contract/render cases).
- Complete .NET regression: **2,171/2,171 passed** (API 268, Identity 1,049, Web 854). Every project Completed; every individual TRX result Passed; no failures/skips. Results under `.artifacts/wi028/full-regression`.
- Warnings-as-errors solution build: zero warnings/errors.
- Existing JavaScript suites: **34/34 passed** (18 identity/admin, 5 product-unit, 11 delivery status).
- Actual captured Razor output checked in the in-app Browser: standard list at desktop 1280px and phone 390px; maximum-length labels at 390px; exact empty state. No horizontal overflow. Tab from the Town action focuses the shop-only action. Evidence under `.artifacts/wi028/rendered`; the local test server and temporary browser tab are closed and the viewport override reset.

## Scope and limitations

Only T-3.3.1 is delivered. The list intentionally contains **all** unassigned shops. The overview count and manager-area scoping follow in E14, gap lists in E17, and Location Profiles in E12. No schema, ownership-policy, mutation API or visit/handover changes.

The provisional source commit intent is reconciled with the final diff: the change introduces list discovery and reuses reviewed assignment flows to fix coverage from each row. The developer authorized integration on 2026-10-05. [PR #9](https://github.com/ColmKenna/Field-Sales/pull/9) merged into main at `57da316d0d968edf9ea7ea263f0750bb03e84245` after [GitHub CI](https://github.com/ColmKenna/Field-Sales/actions/runs/37288553295) passed all 2,171 tests at the verified branch head, with zero build warnings/errors.
