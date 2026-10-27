# WI-032 — production slice plan: tablet sign-in and the day's download

## Selection and approval boundary

Continue **WI-032 — Sign in on the tablet and download the day's data offline** (T-4.1.1,
REP-US-001 / IAM-US-003), Iteration 1. It remains the sole active item: Human Tight-Loop,
Human-Led tests, Complex, High risk, Low confidence. Dependencies WI-002, WI-021 and WI-005 are
done. The spike (increment 1 of the source work package) is concluded for Android, and 1C is
decided in [tablet-offline-1c-decision.md](tablet-offline-1c-decision.md). This plan covers the
remaining work-package increments 2–8. It needs **Approved** for its decisions and scenario list
(T-4.1.1-S) before any test or production code. Every increment below then stops for review.

**Slice:** a rep signs in on the tablet, taps Sync and gets their assigned shops and the catalogue
offline, replaced in one piece, with Home showing when they last synced.

Acceptance criteria quoted from the card:

- With signal, Sync downloads the Morning Snapshot and Home shows "Last synced 07:42" (US-001 S1,
  download part).
- A snapshot download never alters Unsent items.
- No signal shows "No connection — your work is saved on this tablet".
- A rep who signed in with signal 2 hours ago can reopen the downloaded day and capture offline.
  After 24 hours offline, Home waits for connected sign-in and saved work remains (US-003 S2, S4).

Already settled, so not re-decided here: the 1C runtime and storage decision; WI-002's staff
identity (identity host username/password page, exact business-role names, `fieldsales.api` scope
and `fieldsales-api` audience, roles refreshed per request); I3.1 (24 hours from the last connected
sign-in, not extended by reopening); I3.2 (a blocked Sync resumes after sign-in, delivered by
T-4.6.1); and T1.3 (no automatic Sync).

## Decisions for approval

