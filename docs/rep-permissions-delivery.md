# WI-029 — Grant or remove a rep's restriction permission with a record

A Sales Manager or Head Office User can open **Coverage → Rep permissions**, choose a rep and see every
Restriction Group as **Granted** or **Not granted**, with its last change: when, who and why. The rep's
own Sales Manager and any Head Office User can grant or remove a permission with an optional reason.
Every other manager sees the same page without change controls. Every grant and removal is kept in an
append-only record. A change takes effect at the rep's next Sync; the snapshot builder (WI-043) reads the
rep's current permissions through the existing visibility rule.

Source: T-3.4.1 in `plan_docs/iterations/iteration-01-mvp/03-rep-coverage-permissions.md`; story
COV-US-009 S1, S3, S4; design `plan_docs/uxdocs/05-manager.md` M-14 (M14.1).

## Decisions

The developer approved these on 2026-10-19 (recorded in `plan_docs/.agent-notes/decisions.md`).

| # | Decision |
|---|---|
| D1 | The rep's own Sales Manager (current reporting line) or any Head Office User may change, for any rep holding Field Salesperson, including a Sales Manager who also sells. Any Sales Manager or Head Office User may view any rep. |
| D2 | Nobody changes their own permissions, Head Office included. The record also refuses it in SQL. |
| D3 | The reason is optional for grant and removal: trimmed, at most 1000 characters, no control characters. |
| D4 | No new grant on an archived group. Removal stays allowed and is recorded. The per-rep page lists archived groups only while the rep holds them, with Remove only. |
| D5 | Stored in CatalogueDb beside the groups (code under `FieldSales.Api/Coverage`): `RestrictionPermissionChanges` is the append-only record; `RestrictionPermissions` holds current grants with a restrictive foreign key to `RestrictionGroups`. The rep's reporting line is read in a serializable DirectoryDb transaction held open until the catalogue write commits. |
| D6 | A stale or repeated change is refused ("This permission changed. Reload before saving.") and records nothing. |
| D7 | A group whose permissions were all removed may be deleted if no product uses it; the record keeps its captured name. |
| D8 | Grants need an active Field Salesperson; removals also work for a rep who has since become inactive; a failed identity lookup saves nothing. |
| D9 | `restriction-permissions` usage source for the archive message; `RestrictionPermissionReader.CurrentGroupIdsAsync` for WI-043. No tablet endpoint. |

## Acceptance evidence

| Criterion (T-3.4.1) | Evidence |
|---|---|
| "Granting Colm 'Pharmacy-only medicines' with reason 'Completed training 15 Sep 2026' records the date, my name and the reason; after Colm's next Sync the products appear on his tablet (S1)" | **Record: PASS.** `Should_TheRecordShowsDateMyNameAndTheReason_When_IGrantColmPharmacyOnlyMedicinesWithReasonCompletedTraining15Sep2026` (API, real SQL): Niamh, Colm's Sales Manager, grants it; the record holds 15 Sep 2026 14:02:30 UTC, "Niamh Byrne" and the reason, and the change displays as "Granted 15 Sep 2026 14:02 UTC by Niamh Byrne — Completed training 15 Sep 2026". Identity is read before any transaction and the reporting line under serializable isolation. V3 shows the same through the website. **Tablet: hand-off proven, appearance is WI-043.** No tablet or Sync exists yet. `Should_ReturnColmsHeldGroups_When_TheSnapshotBuilderAsksForCurrentPermissions` proves the snapshot builder's input: after the grant, `CurrentGroupIdsAsync("colm")` returns the group and `RestrictedProductVisibility.VisibleToRep` shows its product to Colm only; it is hidden again while the group is archived and after removal. |
| "Colm's permissions show each group as Granted or Not granted with its last change entry (S3)" | **PASS.** `Should_ISeeEachGroupWithGrantedNotGrantedAndTheLastChangeEntry_When_IOpenColmSPermissions` renders the real page for Colm's manager: three groups in name order with "Granted"/"Not granted", the latest change for each ("Removed 02 Oct 2026 09:30 UTC by Orla Kenny — Moved to the hardware team", "Granted 15 Sep 2026 14:02 UTC by Orla Kenny — Completed training 15 Sep 2026") or "No changes recorded", and the matching Grant/Remove controls. Opening the page records nothing. |
| "A Sales Manager who doesn't manage Ciara and isn't a Head Office User can view but not change Ciara's permissions (S4)" | **PASS.** `Should_ICanViewButNotChangeThem_When_IAmASalesManagerWhoDoesNotManageCiaraAndNotA` (website): Ciara is listed as "View only"; her page shows each group's state and last change, "You can view Ciara's permissions but not change them.", no Change column and no buttons; a forged change is refused with 403 and nothing is recorded. `Should_ForbidAndRecordNothing_When_AManagerOutsideTheTeamPostsAChange` proves the same at the API (read allowed with `CanChange` false; grant and removal 403). |

