# WI-034 — Search assigned shops offline

T-4.2.1 / REP-US-004, on `feature/wi-034-search-assigned-shops-offline` from `main` at `cffc3bf`.
The developer approved the plan and scenarios, authorized Apply, reviewed the physical timing
checkpoint, and resumed with `Continue T-4.2.1` on 28 October 2026.

## Result and scope

Home links to offline shop search. A query matches literal substrings of assigned snapshot
Location names, ignoring case and accents and treating straight/curly apostrophes equivalently.
Blank or whitespace input does nothing; Clear removes previous results. Names are prominent,
with Town on the second line. Large rows open the explicitly approved minimal name/Town page
with Back navigation. The full Location screen belongs to WI-035.

`LocationSearchIndex.cs` reads a single active generation transactionally, then normalizes and
sorts an immutable in-memory projection. No network is involved in search, and no snapshot
contract, version, API, database schema or dependencies changed. The screen reads off the UI
thread and rejects outdated input work. Entry and resume reuse `OfflineCataloguePage`'s
current-rep and 24-hour access guard, clearing prior state before the access check and new read.

The source's Closed-location edge is deferred by explicit developer approval: neither the
domain nor the snapshot has closure status yet. No closure status is fabricated. The existing
disabled-account decision (MI-62), startup-speed policy and deferred iPad check are unchanged.

## Acceptance criteria

1. **“With 312 assigned Locations and no signal, typing 'Murphy' shows 'Murphy's Pharmacy
   (Rathdrum)' and 'Murphy's Stores (Aughrim)'; the name is dominant, the Town secondary.”**
   `LocationSearchTests` imports 312 Locations through the real snapshot store and verifies
   the exact two names and towns with online reads configured to throw. The physical device
   verifies the name/Town visual hierarchy and both names using Swords as its fixture town.
2. **“Typing 'Quinn' with no match shows 'No assigned locations match 'Quinn'' with a note
   that only assigned Locations are searchable.”** Device UI XML verifies both exact messages.
   Actual-source component checks verify the explanation appears on a nonblank no-match result;
   initial blank, Clear and whitespace show neither message nor results.
3. **“Results update within 1 second for 500 Locations on the slowest supported tablet.”**
   The approved Android target, physical Samsung SM-P610, used exactly 500 Locations with Wi-Fi
   off and service routes removed. All 16 completed input-to-visible observations passed;
   maximum **110.1 ms**. Index build: **122.149 ms**, then **53.221 ms** on reopening.
   See [checkpoint evidence](tablet-location-search-checkpoint.md) for method, every trial,
   fixture limits, APK identity and cleanup. No claim is made for the deferred iPad target.
4. **“Rows are large one-handed touch targets and open the Location (T-4.2.2).”** Rows use
   80 CSS-pixel link targets. Device tap opens the selected snapshot Location's name and Town,
   and Back returns to search. This is the approved minimal destination; WI-035 supplies the
   remaining Location screen, Calls, Orders and actions.

## Approved scenario verification

| Scenario | Evidence |
|---|---|
| 312 assigned Locations / Murphy | Core test: exact source names, Towns and IDs |
| Quinn / assigned-only explanation | Device oracle + actual-source component check |
| Partial words / case / trimming / literal wildcards | Core theory + device oracle |
| Straight/curly apostrophes | Core theory + physical device |
| Accents and composed/decomposed text | Core theory; unaccented `cafe` on physical device |
| Blank input does nothing | Core theory; device verifies no additional search timing events; component checks |
| Active snapshot replacement / per-rep isolation | Characterization before production changes + Core replacement test |
| Clear on expired access / rep change | Existing real session-rule tests + actual-source component guard checks |
| 500-Location performance | Physical target-device checkpoint |

The isolated framework renderer compiles the actual search Razor source and shared guard,
with a local TabletApp adapter controlling entry outcomes. Its ten checks verify initial blank,
matching results, clear-before-access, expired-entry redirect, rep-change clearing, per-rep
reads, newer input winning over queued old input, Clear invalidating queued work, whitespace,
and absence of unhandled rendering/event errors. It does not replace real Core session-rule
tests or the physical MAUI measurement. Raw harness and output are in ignored
`.artifacts/wi034-component-check/` and `.artifacts/wi034-component-check.log`.

## Verification

- Characterization passed before production edits; Tablet.Core: 86 passed, none skipped,
  including 14 new cases.
- Actual-source component checks: 10 passed.
- Release Android APK passed and was exercised on the physical tablet. Production source has
  not changed since that device run; its SHA-256 is in the checkpoint report.
- Full Release solution build passed with no errors. Its 45 warnings are existing NU1902/NU1903
  package advisories; no new compiler warnings.
- JavaScript: 34 passed, none skipped.
- Shell/Python: 5 passed unchanged in a disposable offline Linux container using an LF checkout.
- Complete .NET suite: **2,618 passed, 0 failed, 0 skipped**, across all 10 test projects.
  API integration 139; demo data 4; identity integration 799; snapshot spike 22; tablet Core 86;
  tablet spike 41; website integration 859; API unit 182; identity unit 258; website unit 228.
  The complete run passed without retries. After tightening the source fixture's exact
  apostrophe spelling, all 86 Core cases passed again. No test was weakened, skipped or deleted.
  Evidence: `.artifacts/wi034-full-suite.log`, `wi034-test-results/`, `wi034-suite-summary.json`,
  and `wi034-source-scenario.log`.

The six demo fixture rows, normal 502-Location snapshot and original Wi-Fi/route/timeout
settings were restored at the checkpoint. No task helper or temporary AppHost is running.
Existing unrelated untracked review documents are untouched. Only WI-034's status changes
inside `plan-data.js`. Commits use the required Refs trailer and contain no automatic attribution.

WI-034 is **done**. Next selection: **WI-035 — Open a shop's Location screen on the tablet**.
It is informational only; no further item is started, and integration remains the developer's decision.