| # | Decision | Recommendation |
|---|---|---|
| P1 | Full snapshot every Sync, or deltas? | **Full snapshot every Sync.** The stories say Sync always downloads, and the spike shows a full target-scale refresh within the 90 s budget. |
| P2 | Build on demand or pre-build each morning? | **On demand, per Sync, for the caller only.** Always current, with no scheduler. Revisit if server build time threatens the budget. |
| P3 | Contract versioning | The manifest carries `schema: field-sales-snapshot` and a **major.minor** version; the tablet sends the majors it reads. The server serves the newest major both support. Additive fields bump the minor, which older apps ignore; a removed or changed field bumps the major. The server keeps serving the previous major for at least one release. With no common major, Sync stops with "Update the Field Sales app to sync", keeping data and work. |
| P4 | Contract owner | **One contract project, `src/FieldSales.Snapshot.Contracts`, owned by the tablet-sync lane.** Every later card that changes it states the version bump and adds contract tests. **Owner: the developer (ColmKenna), as T-4.1.1's owner (confirmed 2026-10-24).** One snapshot version change at a time, each reviewed by the owner, as the index requires. |
| P5 | Field list and address | **Approve the v1 field list below.** Locations hold only Town and Eircode, so v1's "address" is **Town + Eircode** until a card adds address lines. |
| P6 | Project layout | `src/FieldSales.Tablet` (MAUI app, in its own `FieldSales.Tablet.slnx` like the spike), `src/FieldSales.Tablet.Core` (storage, sync and session logic, desktop-testable) and `src/FieldSales.Snapshot.Contracts`. Core, Contracts and their tests join `FieldSales.slnx`, so CI needs no MAUI workload. Storage code is promoted from the spike with tests; the spike stays as reference. |
| P7 | Tablet identity client | Public native client **`fieldsales-tablet`**: authorization code with PKCE, no secret, redirect `com.fieldsales.tablet://signin-callback`, scopes `openid profile roles fieldsales.api offline_access`. Refresh token one-time use with a **24-hour absolute lifetime (developer, 2026-10-24)**, held in Android SecureStorage. Online sign-in and the offline window therefore both last 24 hours from a connected sign-in; refreshing tokens never extends the local window (I3.1). Sign-in uses the **system browser** (RFC 8252), not an embedded web view. Only an active **Field Salesperson** may start a tablet day; other staff see "This app is for Field Salespeople." |
| P8 | Development and device connectivity | adb reverse for `localhost:7201` (identity) and `localhost:7204` (API) over HTTPS. The Samsung trusts the ASP.NET development certificate: a **one-time human step** that the developer has agreed to do in increment 4: install its CA on the tablet, which the system browser needs. The agent exports the certificate and copies it to the tablet. A debug-only network security override covers the app. Release builds never trust it. |
| P9 | Screens in this slice (MI-08) | **Approve I-03 as drawn** (sign-in, first use with no signal, over 24 hours, no previous rep's data). **Minimal T-01:** last sync, Sync now, a progress region, no connection, update required. **Minimal T-02:** "Last synced 07:42" and the downloaded Locations list. Search, the Location screen, Needs Attention and the full Home belong to their own cards. |
| P10 | Local session | The 24 hours start at the last successful connected sign-in, stored per rep. If the device clock reads earlier than that sign-in, or the window has passed, Home is locked (**fail closed**). The locked screen shows no rep data. Each rep's snapshot and Unsent stores are keyed by identity subject, and another rep signing in never sees them. Sign-out and automatic logout behaviour is T-4.7.1. |
| P11 | S5 and MI-62 | WI-032 proves that each Sync replaces the snapshot with the server's **current** view. Uploading earlier work is T-4.1.2, and restricted products are WI-043. The fully disabled account (MI-62) stays **undecided**: no behaviour is built and its scenario is listed as undecided. |
| P12 | Defaults carried from 1C | **Confirmed by the developer (2026-10-24):** a free-space check before Sync (twice the snapshot database size plus 512 MiB, otherwise refuse and keep data), snapshot and Unsent databases excluded from Android backup, and reliance on Android file-based encryption. |
| P13 | Server access | `GET /tablet/snapshot` serves **the caller's own** snapshot only, with no rep parameter. It requires the Field Salesperson role and the `fieldsales.api` scope, with roles refreshed per request as today. A manager or Head Office user without that role is refused. |

## Snapshot contract v1 (proposed, P5)

Manifest: schema and version, rep subject, server build time (UTC), the Irish calendar date it
was built for, per-family counts, and the gzip and plain SHA-256 hashes. Payload families:

- **Locations:** the rep's assigned Locations from T-3.1.1's resolver (`EffectiveOwnerResolver`).
  Carve-outs to another rep and unassigned Locations are excluded. Fields: id, name, customer id
  and name, Town (id, name, county name), Eircode, and the main contact (name, type, phone, email)
  if it is active.
- **Categories:** id, parent id, name. The tablet derives breadcrumbs from the parent chain.
- **Products:** products with **no Restriction Group**: id, code, name, category id, unit, quantity
  step, minimum quantity and parent product id. WI-043 later adds permitted restricted products
  through `RestrictedProductVisibility`.
- **Base prices:** for each included product, the price in effect on the build date plus every
  future-dated price (product id, effective from, amount). Superseded past prices are omitted.

## Increments (each stops for review)

1. **Contract and server builder.** Add `FieldSales.Snapshot.Contracts` (records, version
   constants, JSON options, manifest hashing) and `FieldSales.Api/TabletSync/` (`SnapshotBuilder`
   from the Directory and Catalogue contexts plus the resolver, `TabletSyncEndpoints` with streamed
   gzip, P13's policy and version negotiation). Add integration tests against SQL Server
   Testcontainers for scenarios 1–8. No tablet code. **STOP.**
2. **Tablet storage core.** Add `FieldSales.Tablet.Core`, promoting the spike's verified
   import, atomic switch, separate Unsent store, pinned-SQLite preflight and bounded parser for
   contract v1. Add D2-B's post-switch cleanup, resumed on open, the free-space check and per-rep
   stores. Desktop tests with real SQLite files and child-process kill cover scenarios 9–13. **STOP.**
3. **Identity and local session.** Register `fieldsales-tablet` in the identity host (a new native
   client seed alongside `SeedClientSpec`, with lifetimes from configuration) and add host
   integration tests. In Core: the session clock rules (P10) and the token and SecureStorage
   abstraction. Tests cover scenarios 15–22, with sign-in and clock fakes where needed. **STOP.**
4. **Tablet app.** Add the `FieldSales.Tablet` MAUI app: I-03, minimal T-01 and T-02, Sync wired to
   the server and Core, backup exclusion and debug-only trust. Release build and install on the
   Samsung, then human and automated device checks for S1–S4, no connection, update required and
   the budgets (scenario 25). The developer completes the one-time certificate step. **STOP.**
5. **Final verification.** Full .NET suite and tablet build, device scenario evidence, card
   acceptance criteria one by one, results doc and notes, then WI-032 marked `done` only after
   everything passes. Commit and push. No PR or merge unless asked.

Commits use `feat(wi-032): …` and `Refs: WI-032, T-4.1.1, REP-US-001, IAM-US-003`, with no attribution.

## Human-Led scenario list for approval (T-4.1.1-S)

| # | Scenario | Intent | Level |
|---|---|---|---|
| 1 | Should_ContainOnlyTheRepsAssignedLocations_When_BuildingTheSnapshot | Resolver-assigned Locations only; carve-outs and unassigned excluded | API integration |
| 2 | Should_IncludeTownEircodeAndActiveMainContact_When_ALocationIsAssigned | v1 Location fields; no retired contact | API integration |
| 3 | Should_ExcludeEveryRestrictedProduct_When_BuildingContractV1 | No restricted product reaches v1, whatever the permissions | API integration |
| 4 | Should_IncludeCategoriesUnitsAndSteps_When_ProductsAreIncluded | Breadcrumb chain, unit, step and minimum | API integration |
| 5 | Should_IncludeTodaysAndFutureBasePrices_When_PricesAreDated | The price in effect plus future-dated ones; past ones omitted | API integration |
| 6 | Should_ServeOnlyTheCallersOwnSnapshot_When_StaffRequestSync | Field Salesperson only; no other subject; others refused | API integration |
| 7 | Should_RefuseWithUpdateMessage_When_NoContractVersionIsShared | Version negotiation; data and work kept | API + Core |
| 8 | Should_ReflectCurrentServerData_When_SyncingAgain | A reassigned Location or changed price shows at the next Sync | API integration |
| 9 | Should_ReplaceTheSnapshotInOnePiece_When_DownloadCompletes | Whole new snapshot active; Last synced recorded | Core integration |
| 10 | Should_KeepTheOldSnapshotAndUnsentWork_When_DownloadIsInterrupted | Card test intent: never a mixed snapshot | Core integration |
| 11 | Should_NeverAlterUnsentWork_When_ASnapshotIsDownloaded | Calls, Orders and schedule changes unchanged | Core integration |
| 12 | Should_FinishCleanup_When_TheAppStopsAfterTheSwitch | D2-B: leftover copies removed on next open | Core integration |
| 13 | Should_RefuseSyncAndKeepData_When_FreeSpaceIsBelowTheCheck | P12 capacity rule | Core integration |
| 14 | Should_ShowNoConnectionMessage_When_SyncHasNoSignal | Exact text; work saved; nothing changes | Core + device |
| 15 | Should_OpenHomeAfterConnectedSignInAndSync_When_FirstUse | US-003 S1: Locations and last Sync time | Device |
| 16 | Should_ReopenAndCaptureOffline_When_ConnectedSignInWasTwoHoursAgo | US-003 S2 (card intent) | Core + device |
| 17 | Should_ExplainConnectionNeeded_When_NoSnapshotAndNoConnection | US-003 S3; no other rep's data | Core + device |
| 18 | Should_KeepUnsentWorkAndRequireSignIn_When_TwentyFourHoursHaveElapsed | US-003 S4 (card intent) | Core + device |
| 19 | Should_NotExtendTheWindow_When_TheAppIsReopenedOffline | I3.1 | Core |
| 20 | Should_LockHome_When_TheDeviceClockReadsBeforeTheLastSignIn | P10 fail closed | Core |
| 21 | Should_KeepEachRepsDataSeparate_When_AnotherRepSignsInOnTheTablet | Identity-bound stores | Core + device |
| 22 | Should_RefuseTheTabletDay_When_TheAccountIsNotAFieldSalesperson | P7 role rule | Identity + Core |
| 23 | Should_UseCurrentServerAccess_When_AccessChangesBeforeTheNextSync | US-003 S5, download part only | API + Core |
| 24 | MI-62 — whole account disabled before reconnect | **Undecided**; no test or behaviour | — |
| 25 | Should_MeetTheAgreedBudgets_When_SyncingOnTheSamsung | Refresh under 90 s and open under 3 s at target scale | Device |

The 500-Location search budget is re-verified by T-4.2.1 (search), which owns that screen.

**Increment 4b amendment (developer decisions, 2026-10-26):**
- **Scenario 18 on the device:** the developer moves the Samsung's date forward more than 24 hours in
  Settings, checks that the sign-in screen appears and that Unsent work is kept, then sets the date back.
  The app keeps no test hook for this.
- **Scenario 25 at target scale:** a dev-only DemoData command, `./scripts/demo-data.sh scale`, adds
  its own seed batch (`scale-v1`) on top of the sample seed. The batch adds 500 shops in Swords for Colm,
  25,000 products in 800 categories, and today's price plus two future prices for each product
  (75,000 prices). It is applied once and never changes the sample records.
  The app writes `open` (process start to the first Home with the shops list), `sync` and
  `cleanup` durations to the Android log under the `FieldSalesTiming` tag. The rep sees none of this.

## Out of scope, risks and missing information

Out of scope: uploading work (T-4.1.2), sync failure causes (T-4.1.3), sign-in resume (T-4.6.1),
sign-out and automatic logout (T-4.7.1), search and the Location screen (T-4.2.x), Needs Attention
(T-4.3.1), restricted products (WI-043), the full Home (T-4.5.1), Unsent capture screens (E5),
the iPad (deferred by 1C), MI-62, production hosting and distribution, and any PR or integration.

Risks **(inferred)**:
- Duende's native-client flow and Android's system-browser redirect need device proof in
  increment 4.
- The development certificate step is manual.
- The tablet does not charge over the current USB link, so device sessions need the screen-timeout
  workaround.
- The server build time at target scale is unmeasured.
- Promoting spike code must keep its verified behaviour, so the spike's scenario tests are ported
  first as characterisation.

Missing information:
- A production address source beyond Town and Eircode (P5).
- MI-62.

No placeholder remains unresolved for this slice: `{{TABLET_CLIENT_STACK}}` is resolved by 1C.