## Scenario map

All scenarios were agreed with the developer before test code (Human-Led).

| Scenarios | Tests |
|---|---|
| R1–R6, the record | `tests/Integration/FieldSales.Api/RestrictionPermissionPersistenceTests.cs` (10 cases): UTC time, actor, rep, group and reason stored; every change kept in order; SQL trigger (51029) and EF guard reject update and delete; reason blank, trimmed, 1000/1001 characters, control characters; a failure part-way saves neither table; a held group cannot be deleted, a group with only removed permissions can and its record keeps the name. |
| A2, the rule | `tests/Unit/FieldSales.Api/RestrictionPermissionRuleTests.cs` (12 cases). |
| A1, A3–A10, the API | `tests/Integration/FieldSales.Web/RestrictionPermissionIntegrationTests.cs` (9): S1 record; outside manager forbidden; Head Office any rep including a selling manager and a rep with no reporting line; no self-change; current team and roles on the next request, and a reporting-line change waiting for an in-flight grant (paused before its commit, blocking proved through `sys.dm_exec_requests`); identity unavailable, unknown subject, non-rep and inactive rep; stale, repeated and racing changes; archived group; Field Salespersons and unscoped tokens denied. |
| V1–V5, the page | `tests/Integration/FieldSales.Web/RestrictionPermissionPageTests.cs` (5): S3; S4; grant and remove from the page with notices, entry links and a stale form; missing antiforgery token; archived held group and the empty state. |
| I1–I4, hand-offs | `tests/Integration/FieldSales.Web/RestrictionPermissionHandOffTests.cs` (4): archive and un-archive notes count current grants only; Archive only while held, delete refused when a grant arrives after the confirmation, delete allowed once all are removed; snapshot hand-off with `VisibleToRep`; the permission source is required and its absence fails closed (503). |

Mutation checks, each restored afterwards: keeping the current grant on removal (R2, R6 fail);
disabling the EF guard (R3); releasing the reporting-line lock before the catalogue write (A6); letting
any Sales Manager change any rep (A2, A3, A6); ignoring antiforgery (V4); showing the Change column to
viewers (V2); counting the change record instead of current grants (I1, I2); not requiring the source
(I4).

## Design notes

- Grant and removal append one `RestrictionPermissionChange` (captured rep, actor and group names; no
  foreign keys) and insert or delete the current `RestrictionPermission` in one serializable catalogue
  transaction. Each request carries the last change id the page showed, so a stale page or a double
  submit changes nothing. Racing writers are serialized by SQL Server; the loser gets 409.
- Authorization is `RestrictionPermissionRule.CanChange` (pure). The API applies it using the actor's
  current roles and the rep's reporting line, read inside a serializable DirectoryDb transaction that
  stays open until the catalogue commit; a concurrent reporting-line change waits. No distributed
  transaction is used; staff identities are looked up before either transaction opens.
- `RestrictionPermissionUsageSource` counts current grants on the catalogue context, so the Restriction
  Group retirement reads it inside its own serializable transaction (the WI-009/WI-011 obligation for
  references owned by another area). It is a required source.
- The page and its client follow the existing coverage conventions: server-held tokens, payload
  validation that refuses inconsistent pages, no automatic retry of POSTs, PRG with a status notice.

