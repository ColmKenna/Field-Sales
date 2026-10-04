# Location coverage and assignment history — WI-026

Managers can open /Coverage/Location/{id} to see a shop's current Primary Rep and the assignment that makes them responsible. History opens on its own page, newest first. Head Office also has access and a link from the Location master-data record; direct Location assignment rows link from the rep's Territory page.

## Acceptance evidence

| Criterion (T-3.2.1) | Result and evidence |
|---|---|
| 1. Murphy's Pharmacy shows “Primary: Aoife (via Rathdrum)”. | PASS — Should_RenderAoifeViaRathdrumAndDirectColm_When_ManagerOpensM07 uses real SQL/API/BFF with County coverage and a Town override. All four levels also exercise the existing resolver; removing the override reveals current County fallback. |
| 2. Byrne's Chemist, assigned directly, shows “Primary: Colm (assigned directly)”. | PASS — the same authenticated scenario creates a direct override, asserts the exact wording and Territory navigation. |
| 3. Walsh's Shop, with no assignment reaching it, shows “Primary: Unassigned” with an Assign action (the action itself is T-3.2.2). | PASS — Should_ShowUnassignedAndTownFirstAffordances_When_ShopHasNoCoverage covers zero/one/two other Town shops, excludes a different Town, verifies Town-first and single-shop disabled buttons and explanatory text. WI-027 owns enabling the actions; this item cannot save from the new pages. |
| 4. History shows entries newest first in the form “17 Sep 2026 14:02 — Colm → Aoife — via Rathdrum assignment — by M. Byrne”. | PASS — Should_RetainNewestFirstCapturedHistory_When_CurrentNamesChangeOrIdentityDirectoryFails exercises two production writes at equal timestamps, Sequence ordering, renamed staff/geography and unavailable identity directory with no historical label lookup. Should_ShowExampleAndConvertOffsetToUtc_When_HistoryHasCapturedNames proves exact example wording/UTC conversion. Direct assignment, geography change, removal and reason presentation are covered separately. Times are explicitly UTC in the History view. |
| 5. The page shows no list of open Visit Dues and no last-Call line. | PASS — authenticated page/SQL scenarios and real Razor render checks assert their absence; History is reached through a separate route, with no entries/latest change inline. |
| 6. The Specialists line is present but empty until E20. | PASS — the rendered Specials line contains only its label and an empty value; authenticated fixture and SQL page tests assert it. |

## Contract and authority

- New manager-shop GETs: /coverage/locations/{id}/page and /coverage/locations/{id}/page/history. Current Sales Manager or Head Office roles are required through the existing trusted role boundary; a subject and fieldsales.api scope are required. Field Salesperson or SysAdmin alone cannot read these pages.
- The approved “any shop” interpretation allows this new read view to include foreign-team and unassigned shops. Existing /owner, /history, rep lists/books and every assignment/reporting-line mutation retain WI-021 team authority. Head Office master-data editing is not exposed to managers.
- Current owner labels come from the existing trusted staff directory before SQL locks. Missing/invalid identities and unavailable/malformed/wrong-Location API responses fail closed; failures never become Unassigned. An inactive owner's trusted name stays readable.
- The shared EffectiveOwnerResolver supplies ownership. The shared Paths query gained an optional Town filter before projection so other-unassigned counts are computed correctly in SQL-supported queries. Coverage reads run in a serializable snapshot and write nothing.
- History reads use immutable captured fields and Sequence ordering without a current identity directory request. Existing AssignmentHistoryFormatter.Display and append-only history writes are unchanged. The new view supplies concise presentation with before/after, cause/source, actor and optional reason; retained labels survive renames.
- Typed BFF validation checks requested Location identity, names, source levels/IDs, nonnegative counts, per-entry Location identity, captured actors/owners, distinct history IDs and strictly decreasing positive sequences. Razor encodes names and reasons; access tokens remain server-side. Navigation uses known local routes.

## Verification

- Pre-change characterization: **42/42 Passed**, Completed TRX, unchanged assertions.
- Corrected focused owner/history/territory/new-view regression: **112/112 Passed**, Completed TRX. An initial new-reader SQL translation error and invalid new SysAdmin fixtures were corrected; no existing tests were weakened, skipped or removed.
- Final authenticated Razor renders: **11/11 Passed**, including maximum-length labels.
- Existing JavaScript: **34/34 Passed**; isolated identity test files match repository source byte-for-byte.
- Warnings-as-errors solution build: zero warnings/errors.
- Browser checks use the actual authenticated Razor fixture renders with final CSS. Primary/source, direct and unassigned states, separate History/empty History, phone width (390px), maximum-length names/reasons and keyboard History navigation verified. These presentation checks complement real BFF/API/SQL tests.
- Full solution regression: **pending**; completion will record exact Completed TRX totals before WI-026 is marked done.

Artifacts: .artifacts/wi026/{characterization,focused-corrected,browser-pages,regression,rendered}; build logs and javascript.log alongside them. Local-only generated artifacts are ignored by Git.

## Boundaries

No migration, stored ownership, new resolver rule, reporting-line editor change or package/runtime update. WI-027 supplies coverage-changing controls; WI-028 supplies unassigned inventory. Visits/handover, specialist assignments and online-ordering setup remain their respective later items. Business-local timezone is not selected here; the existing explicit UTC convention remains.