## Changes to existing code and tests

- `CatalogueDbContext` gains the two tables and an EF save guard; migration
  `20261018235543_AddRestrictionPermissions` is additive (the regenerated snapshot's dropped
  `ValueGeneratedNever()` on `ProductAttributeValue.Id` was restored, as in WI-011).
- `RestrictionGroupListStore` requires `["products", "restriction-permissions"]`.
- Navigation links were added to the coverage navigation, the Manager and Head Office home pages and the
  rep's Territory page.
- `ReferenceListIntegrationTests` (WI-011): the fake permission count now replaces the real source
  instead of duplicating it; assertions unchanged.
- `ProductClassificationApiTests` (WI-012): reads the Restriction Group product count by its source key,
  because the list now has two usage sources; expected values unchanged.

## Verification

- Increment 0 baseline (characterization): API integration 44/44 and Web integration 121/121 for the
  affected areas.
- Final build (`dotnet build FieldSales.slnx --no-incremental -m:1 -warnaserror
  '-warnnotaserror:NU1902;NU1903'`): 0 errors; the only warnings are the pre-existing NU1902/NU1903
  audit codes (see Findings). No compiler or analyzer warnings.
- Complete .NET suite (`dotnet test FieldSales.slnx --no-build --no-restore -m:1 --settings
  sqlserver.runsettings`): **2,217/2,217 passed** — API unit 180, API integration 110, Web unit 133,
  Web integration 739, Identity Admin unit 258, Identity Admin integration 794, DemoData integration 3.
  Every TRX run Completed and every individual result Passed; none failed, skipped, aborted or not
  executed (`.artifacts/wi029/final`, not committed).
- JavaScript: 34/34 (identity admin UI 18, product unit form 5, delivery console status 11). The admin UI
  tests ran from an isolated copy verified byte-identical to the repository sources.
- Rendered evidence (`.artifacts/wi029/rendered`, not committed): desktop 1280 px and phone 390 px for
  the rep list, Colm's page, Ciara's view-only page, an archived group, a stale save and the empty
  state. At 390 px every page is exactly 390 px wide; tables with a Change column scroll inside their own
  container.

## Findings for the developer

- **NuGet audit:** `dotnet build -warnaserror` fails on main itself (before WI-029) with NU1902/NU1903
  for transitive `System.Security.Cryptography.Pkcs` (9.0.4, 10.0.8, 10.0.10),
  `System.Security.Cryptography.Xml` 10.0.10 and `MessagePack` 2.5.302. There are no compiler warnings.
  WI-029 was verified with only those two codes excluded from warnings-as-errors.
- **Test host:** the API's Aspire `AddSqlServerDbContext<CatalogueDbContext>` reads
  `ConnectionStrings:CatalogueDb` before WebApplicationFactory configuration applies, so
  `GeographyApplication`'s setting never reaches it. The permission tests replace the context on a
  per-test host (`RestrictionPermissionHost`) instead of changing the shared fixture.

## Scope and obligations

Only T-3.4.1 is delivered.

- **WI-030** (cohort grants from the group's page, CV009-A–D) must reuse `RestrictionPermissionRule`,
  `RestrictionPermissionWriter` and the record; the `(RestrictionGroupId, Sequence)` index is in place.
- **WI-031** owns the removal consequence statement (M14.2, CV009-E/F). Until then Remove saves directly.
- **WI-043** owns S1's "after Colm's next Sync the products appear on his tablet" and S2 ("No longer
  available to you", valid when captured). It must pass `RestrictionPermissionReader.CurrentGroupIdsAsync`
  to `RestrictedProductVisibility.VisibleToRep` rather than re-deriving visibility.

The source's provisional commit message ("grant restriction permissions per rep with a record … takes
effect at the rep's next sync; lines already captured still send") is reconciled with the diff: the
record, rule, page and hand-offs are delivered here; the tablet half belongs to WI-043. Commits follow
the card's `feat(wi-029)` format with `Refs: WI-029, T-3.4.1, COV-US-009`.
