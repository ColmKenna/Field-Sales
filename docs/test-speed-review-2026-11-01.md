# Test speed review: 1 November 2026

Stage 1 of the test feedback-loop review: diagnosis and plan only. No source, test, project or
CI file was changed. Every figure below comes from runs on 1 November 2026 unless it is marked
as an estimate.

## 1. Summary

| | Today | Proposed |
|---|---|---|
| Inner loop (between commits) | No defined tier. The documented unit command (`dotnet test FieldSales.slnx --filter "Category=Unit"`) takes **13.7 s** after an edit. It runs no tablet storage, component or misplaced Admin tests, so recent work items also ran Tablet.Core (27 s under CI settings) and SQL-backed API classes (API project 98–206 s) between increments | **About 16–17 s** after an edit (estimate from measured parts: about 7 s build plus 9.4 s for 996 tests) for the unit projects, Tablet.Components and Tablet.Core, without Docker. With items 5–12 added it covers about 1,080 tests plus the new unit tests |
| Work item / pre-commit | Full suite: **9 min 2 s to 10 min 50 s** of sequential test time, plus build, Docker required | **About 50 s** without Docker, plus the SQL suites for the areas touched: **0.5–3 min** (section 6.3) |
| CI / pre-merge | Every test, four lanes | Unchanged: every test still runs. Nothing is deleted, skipped or weakened |

Main findings:

1. **The tiers the brief assumed are not here.** There is no fast solution, no Journey project, no
   `Requires=SqlServer` trait, no `scripts/test.ps1` and no `AGENTS.md` (section 2). Stage 2 would
   create them rather than extend them.
2. **The SQL suites are where the time goes**, and none of them can be selected out today. 68
   classes (966 tests) need SQL Server. Nothing in the trait set distinguishes them from the 1,284
   Docker-free integration tests.
3. **Docker-free integration runs 2.5–3 times faster at default parallelism.** CI's
   `sqlserver.runsettings` caps xUnit at 2 threads. It applies to every integration project, including
   ones that never touch SQL Server: Tablet.Core takes 27.4 s under the cap and 8.8 s without it;
   Admin's Docker-free classes take 47.1 s and 19.6 s.
4. **API per-test catalogs.** Eight API classes create and migrate a fresh SQL catalog for every
   test: about 55 s of the 98 s warm run (about 107 s of 206 s cold).
5. **Web's critical path is one SQL collection.** 29 Directory and Coverage classes share one
   container and run in sequence for about 214 s. The cost is the tests themselves (two hosts over
   HTTP plus SQL), not setup.
6. **79 pure unit tests sit in the Admin integration project** (plus 3 in Tablet.Core). The unit
   filter never runs them; they run only inside the 158 s Admin integration run.
7. **A 5 s wait recurs in the Identity host.** One test in each of five Admin classes takes exactly
   about 5 s, always on the first request that reads signing keys. This matches Duende's documented
   `KeyManagement.InitializationSynchronizationDelay` default of 5 s. It is a strong hypothesis that
   still needs one confirming run (item 16).

The brief left the work-item budget as a placeholder (`<BUDGET>`). Section 6.3 shows what fits in
2, 5 and 10 minutes; the cadence assignments assume **5 minutes**.

## 2. What exists versus what the brief assumed

| Brief assumed | Found in this repository |
|---|---|
| Test classes carry `Category` traits `Unit`, `Integration`, `E2E` | All 66 unit classes carry `Category=Unit`. Only 4 of 199 integration classes carry `Category=Integration`. No class carries `E2E` |
| SQL Server suites carry `Requires=SqlServer` | No such trait anywhere |
| `tests/CpkSales.Fast.slnx` | Not present. Solutions: `FieldSales.slnx`, `FieldSales.Tablet.slnx`, `FieldSales.TabletSpike.slnx` |
| A Journey project | Not present; `tests/E2E/` holds only `.gitkeep` |
| Two SQL Server projects | None dedicated. SQL Server suites live inside the API, Admin, Web and DemoData integration projects, next to Docker-free classes |
| `scripts/test.ps1 -Tier …` | Not present |
| CI tiers in `.github/workflows/ci.yml` | Present: a fast job (build, `Category=Unit`, three Node suites, mocked-Docker Python checks); four integration lanes (`api`, `admin`, `web`, `supporting`) running `Category!=Unit` with `-m:1 --settings sqlserver.runsettings`; and the aggregate `Build & Test` check |
| `AGENTS.md` describes the tiers | Not present. `README.md` § Testing and `plan_docs/.agent-notes/README.md` give the commands |

Minor drift: [docs/test-performance.md](test-performance.md) says the supporting lane runs four
projects ("all seven projects" in total), but `ci.yml` now also runs Tablet.Components there.

## 3. Method

- **Machine:** Windows 11 (10.0.26300), 32 logical CPUs, 31.7 GB RAM; .NET SDK 10.0.401.
- **Docker:** Docker Desktop server 29.8.1, started at the beginning of this review (VM: 32 CPUs,
  16.6 GB). Image `mcr.microsoft.com/mssql/server:2022-latest` (`49b45a911dc5`, built 26 Feb 2026).
- **Build:** one Debug build of `FieldSales.slnx` (64.7 s, 0 warnings, 0 errors). All test runs
  used `--no-build --no-restore`, except the build-plus-test loop timings in section 4.1.
- **First pass:** each integration project ran in sequence with CI's arguments:
  `--filter "Category!=Unit" -m:1 --settings sqlserver.runsettings --logger trx`. The API project
  ran a second time after Docker had warmed up.
- **Docker-free runs:** Docker-free selections ran a second time with `DOCKER_HOST=tcp://127.0.0.1:1`,
  so any Testcontainers use would fail at once. Every selection passed, which shows it needs no
  Docker.
- **Durations:** wall-clock times come from a stopwatch around `dotnet test`. Per-test and per-class
  durations come from the TRX `startTime`/`endTime`/`duration` attributes.
- **Set-up versus per-test cost:** xUnit records fixture and `IAsyncLifetime` set-up outside test
  durations. One-off cost is therefore read from the gap before a class's first test plus that
  test's excess over the class median. Per-test set-up is read from the gaps between tests within a
  class (class span minus summed durations).
- **Results:** every run passed. **2,926 tests: 676 unit and 2,250 integration**, 0 failed and
  0 skipped.
- **Caveat:** these are single runs on one workstation, not statistical benchmarks. The API figures
  show how much state matters: the first SQL suite after Docker started ran 2.1 times slower than
  the warm re-run.

Scratch scripts and TRX files are outside the repository, in the session scratchpad
(`trx_report.py`, `classify.py`, `cadence.py`, `run-*.ps1`). They can be moved under `artifacts/`
if wanted.

## 4. Timings

### 4.1 Build and the edit-test loop

| Step | Wall (s) |
|---|---:|
| One solution build at the start (incremental over existing `obj/`) | 64.7 |
| No-op build: solution / unit projects only | 5.6 / 2.6 |
| Build after touching an API source file: unit projects / solution | 7.0 / 7.3 |
| Loop (build + test) after an API change: unit projects only / solution `Category=Unit` filter | **11.0 / 13.7** |
| Loop after a Web change / an Admin services change (unit projects) | 8.6 / 10.7 |
| Loop after a Tablet.Core change: unit projects + Tablet.Components | 13.8 |
| Compile cost of one test project alone (`--no-incremental --no-dependencies`) | 1.0–5.7 (API integration 5.7; others ≤ 2.3) |

The touches changed only timestamps, so no reference assembly changed and dependent projects did not
recompile. An edit to a public signature also recompiles the dependants: 1–6 s per test project,
partly in parallel. Building is not the bottleneck; choosing which tests to run is.

### 4.2 Per project

**Unit tier**

The solution-wide filter took 9.3 s wall on the first run and 8.2 s on the second. Running the four
unit projects alone took 6.7 s, because integration assemblies are not loaded for discovery.

| Project | Tests | TRX elapsed (s) | Sum of test durations (s) | Slowest test |
|---|---:|---:|---:|---|
| Api.UnitTests | 185 | 6.8 | 7.6 | `EircodeLookupTests.Should_BoundTheLookup_AndPreserveCallerCancellation`, 5.02 s (the production 5 s bound) |
| Web.UnitTests | 219 | 4.4 | 4.3 | `CatalogueHttpContractTests.SafeRead_RetainsRetrySupport`, 2.51 s (real retry delay) |
| Identity.Admin.UnitTests | 256 | 1.6 | 5.1 | 0.31 s |
| Tablet.Core.UnitTests | 16 | 1.3 | 0.04 | 0.01 s |

**Integration tier, first pass with CI settings (2 xUnit threads, projects in sequence)**

| Project | Tests | Needs Docker | Wall (s) | Sum of test durations (s) | Where the time goes |
|---|---:|---|---:|---:|---|
| Web.IntegrationTests | 873 | 39 of 53 classes | 224.2 | 244.6 | Directory SQL collection is the critical path (section 4.3) |
| Api.IntegrationTests | 186 | 18 of 19 classes | **206.4 cold / 98.0 warm** | 84.3 / 33.4 | Per-test catalogs: 107 s cold, 55 s warm |
| Identity.Admin.IntegrationTests | 804 | 10 of 97 classes | 158.1 | 194.6 | About 56 s Docker-free phase, then about 100 s serial SQL collection |
| Tablet.Core.IntegrationTests | 237 | no | 27.4 | 50.5 | SQLite fsync per test, capped at 2 threads |
| TabletSpike.IntegrationTests | 41 | no | 15.6 | 19.1 | `StorageScenarios`, 14 s sequential |
| DemoData.IntegrationTests | 4 | yes | 14.8 | 6.3 | 7.1 s container start |
| SnapshotSpike.IntegrationTests | 22 | no | 2.1 | 0.8 | |
| Tablet.Components.IntegrationTests | 83 | no | 1.5 | 0.4 | |
| **Total** | **2,250** | | **650 (10 min 50 s); 542 (9 min 2 s) with the warm API figure** | | |

Against the 28 October baseline in `artifacts/test-speed/`, comparing TRX elapsed times: Admin
(157 s against 164 s) and Web (223 s against 232 s) match. The warm API run (97 s against 78 s)
has 46 more tests. Tablet.Core grew from 86 to 237 tests and from 5.8 s to 26.5 s.

**Docker-free selections, Docker blocked**

| Selection | Tests | CI settings (s) | Default parallelism (s) |
|---|---:|---:|---:|
| Tablet.Core.IntegrationTests | 237 | 27.4 | **8.8** |
| Admin: 87 classes outside the SQL collection | 772 | 47.1 | **19.6** |
| Web: 14 host-only or SQLite classes | 126 | 6.6 | **4.8** |
| API: `StaffSessionAuthorizationTests` | 3 | – | 2.3 |
| Unit projects + Tablet.Components | 759 | – | 6.8 |
| Unit projects + Tablet.Components + Tablet.Core | 996 | – | **9.4** |

Non-.NET checks from CI's fast job: Web Node tests 0.2 s, tablet Node tests 0.1 s, Admin UI Node
tests (`npm run test:admin-ui`) 2.8 s, all passing. The mocked-Docker Python checks could not run on
this machine (section 10).

### 4.3 Slowest classes, with one-off and per-test costs

Columns:

- **Set-up before class:** the gap since the previous class ended (container start or class fixture).
  It is approximate where classes overlap.
- **First-test excess:** host boot or first-use cost absorbed by the first test.
- **Per-test set-up:** set-up recorded between tests within the class (per-test `IAsyncLifetime`).

API figures are from the warm run.

| Project | Class | Tests | Class span (s) | Set-up before class (s) | First-test excess (s) | Median test (s) | Per-test set-up (s) |
|---|---|---:|---:|---:|---:|---:|---:|
| Admin | TemplateAdoptionBaselineTests | 5 | 58.9 | 8.0 | 0.46 | 10.762 | 0.00 |
| Admin | DevelopmentMigrationSqlServerTests | 2 | 26.0 | 0.0 | 5.80 | 10.081 | 0.00 |
| Web | LocationCoverageActionIntegrationTests | 71 | 19.5 | 0.6 | 0.42 | 0.268 | 0.00 |
| Web | PermissionRemovalCohortPageTests | 33 | 18.4 | 0.6 | 0.23 | 0.534 | 0.00 |
| Web | CoverageMutationIntegrationTests | 32 | 17.2 | 0.7 | 0.34 | 0.118 | 0.00 |
| Web | ContactIntegrationTests | 26 | 17.0 | 1.2 | 0.39 | 0.298 | 0.00 |
| TabletSpike | StorageScenarios | 26 | 14.0 | 0.0 | 0.03 | 0.247 | 0.00 |
| Tablet.Core | TabletStorageScenarios | 36 | 12.7 | 0.0 | 0.00 | 0.353 | 0.00 |
| Web | ReferenceListIntegrationTests | 33 | 12.6 | 3.1 | 0.49 | 0.300 | 0.00 |
| Web | PermissionRemovalCohortTests | 27 | 12.3 | 0.6 | 0.32 | 0.441 | 0.00 |
| API | TerritoryAssignmentPersistenceTests | 22 | 12.0 | 5.2 | 0.30 | 0.043 | 0.49 |
| API | ProductSearchApiTests | 13 | 10.3 | 0.0 | 0.02 | 0.204 | 0.60 |
| Web | CustomerIntegrationTests | 39 | 10.0 | 0.6 | 0.30 | 0.080 | 0.00 |
| Web | PermissionRemovalReviewPageTests | 19 | 10.0 | 0.6 | 0.25 | 0.503 | 0.00 |
| Web | MainContactReplacementTests | 27 | 10.0 | 1.6 | 0.42 | 0.309 | 0.00 |
| API | ProductClassificationApiTests | 12 | 9.9 | 0.0 | 0.04 | 0.234 | 0.61 |
| Admin | Task02SqlServerIntegrationTests | 7 | 8.6 | 0.0 | 0.00 | 0.435 | 0.00 |
| Web | RestrictionPermissionCohortTests | 19 | 8.5 | 0.5 | 0.12 | 0.424 | 0.00 |
| API | TabletSnapshotApiTests | 34 | 8.1 | 0.9 | 0.28 | 0.094 | 0.12 |
| API | ProductAttributeApiTests | 9 | 7.2 | 0.0 | 0.38 | 0.186 | 0.64 |
| Tablet.Core | TabletSyncFailureTests | 27 | 6.8 | 0.0 | 0.00 | 0.285 | 0.00 |
| Web | AssignmentTransferIntegrationTests | 32 | 6.5 | 0.6 | 0.27 | 0.168 | 0.00 |
| Web | AssignmentImpactIntegrationTests | 35 | 6.5 | 0.6 | 0.35 | 0.111 | 0.00 |
| Web | BrandIntegrationTests | 24 | 6.3 | 0.6 | 0.24 | 0.133 | 0.00 |
| DemoData | DemoSeedTests | 4 | 6.2 | 7.1 (container, before first test) | 2.60 | 0.580 | 0.00 |

Where the time goes, by suite:

- **API (98 s warm, 206 s cold).** The container starts in about 6 s. All 18 SQL classes share
  `SqlServerCollection`, so they run one after another. Eight classes build a host, create a catalog
  and run the migrations for **every test**: 0.43–0.64 s per test warm, 1.2–1.6 s cold. That totals
  54.5 s warm or 107 s cold, against 33 s of test bodies. Classes already on the shared-schema reset
  pattern (`TabletSnapshotApiTests`, `TabletUploadApiTests`, from commit `8702bf4`) pay 0.11–0.12 s
  per test.
- **Web (224 s).** The Directory container is ready at 8 s; the first Catalogue test starts at 16 s.
  Class fixtures (new catalog, migrations, API and Web hosts) cost about 0.6–0.8 s each, 25 s across
  39 SQL classes. The rest is the tests themselves: 0.1–0.5 s each over HTTP through two hosts and
  SQL. The Catalogue collection finishes after about 62 s. The Directory collection (Directory
  subgroup 59 s busy, Coverage subgroup 135 s busy) then runs alone, so it is the critical path.
- **Admin (158 s).** First, about 56 s for 87 Docker-free classes. Each boots its own Identity host
  on in-memory SQLite (first-test excess about 0.2 s). Five classes also lose about 5 s once
  (section 4.4). Then the SQL collection, marked `DisableParallelization = true`, runs alone for
  about 100 s. 85 s of that is two classes that start a fresh Development host per test. That host
  migrates three SQL Server databases, about 10 s per test.
- **Tablet.Core (27.4 s under CI settings, 8.8 s at default parallelism).** There is no notable
  start-up. Each test costs 0.15–0.35 s of SQLite I/O, because the production configuration uses WAL
  with `synchronous=FULL` and no pooling (`TabletSqlite.cs:16–21`). The critical path at full
  parallelism is `TabletStorageScenarios`: 36 tests in sequence, 7.3 s.
- **Unit (6.7–9.3 s).** The tier's floor is set by two tests that wait on real time (5.0 s and
  2.5 s). Without them, every unit assembly finishes in under 2 s.

### 4.4 Recurring 5-second tests

| Test | Duration (s) |
|---|---:|
| `DiagnosticsServiceTests.GetDiagnosticsAsync_UserHoldingReservedClaim_IsReportedForRemediation` | 5.26 |
| `AdminSmokeTests.Should_Return200AndRenderLayout_When_RequestingDiagnosticsIndex` | 5.16 |
| `AdminLayoutNavigationTests.Get_KeysPage_HighlightsKeysNavItem` | 5.15 |
| `WebComponentLoadingTests.TablePage_LoadsOnlyTheTableModule(url: "/Admin/Keys")` | 5.09 |
| `StylesheetSeparationTests.AdminPage_LoadsSharedThenAdmin_AndNotPublic(url: "/Admin")` | 5.08 |

Each is the first request in its host to reach `IKeyMaterialService`, through the Keys page or
`DiagnosticsService`. The Testing host uses `AddDeveloperSigningCredential(false)`
(`Program.cs:160`) and leaves automatic key management at its defaults. In Duende 8.0.5 those are
`Enabled = true`, `InitializationDuration = 5 minutes` and
`InitializationSynchronizationDelay = 5 seconds`. `CanConnectAsync` against SQLite cannot account
for 5 s. The cause is very likely the key-management delay, but that is not proven: the options are
set in code and can't be overridden from outside, so a confirming run needs a code change (item 16).
The Development hosts in `TemplateAdoptionBaselineTests` and `DevelopmentMigrationSqlServerTests`
(about 10 s per test) may pay the same delay. That is not verified.

## 5. Category label audit

A static scan of all 265 test classes found no Unit-labelled class that boots a host, uses SQLite or
EF InMemory, starts a container or spawns a process. The mismatches run the other way.

| # | Finding | Classes | Effect |
|---|---|---|---|
| L-1 | Pure unit tests (Moq, reflection, in-memory configuration) in `Identity.Admin.IntegrationTests`, untagged | `ReservedClaimTypePolicyTests` (37), `BuiltInIdentityResourcePolicyTests` (24), `RoleServiceTests` (11), `AdminCreationPolicyTests` (4), `PageModelAuditCoverageTests` (3): 79 tests | Never selected by `Category=Unit` (neither the CI fast job nor the README command); run only inside the 158 s Admin integration run |
| L-2 | Pure JSON contract tests in `Tablet.Core.IntegrationTests` | `SnapshotAttributeContractTests` (2), `SnapshotAttributeCompatibilityTests` (1; compiles the V10 copy under `Compatibility/V10`) | Same as L-1 |
| L-3 | 195 of 199 integration classes carry no `Category` trait | All except `AdminWebFactoryTests`, `Pkcs12CertificateLoaderTests`, `SqlTicketStoreTests` and `StaffAreaServicePersistenceTests` | `Category=Integration` selects 4 classes; every integration selection relies on `Category!=Unit` |
| L-4 | No trait marks a SQL Server dependency | 68 classes, 966 tests: Web 39, API 18, Admin 10, DemoData 1 | The 1,284 Docker-free integration tests can't be selected on their own. Without Docker, `Category!=Unit` fails these classes instead of skipping them |
| L-5 | Unit tests waiting on real time | `EircodeLookupTests` (5.0 s; production `CancelAfter(TimeSpan.FromSeconds(5))` at `EircodeLookup.cs:37`), `CatalogueHttpContractTests.SafeRead_RetainsRetrySupport` (2.5 s; real retry back-off) | Labels are right (in-process), but these tests set the unit tier's floor |
| L-6 | `Tablet.Components.IntegrationTests` behaves like a unit project | 4 classes, 83 tests, 0.2 s | The Integration label is defensible (real Razor components over Tablet.Core types); placed in cadence 1 |
| L-7 | Unused database-provider packages in unit projects | `Identity.Admin.UnitTests` (EF InMemory, EF Sqlite, SQLitePCLRaw); `Web.UnitTests` (EF Sqlite, SQLitePCLRaw) | No current mislabel, but they invite one |

## 6. Cadences

### 6.1 Definitions

| Cadence | When | Contents | Measured or estimated time |
|---|---|---|---|
| **1 Inner loop** | Between commits | Unit projects, Tablet.Components, Tablet.Core (+ CrashHost), plus the classes moved under L-1 and L-2 and the new unit tests | About 16–17 s after an edit (about 7 s build + 9.4 s tests), no Docker |
| **2 Work item** | End of a work item, before committing | Docker-free integration: Admin host classes, Web host-only classes, the API JWT class, Admin's Docker-free infrastructure classes (about 27 s at default parallelism). Plus the SQL behaviour classes for each **area touched**: Catalogue, Directory, Coverage, Tablet sync, Identity | About 50 s + 0.5–3 min per area (section 6.3) |
| **3 CI / pre-merge** | Every push and PR (unchanged) | Everything. Ten classes run *only* here: Identity migration and adoption, DemoData seed, both spike-tool projects, and the API reference-migration up/down test | Unchanged: four lanes |

Totals: cadence 1 covers 89 classes (993 tests), plus 7 classes (82 tests) to move under L-1 and
L-2. Cadence 2 covers 159 classes (1,764 tests): 97 Docker-free and 62 SQL-backed. Cadence 3
covers 10 classes (87 tests) that run only in CI. Every class has exactly one cadence; Appendix A
lists all 265.

Tablet.Core stays labelled Integration: it writes real SQLite files with production durability
settings. It joins the inner loop because it adds only 2.6 s to the combined Docker-free run, and
the tablet is where the recent work items have been. If its disk-bound cost grows, it is the first
candidate to move to cadence 2.

### 6.2 Why each group stays out of the inner loop

The per-class version is in Appendix A.

| Group | Classes | Cadence | Regression it guards that faster tests do not |
|---|---:|---|---|
| Admin host classes (Identity host on SQLite) | 79 | 2 | Razor rendering and model binding, antiforgery on every POST, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services |
| Admin infrastructure, Docker-free (`DevelopmentSeederTests`, `ConcurrentTestStartTests`, `Pkcs12CertificateLoaderTests`) | 3 | 2 | Unreachable-SQL failure path; the barrier helper used by SQL concurrency tests; real PKCS#12 loading with production key-storage flags |
| Web host-only and SQLite classes | 14 | 2 | Real cookie auth, antiforgery, staff-area authorization and the resilience pipeline against stubbed API handlers; SQLite ticket and session stores |
| API `StaffSessionAuthorizationTests` | 1 | 2 | The only Docker-free check of the real JWT bearer pipeline: `MapInboundClaims = false`, role claim type `role`, audience and scope policy |
| Admin SQL behaviour (Identity area) | 6 | 2 (Identity) | Real SQL Server transactions and serialised concurrent writes; JWT-protected staff directory and role-lookup endpoints over SQL-backed users |
| API SQL classes (Catalogue, Directory, Coverage) | 14 | 2 (area) | Migrations applied to real catalogs; unique and filtered indexes, triggers and check constraints; serializable reads; EF translation; HTTP endpoints over SQL |
| API tablet classes (`TabletSnapshotApiTests`, `TabletUploadApiTests`, `TabletUploadEndToEndTests`) | 3 | 2 (Tablet sync) | Snapshot built in serializable SQL transactions; idempotent upload storage; restriction filtering in SQL; the only end-to-end check of the real tablet store syncing against the real API |
| Web SQL classes (Catalogue 10, Directory 10, Coverage 19) | 39 | 2 (area) | Pages through real HTTP to a real API host on SQL Server: the page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts |
| Identity migrations and adoption (`TemplateAdoptionBaselineTests`, `DevelopmentMigrationSqlServerTests`, `MigrationAdoptionSqlServerTests`, `DatabaseSchemaReadinessTests`) | 4 | 3 | Fresh Development migration, the adoption script, the schema-readiness gate, pinned-template host start, and the Identity host emitting the `role` claim |
| API `ProductReferencePersistenceTests` | 1 | 3 | Reference-data migration upgrade and rollback on real SQL Server |
| DemoData, TabletSpike, SnapshotSpike | 5 | 3 | The demo seed tool against real SQL Server; spike tooling under `tools/` (no production code path) |

**Gap no tier covers.** No automated test drives a Web OpenID Connect sign-in against the real
Identity host. The `role` claim is checked piecewise:

- the Identity host emits it (`TemplateAdoptionBaselineTests`, cadence 3);
- the API maps it from a JWT (`StaffSessionAuthorizationTests`, cadence 2);
- Web tests sign in with a synthetic cookie principal (`StaffWebsiteFactory`).

The chain between them is not tested. This is recorded for information; adding such a test is out
of scope here.

### 6.3 Work-item budget (`<BUDGET>` not given)

The table sums warm, CI-settings figures: test classes plus about 7–8 s container start per SQL
project. Add roughly 1–2 s of test-host start per project.

| Work-item selection | Estimated time |
|---|---:|
| Docker-free part (inner-loop set + Admin 19.6 s + Web 4.8 s + API 2.3 s) | about 45–50 s |
| + Tablet sync area (API tablet classes) | + about 25 s |
| + Identity area (Admin SQL behaviour classes) | + about 20 s |
| + Directory area (API 5.7 s, Web Directory subgroup 59 s busy) | + about 1.5 min |
| + Catalogue area (API 40 s, Web Catalogue collection 46 s) | + about 1.7 min |
| + Coverage area (API 22 s, Web Coverage subgroup 145 s) | + about 3 min |
| Every SQL behaviour class (all areas, cadence-3 classes excluded) | + about 5.5 min |

Under a **2-minute** budget, only the Docker-free part plus the Tablet sync or Identity area fits.
**5 minutes** fits any single area, Coverage included (worst case about 3 min 50 s). **10 minutes**
fits every SQL behaviour class, and Stage 2 would not need area scoping. A cold Docker start can
double the first SQL project's time (section 3).

## 7. Proposed unit tests for the inner loop

Each runs in milliseconds. "Sooner than" names the slower test whose feedback it brings forward.
None replaces that test.

| # | Sooner than | Logic exercised | Cannot catch | Effort |
|---|---|---|---|---|
| UT-1 | `TabletUploadApiTests.Should_RefuseWithoutStoring_When_TheBodyIsCorruptIncompleteOrUnsupported` (13 cases; API SQL project, 98–206 s) | `TabletWorkReceiver.ReadOrder`, `StructureFault`, `ReadCall`, `IsSnapshot` and `IsLine` (`src/FieldSales.Api/Ordering/TabletWorkReceiver.cs:90–141`): mapping a body to Corrupt, Unsupported or Incomplete | "Without storing"; idempotent resend and concurrent duplicates (unique key + serializable transaction); role and scope authorization; the endpoint size limit. **Needs** the private static methods made `internal` (or extracted) plus `InternalsVisibleTo("FieldSales.Api.UnitTests")`: a small production change | M |
| UT-2 | `RestrictedProductVisibilityTests` (API SQL) and the permission theory in `TabletRestrictionSnapshotTests` | `RestrictedProductVisibility.VisibleToRep` (`Catalogue/RestrictedProductVisibility.cs:14`) over in-memory `IQueryable`s. Cases: no group; active and held; active and not held; archived and held; missing group; empty permission set; null permissions | EF translation of `Contains`/`Any` to SQL; SQL null semantics; consistency with the snapshot's serializable transaction | S |
| UT-3 | Web `MainContactReplacementTests` (27), `ContactIntegrationTests` (26), `MainContactCharacterisationTests` (2): Directory SQL collection | `Location.LinkContact`, `EnsureFirstActiveMain`, `SetMain` (stale expected Main, replacement confirmation), `RetireMain`, `UnlinkContact` (`Directory/Location.cs:86–128`); `Contact.SetStatus`, including the inactive-Main guard and legacy roster repair (`Directory/Contact.cs:47–59`) | The database guard that independently enforces one Main; rowversion and serializable locking in `ContactStore`; 400/409 mapping; page flows. **Needs** `InternalsVisibleTo("FieldSales.Api.UnitTests")` (these members are `internal`) | M |
| UT-4 | `TabletSyncFailureTests` (27 tests, about 0.3 s each on SQLite) and the wording checks in `NeedsAttentionTests` | `SyncMessages.Explain` (`src/FieldSales.Tablet.Core/TabletSync.cs:58`), a WI-040 D1–D6 decision table over the public `SyncOutcome` and `UploadSummary` records | Whether a real sync produces those outcomes: stop classification from HTTP failures, upload-before-download order, stored state after a failure | S |
| UT-5 | Migration failures that otherwise surface only in SQL suites: per-test API catalogs, Web class fixtures, and Admin `DevelopmentMigrationSqlServerTests` (cadence 3) | `Database.HasPendingModelChanges()` for `CatalogueDbContext` and `OrderingDbContext` (Api.UnitTests), `StaffWebDbContext` (Web.UnitTests), and `ApplicationDbContext`, `ConfigurationDbContext` and `PersistedGrantDbContext` (Admin.UnitTests), following `DirectoryModelTests`. The Duende contexts need store options in a service provider, as `DevelopmentSeederTests` shows | Migration SQL failing on SQL Server; adoption scripts; trigger and constraint behaviour; data and down migrations | S–M |
| UT-6 | Web `GeographyIntegrationTests` (18), `TownCoordinatesIntegrationTests` (13), `ReferenceListIntegrationTests` (33); API `ProductReferencePersistenceTests` | `GeographyNames.Validate`, `GeographyEntity.Rename` (normalised name), `Town.SetCoordinates` change detection and error translation (`Directory/GeographyEntities.cs:14–62`); `NamedReferenceItem.Rename`, `Archive` and `Unarchive` (`Catalogue/NamedReferenceItem.cs:16–23`). `NameRules` boundaries are already unit-tested | Unique names per parent (SQL indexes and collation); in-use checks before archive (`GeographyUsageReader`, `ReferenceUsageReader`); `CK_Towns_Coordinates`. Geography members are `internal`: same `InternalsVisibleTo` as UT-3 | S |
| UT-7 | API `AssignmentHistoryPersistenceTests` (9); Web `AssignmentHistoryIntegrationTests` (13) | `AssignmentHistory.Capture` guards: empty ids, unchanged owner, reason length and control characters, UTC normalisation, names resolved through `HistoryStaffSnapshot.Find` with `CoverageIdentityUnavailableException` (`Coverage/AssignmentHistory.cs:32–76`) | History written in the same transaction as the ownership change; history queries and ordering; SQL length constraints | S |

The best value is UT-3 and UT-1, followed by UT-5: they bring rules that today fail only after a
container start into the edit loop. UT-4 is cheaper but gains less, because Tablet.Core already
joins the inner loop.

## 8. Speed-ups to existing tests

Estimates come from the measurements above. None removes a test or an assertion, and none touches
schema-readiness, signing-certificate, Data Protection, antiforgery or authorization checks.

| # | Change | Evidence | Estimated saving | Effort |
|---|---|---|---|---|
| S-1 | Run Docker-free selections at default xUnit parallelism; keep `sqlserver.runsettings` for SQL selections. The 2-thread cap guards SQL memory, but today it also throttles projects that never start a container | Tablet.Core 27.4 → 8.8 s; Admin Docker-free 47.1 → 19.6 s; Web Docker-free 6.6 → 4.8 s | **About 48 s per work-item run** locally. CI's supporting lane on a 4-vCPU runner would gain less (not measured) | S (part of item 4) |
| S-2 | API: give the eight per-test-catalog classes one migrated catalog per class, with transactional resets between tests, reusing the `8702bf4` pattern. Keep at least one fresh-catalog migration and `HasPendingModelChanges` test per schema, and give the "application restarts" tests a fresh host on the same catalog | 54.5 s per-test set-up in a 98 s warm run (107 s of 206 s cold); shared-schema classes pay 0.12 s per test | **About 40 s warm, about 90 s cold** per API run (local and CI `api` lane) | L |
| S-3 | Web: split `DirectorySqlServerCollection` into Directory and Coverage collections (a third container), with 3 threads for this project | Directory collection 214 s critical path = Directory 59 s + Coverage 135 s busy | **About 70 s per Web run** (224 → about 155 s, estimated). Risk: a third SQL Server container on the 16 GB CI runner; confirm with one CI run | S–M |
| S-4 | Set Duende `KeyManagement.InitializationSynchronizationDelay` to zero in the test hosts only, *after* confirming it is the cause. Keys are still generated, stored and used to sign. Add a test pinning the production default | Five Admin classes lose about 5 s each (section 4.4) | About 12 s of Admin's 47 s Docker-free phase under CI settings. About 0–5 s at default parallelism, where the waits overlap other work. Up to about 35 s in the serial SQL collection **if** the Development hosts pay it too (unverified) | S |
| S-5 | Remove the two real waits in unit tests: make the Eircode lookup bound injectable (option or `TimeProvider`), and set the retry delay to zero in `SafeRead_RetainsRetrySupport`, as `StaffWebsiteFactory` already does. The retry-count assertion stays | Api.UnitTests 6.8 s and Web.UnitTests 4.4 s, against under 2 s without the waits | About 3 s per unit-only run. **Only about 0.6 s in the proposed inner loop**, where Tablet.Core (8.8 s) is the critical path, so it is worth doing only alongside S-6 or for unit-only runs | S |
| S-6 | Split `TabletStorageScenarios` (36 sequential tests) into two or three classes | It is the Tablet.Core critical path (7.3 s); the next-longest classes are about 4.7 s | About 3 s per inner-loop run (9.4 → about 6.5 s tests) | S–M |

**Not worth doing:**

- **Sharing databases in the Identity migration and adoption classes (85 s).** Each test must start
  from pristine databases; sharing would weaken the schema-readiness and adoption checks. Run them in
  cadence 3 instead.
- **Relaxing SQLite durability settings in Tablet tests.** It would cut most of Tablet.Core's cost,
  but it would weaken the durability checks.
- **Narrowing CI's unit filter so it stops loading integration assemblies.** It saves 1.5 s
  (8.2 → 6.7 s); it comes free with item 1 for local use.
- **Sharing Admin hosts across more classes.** That part is 19.6 s at default parallelism, and the
  isolation risk outweighs the gain.
- **DemoData (14.8 s, half of it container start) and TabletSpike (15.6 s).** These are cadence 3
  only, and the gain is too small.

## 9. Remediation items

Effort: S = up to 2 h, M = about half a day, L = 1–2 days. "Saved" is per run of the affected tier.

| # | Item | Effort | Time saved | Depends on |
|---|---|---|---|---|
| 1 | Add `FieldSales.Fast.slnx`: the four unit projects, Tablet.Components, Tablet.Core and CrashHost. This is the inner-loop command | S | Inner loop of about 16–17 s, replacing between-commit runs of SQL-backed projects (API project 98–206 s) and of Tablet.Core under CI settings (27 s) | – |
| 2 | Tag the 68 SQL Server classes `Requires=SqlServer`, and the 195 untagged integration classes `Category=Integration` (L-3, L-4) | S–M | Docker-free selection (about 50 s) becomes possible; without Docker, runs fail clearly instead of failing 68 classes | – |
| 3 | Tag SQL behaviour classes `Area=Catalogue\|Directory\|Coverage\|TabletSync\|Identity` | S | Work-item SQL takes 0.5–3 min per area instead of about 5.5 min for all of them | 2 |
| 4 | Add `scripts/test.ps1 -Tier Fast\|WorkItem\|SqlServer\|All [-Area …]` with S-1's parallelism rule and a Docker pre-check for SQL tiers. Update README § Testing, `plan_docs/.agent-notes/README.md` and `docs/test-performance.md` (drift). `AGENTS.md` doesn't exist: create it only if you want one | M | S-1: about 48 s per work-item run. Also stops plain `dotnet test FieldSales.slnx` starting too many containers (documented crash) | 1–3 |
| 5 | Move the 5 pure Admin classes (79 tests) into `Identity.Admin.UnitTests`, and `SnapshotAttributeContractTests` (2) into `Tablet.Core.UnitTests`. The V10 compatibility test can stay with its compiled V10 sources | S | 81 tests gain inner-loop and CI unit-job feedback, at about 0.2 s cost | – |
| 6 | UT-3 main-contact rules (+ `InternalsVisibleTo` for Api.UnitTests) | M | Feedback for these rules moves from the Web Directory SQL run (about 1.5 min) to the inner loop | – |
| 7 | UT-1 upload payload faults (+ small visibility change) | M | Moves from the API SQL project run (98–206 s) to the inner loop | 6 (same `InternalsVisibleTo`) |
| 8 | UT-5 pending-model-change checks for six contexts | S–M | Missing migrations caught in the inner loop instead of in SQL suites or CI | – |
| 9 | UT-2 restricted product visibility truth table | S | Moves from the API SQL run to the inner loop | – |
| 10 | UT-6 geography and reference-name entity rules | S | Moves from Web Directory/Catalogue SQL runs to the inner loop | 6 |
| 11 | UT-7 ownership-history capture guards | S | Moves from the API and Web Coverage SQL runs to the inner loop | – |
| 12 | UT-4 sync result wording table | S | About 0.3 s per case and isolated from storage; small gain because Tablet.Core is already in the inner loop | – |
| 13 | S-2 API shared catalog per class with resets | L | About 40 s warm, about 90 s cold per API run (local and CI `api` lane) | – |
| 14 | S-3 split the Web Directory SQL collection | S–M | About 70 s per Web run (estimate; confirm CI memory) | – |
| 15 | S-1 for CI's supporting lane (no thread cap on Docker-free projects) | S | Not measured on CI; locally Tablet.Core 27.4 → 8.8 s | 2 |
| 16 | S-4 confirm the Duende 5 s cause with one before/after run, then zero the delay in test hosts only | S | About 12 s Admin under CI settings; up to about 35 s more if the Development hosts pay it too | – |
| 17 | S-6 split `TabletStorageScenarios` | S–M | About 3 s per inner-loop run | 1 |
| 18 | S-5 remove the two real waits in unit tests | S | About 3 s per unit-only run; about 0.6 s in the inner loop unless 17 is done too | – |

Suggested order: 1, 5, 2, 3, 4 (the tiers), then 6–11 (unit tests), then 13 and 14 (the largest
CI savings). Items 15–18 are optional.

### Stage 2 progress

**Item 1: done** on branch `perf/test-feedback-loop`. `FieldSales.Fast.slnx` holds the four unit
projects, Tablet.Components, Tablet.Core and its CrashHost. Measured with Docker blocked, two rounds,
build included after a timestamp-only touch of an API or Tablet.Core source file:

| Command | Tests | Round 1 (s) | Round 2 (s) |
|---|---:|---:|---:|
| Before: `dotnet test FieldSales.slnx --filter "Category=Unit"` (after API touch) | 676 | 13.8 | 12.2 |
| Before: Tablet.Core with CI settings (after Tablet.Core touch) | 237 | 12.0 | 10.8 |
| **After: `dotnet test FieldSales.Fast.slnx`**, after API touch | 996 | 13.8 | 23.8 |
| After: same, after Tablet.Core touch | 996 | 11.8 | 20.4 |
| After: same, nothing changed | 996 | 14.5 | 20.2 |

The fast solution runs both "before" selections in one command. It took 11.8–14.5 s in round 1,
against 23–26 s for the two separate commands. Round 2 was slower because of Tablet.Core alone:
that assembly took 8 s in round 1 and 10–14 s in round 2, under about 41% background CPU load. The
main gain is what the command leaves out. It cannot start a SQL Server container, so the API SQL
project (98–206 s) and other SQL-backed classes drop out of the edit loop. CI is unchanged.

**Items 2–4: done.**

- Traits now on every integration class: 188 `Category=Integration`, 68 `Requires=SqlServer` and 62
  `Area` (Catalogue 20, Coverage 22, Directory 11, Identity 6, TabletSync 3).
- The two Tablet.Core JSON contract classes are tagged `Category=Unit` where they stand, because they
  compile the V10 sources in their project; CI's unit job now runs them.
- The five pure Admin classes stay untagged until item 5 moves them.
- `scripts/test.ps1` runs the Fast, WorkItem (`-Area`), SqlServer and All tiers. Docker-free selections
  run at default parallelism; SQL selections use CI's settings.

Checks:

| Check | Result |
|---|---|
| `-Tier WorkItem` with Docker blocked | Passed, with the same Docker-free counts as section 4.2 (API 3, Admin 772, Web 126), so every SQL class is tagged |
| `-Tier SqlServer` | Selected exactly the 966 SQL tests; all passed |
| `-Area TabletSync,Identity` | Added 67 API and 18 Admin SQL tests (92 s) |
| `-Area` without Docker | Stops in under 1 s with a "Docker is not running" message |

The afternoon runs were about 2.4 times slower per test than the morning runs, on the same build.
The machine is a laptop with the Balanced power plan, and another IDE was using about 1.3 cores, so
the comparisons below are back to back:

| Docker-free selection | CI settings (s) | Default parallelism (s) |
|---|---:|---:|
| Admin, 772 tests | 138.2 | 55.2 |
| Tablet.Core, 234 tests | 25.2 | 14.4 |

Under these conditions, `-Tier WorkItem` without areas took 79–81 s, and `-Tier SqlServer` took
983 s (the API 186 s, Admin 142 s, Web 628 s, DemoData 27 s).

**Item 5: done.** The five pure Admin classes (79 tests) moved with `git mv` into
`Identity.Admin.UnitTests`, in matching folders and namespaces, tagged `Category=Unit`. The Admin
unit project went from 256 to 335 tests, and the Admin Docker-free integration selection from 772 to
693. All the moved tests now run in the inner loop and CI's unit job. The Tablet.Core contract
classes were handled under items 2–4.

## 10. Not measured, and why

- **CI wall-clock.** No workflow was dispatched; that would be an outward action, and it was not
  asked for. The lane figures above are local, sequential and on a 32-core machine. GitHub's 4-vCPU
  runners will be slower, and parallelism savings will be smaller there.
- **The mocked-Docker Python checks (`tests/demo-data-script.test.py`).** They errored on this
  Windows machine: all 5 tests failed with a missing `calls` file, because the mock `docker` is a
  bash script with a shebang and Windows `PATH` lookup won't run it. CI runs them on Ubuntu. They
  were neither measured nor passed here.
- **Cause of the 5 s Admin waits.** Strong evidence points to Duende key management (section 4.4).
  Proving it needs a code change, which Stage 1 does not allow.
- **Rebuild cost of a public-signature edit.** Only timestamp touches were measured. Per-project
  compile cost (1.0–5.7 s) is given as a bound.
- **Run-to-run variance.** Single runs, except the API project (twice: 206 s cold, 98 s warm). Admin
  and Web matched the 28 October baseline within about 4%.
- **E2E / Journey tier.** It does not exist, so there was nothing to measure. The tablet MAUI app
  (`FieldSales.Tablet.slnx`) and device checks are outside the .NET test tiers and were not run.

## Appendix A: cadence per test class

The time column is the class span from the first pass under CI settings. API classes use the warm
re-run. Cadence `1*` means the class should move to its unit project (L-1, L-2). A suffix such as
`2 (Coverage)` means the class runs at the end of a work item when that area is touched, and in CI
otherwise.

#### FieldSales.Tablet.Core.UnitTests

| Class | Tests | Time (s) | Needs | Cadence | Regression it guards that faster tests do not |
|---|---:|---:|---|---|---|
| SyncNoticesTests | 9 | 0.10 | none | 1 | - |
| EntryHandOffTests | 7 | 0.10 | none | 1 | - |

#### FieldSales.Identity.Admin.UnitTests

| Class | Tests | Time (s) | Needs | Cadence | Regression it guards that faster tests do not |
|---|---:|---:|---|---|---|
| LogoutPageTests | 4 | 0.24 | none | 1 | - |
| RolesPageModelTests | 6 | 0.21 | none | 1 | - |
| UsersDetailsPageModelTests | 20 | 0.21 | none | 1 | - |
| ClientEditorPageModelCharacterisationTests | 2 | 0.20 | none | 1 | - |
| ApiResourceEditorPageModelTests | 27 | 0.19 | none | 1 | - |
| BootstrapFailureTests | 1 | 0.17 | none | 1 | - |
| ApiScopeEditorPageModelTests | 10 | 0.16 | none | 1 | - |
| ResetPasswordPageModelTests | 4 | 0.16 | none | 1 | - |
| StartupHelpersTests | 10 | 0.16 | none | 1 | - |
| ClientsClonePageModelTests | 4 | 0.15 | none | 1 | - |
| GrantsIndexPageModelTests | 4 | 0.15 | none | 1 | - |
| IdentityResourceEditorPageModelTests | 6 | 0.15 | none | 1 | - |
| ApiScopesIndexPageModelTests | 8 | 0.14 | none | 1 | - |
| UsersIndexPageModelTests | 6 | 0.14 | none | 1 | - |
| SecretRevealPresentationTests | 7 | 0.14 | none | 1 | - |
| ClientsIndexPageModelTests | 5 | 0.12 | none | 1 | - |
| ApisIndexPageModelTests | 5 | 0.11 | none | 1 | - |
| IdentityResourcesIndexPageModelTests | 4 | 0.11 | none | 1 | - |
| AdminServicesDependencyDirectionTests | 2 | 0.08 | none | 1 | - |
| TokenLifetimeConfigurationTests | 4 | 0.08 | none | 1 | - |
| ApiResourceBasicsValidationErrorsTests | 8 | 0.08 | none | 1 | - |
| ValidationErrorDictionaryTests | 9 | 0.08 | none | 1 | - |
| PaginationRouteTests | 8 | 0.08 | none | 1 | - |
| SecretSecurityContextTests | 6 | 0.08 | none | 1 | - |
| ClientsDetailsPageModelTests | 12 | 0.07 | none | 1 | - |
| ListResultTests | 12 | 0.07 | none | 1 | - |
| LayoutPresentationStateTests | 15 | 0.07 | none | 1 | - |
| FormPresentationTests | 9 | 0.07 | none | 1 | - |
| ClientEndpointsTests | 13 | 0.07 | none | 1 | - |
| RequiredConfigurationTests | 4 | 0.06 | none | 1 | - |
| UriValidationHelperTests | 6 | 0.06 | none | 1 | - |
| PaginationAndSearchHardeningTests | 8 | 0.06 | none | 1 | - |
| AuditLogsIndexPageModelTests | 3 | 0.04 | none | 1 | - |
| AuditOperationTests | 4 | 0.01 | none | 1 | - |

#### FieldSales.Web.UnitTests

| Class | Tests | Time (s) | Needs | Cadence | Regression it guards that faster tests do not |
|---|---:|---:|---|---|---|
| CatalogueHttpContractTests | 24 | 2.83 | none | 1 | - |
| RestrictionPermissionCohortClientTests | 75 | 0.20 | none | 1 | - |
| AssignmentTransferClientTests | 24 | 0.17 | none | 1 | - |
| LocationCoverageClientTests | 40 | 0.16 | none | 1 | - |
| ProductPriceClientTests | 5 | 0.11 | none | 1 | - |
| RepTerritoryClientTests | 10 | 0.11 | none | 1 | - |
| PermissionRemovalReviewTests | 20 | 0.06 | none | 1 | - |
| CurrentStaffRolesClientTests | 2 | 0.05 | none | 1 | - |
| StaffCookieEventsTests | 1 | 0.05 | none | 1 | - |
| StaffContractTests | 7 | 0.05 | none | 1 | - |
| FormPresentationTests | 4 | 0.05 | none | 1 | - |
| StaffAreaServiceTests | 7 | 0.04 | none | 1 | - |

#### FieldSales.Api.UnitTests

| Class | Tests | Time (s) | Needs | Cadence | Regression it guards that faster tests do not |
|---|---:|---:|---|---|---|
| EircodeLookupTests | 20 | 5.32 | none | 1 | - |
| DirectoryModelTests | 1 | 0.89 | none | 1 | - |
| TabletOrderContractTests | 3 | 0.14 | none | 1 | - |
| CategoryProductCountsTests | 3 | 0.13 | none | 1 | - |
| StaffDirectoryClientTests | 12 | 0.13 | none | 1 | - |
| AssignmentImpactPreviewTests | 2 | 0.13 | none | 1 | - |
| EffectiveOwnerResolverTests | 19 | 0.12 | none | 1 | - |
| AssignmentTransferPlannerTests | 12 | 0.12 | none | 1 | - |
| QuantityTests | 40 | 0.11 | none | 1 | - |
| ProductPriceHistoryTests | 6 | 0.11 | none | 1 | - |
| ProductTests | 11 | 0.11 | none | 1 | - |
| ProductClassificationCharacterisationTests | 4 | 0.10 | none | 1 | - |
| GeographyCsvTests | 21 | 0.10 | none | 1 | - |
| CategoryTreeTests | 4 | 0.09 | none | 1 | - |
| ProductAttributeCharacterisationTests | 2 | 0.09 | none | 1 | - |
| CoordinatesTests | 7 | 0.08 | none | 1 | - |
| ValidationRulesTests | 6 | 0.08 | none | 1 | - |
| RestrictionPermissionRuleTests | 12 | 0.08 | none | 1 | - |

#### FieldSales.Tablet.Core.IntegrationTests

| Class | Tests | Time (s) | Needs | Cadence | Regression it guards that faster tests do not |
|---|---:|---:|---|---|---|
| TabletStorageScenarios | 36 | 12.72 | SQLite files | 1 | Real SQLite (WAL, synchronous=FULL, no pooling) and per-rep workspace files: durable writes, atomic snapshot swap, retention/purge, crash recovery (CrashHost process), upload queue persistence. |
| TabletSyncFailureTests | 27 | 6.85 | SQLite files | 1 | Real SQLite (WAL, synchronous=FULL, no pooling) and per-rep workspace files: durable writes, atomic snapshot swap, retention/purge, crash recovery (CrashHost process), upload queue persistence. |
| TabletUploadTests | 20 | 6.20 | SQLite files | 1 | Real SQLite (WAL, synchronous=FULL, no pooling) and per-rep workspace files: durable writes, atomic snapshot swap, retention/purge, crash recovery (CrashHost process), upload queue persistence. |
| ProductAttributeStorageTests | 14 | 3.98 | SQLite files | 1 | Real SQLite (WAL, synchronous=FULL, no pooling) and per-rep workspace files: durable writes, atomic snapshot swap, retention/purge, crash recovery (CrashHost process), upload queue persistence. |
| OrderModelTests | 17 | 3.50 | SQLite files | 1 | Real SQLite (WAL, synchronous=FULL, no pooling) and per-rep workspace files: durable writes, atomic snapshot swap, retention/purge, crash recovery (CrashHost process), upload queue persistence. |
| OrderReadyTests | 7 | 2.17 | SQLite files | 1 | Real SQLite (WAL, synchronous=FULL, no pooling) and per-rep workspace files: durable writes, atomic snapshot swap, retention/purge, crash recovery (CrashHost process), upload queue persistence. |
| TabletAppSessionScenarios | 12 | 2.00 | SQLite files | 1 | Real SQLite (WAL, synchronous=FULL, no pooling) and per-rep workspace files: durable writes, atomic snapshot swap, retention/purge, crash recovery (CrashHost process), upload queue persistence. |
| NeedsAttentionTests | 19 | 1.90 | SQLite files | 1 | Real SQLite (WAL, synchronous=FULL, no pooling) and per-rep workspace files: durable writes, atomic snapshot swap, retention/purge, crash recovery (CrashHost process), upload queue persistence. |
| LocationContactStatusTests | 12 | 1.88 | SQLite files | 1 | Real SQLite (WAL, synchronous=FULL, no pooling) and per-rep workspace files: durable writes, atomic snapshot swap, retention/purge, crash recovery (CrashHost process), upload queue persistence. |
| LocationSearchTests | 13 | 1.73 | SQLite files | 1 | Real SQLite (WAL, synchronous=FULL, no pooling) and per-rep workspace files: durable writes, atomic snapshot swap, retention/purge, crash recovery (CrashHost process), upload queue persistence. |
| TabletSessionScenarios | 13 | 1.56 | SQLite files | 1 | Real SQLite (WAL, synchronous=FULL, no pooling) and per-rep workspace files: durable writes, atomic snapshot swap, retention/purge, crash recovery (CrashHost process), upload queue persistence. |
| RestrictedTabletTests | 5 | 1.48 | SQLite files | 1 | Real SQLite (WAL, synchronous=FULL, no pooling) and per-rep workspace files: durable writes, atomic snapshot swap, retention/purge, crash recovery (CrashHost process), upload queue persistence. |
| LocationDetailTests | 4 | 1.22 | SQLite files | 1 | Real SQLite (WAL, synchronous=FULL, no pooling) and per-rep workspace files: durable writes, atomic snapshot swap, retention/purge, crash recovery (CrashHost process), upload queue persistence. |
| TabletSignOutScenarios | 13 | 1.07 | SQLite files | 1 | Real SQLite (WAL, synchronous=FULL, no pooling) and per-rep workspace files: durable writes, atomic snapshot swap, retention/purge, crash recovery (CrashHost process), upload queue persistence. |
| TabletSignOutCharacterizationTests | 4 | 0.82 | SQLite files | 1 | Real SQLite (WAL, synchronous=FULL, no pooling) and per-rep workspace files: durable writes, atomic snapshot swap, retention/purge, crash recovery (CrashHost process), upload queue persistence. |
| TabletSyncResumeTests | 5 | 0.67 | SQLite files | 1 | Real SQLite (WAL, synchronous=FULL, no pooling) and per-rep workspace files: durable writes, atomic snapshot swap, retention/purge, crash recovery (CrashHost process), upload queue persistence. |
| LocationReadCharacterizationTests | 2 | 0.53 | SQLite files | 1 | Real SQLite (WAL, synchronous=FULL, no pooling) and per-rep workspace files: durable writes, atomic snapshot swap, retention/purge, crash recovery (CrashHost process), upload queue persistence. |
| OrderCaptureTests | 5 | 0.19 | SQLite files | 1 | Real SQLite (WAL, synchronous=FULL, no pooling) and per-rep workspace files: durable writes, atomic snapshot swap, retention/purge, crash recovery (CrashHost process), upload queue persistence. |
| TabletSignInFlowTests | 6 | 0.09 | SQLite files | 1 | Real SQLite (WAL, synchronous=FULL, no pooling) and per-rep workspace files: durable writes, atomic snapshot swap, retention/purge, crash recovery (CrashHost process), upload queue persistence. |
| SnapshotAttributeContractTests | 2 | 0.03 | none | 1* | pure JSON contract test in an integration project; move to the unit project |
| SnapshotAttributeCompatibilityTests | 1 | 0.02 | none | 1* | pure JSON contract test; may stay with the V10 sources it compiles (item 5) |

#### FieldSales.TabletSpike.IntegrationTests

| Class | Tests | Time (s) | Needs | Cadence | Regression it guards that faster tests do not |
|---|---:|---:|---|---|---|
| StorageScenarios | 26 | 13.97 | files, processes | 3 | Spike tooling under tools/ (export format, device storage experiments, child processes); no production code path. |
| ReportScenarios | 7 | 4.17 | files, processes | 3 | Spike tooling under tools/ (export format, device storage experiments, child processes); no production code path. |
| LocalOpenScenarios | 8 | 0.97 | files, processes | 3 | Spike tooling under tools/ (export format, device storage experiments, child processes); no production code path. |

#### FieldSales.SnapshotSpike.IntegrationTests

| Class | Tests | Time (s) | Needs | Cadence | Regression it guards that faster tests do not |
|---|---:|---:|---|---|---|
| SnapshotExportTests | 22 | 0.82 | files | 3 | Spike tooling under tools/ (export format, device storage experiments, child processes); no production code path. |

#### FieldSales.Tablet.Components.IntegrationTests

| Class | Tests | Time (s) | Needs | Cadence | Regression it guards that faster tests do not |
|---|---:|---:|---|---|---|
| TabletSessionScreenTests | 46 | 0.14 | none | 1 | - |
| LocationDetailComponentTests | 12 | 0.10 | none | 1 | - |
| SignOutControlTests | 11 | 0.07 | none | 1 | - |
| NewOrderComponentTests | 14 | 0.07 | none | 1 | - |

#### FieldSales.Api.IntegrationTests

| Class | Tests | Time (s) | Needs | Cadence | Regression it guards that faster tests do not |
|---|---:|---:|---|---|---|
| TerritoryAssignmentPersistenceTests | 22 | 12.02 | SQL Server | 2 (Coverage) | Real SQL Server: migrations applied per catalog, unique/filtered indexes, triggers and check constraints, serializable reads, EF query translation, HTTP endpoints over SQL. |
| ProductSearchApiTests | 13 | 10.28 | SQL Server | 2 (Catalogue) | Real SQL Server: migrations applied per catalog, unique/filtered indexes, triggers and check constraints, serializable reads, EF query translation, HTTP endpoints over SQL. |
| ProductClassificationApiTests | 12 | 9.91 | SQL Server | 2 (Catalogue) | Real SQL Server: migrations applied per catalog, unique/filtered indexes, triggers and check constraints, serializable reads, EF query translation, HTTP endpoints over SQL. |
| TabletSnapshotApiTests | 34 | 8.10 | SQL Server + host | 2 (Tablet sync) | Real API + SQL for the tablet contract: snapshot built from SQL in serializable transactions, idempotent upload storage, restriction filtering in SQL, and (end-to-end) the real tablet store syncing against the API. |
| ProductAttributeApiTests | 9 | 7.18 | SQL Server | 2 (Catalogue) | Real SQL Server: migrations applied per catalog, unique/filtered indexes, triggers and check constraints, serializable reads, EF query translation, HTTP endpoints over SQL. |
| AssignmentHistoryPersistenceTests | 9 | 5.75 | SQL Server | 2 (Coverage) | Real SQL Server: migrations applied per catalog, unique/filtered indexes, triggers and check constraints, serializable reads, EF query translation, HTTP endpoints over SQL. |
| LocationPositionPersistenceTests | 12 | 5.74 | SQL Server | 2 (Directory) | Real SQL Server: migrations applied per catalog, unique/filtered indexes, triggers and check constraints, serializable reads, EF query translation, HTTP endpoints over SQL. |
| ProductPriceApiTests | 6 | 5.05 | SQL Server | 2 (Catalogue) | Real SQL Server: migrations applied per catalog, unique/filtered indexes, triggers and check constraints, serializable reads, EF query translation, HTTP endpoints over SQL. |
| RestrictionPermissionPersistenceTests | 10 | 4.57 | SQL Server | 2 (Coverage) | Real SQL Server: migrations applied per catalog, unique/filtered indexes, triggers and check constraints, serializable reads, EF query translation, HTTP endpoints over SQL. |
| TabletUploadApiTests | 29 | 4.22 | SQL Server + host | 2 (Tablet sync) | Real API + SQL for the tablet contract: snapshot built from SQL in serializable transactions, idempotent upload storage, restriction filtering in SQL, and (end-to-end) the real tablet store syncing against the API. |
| ProductReferencePersistenceTests | 6 | 3.04 | SQL Server | 3 | Reference-data migration upgrade and rollback on real SQL Server. |
| TabletUploadEndToEndTests | 4 | 2.80 | SQL Server + host | 2 (Tablet sync) | Only end-to-end check that the real tablet store uploads and downloads against the real API over SQL (removal/archive -> review -> Sent/Pending). |
| CataloguePersistenceTests | 2 | 2.03 | SQL Server | 2 (Catalogue) | Real SQL Server: migrations applied per catalog, unique/filtered indexes, triggers and check constraints, serializable reads, EF query translation, HTTP endpoints over SQL. |
| ProductPersistenceTests | 4 | 1.76 | SQL Server | 2 (Catalogue) | Real SQL Server: migrations applied per catalog, unique/filtered indexes, triggers and check constraints, serializable reads, EF query translation, HTTP endpoints over SQL. |
| RestrictedProductVisibilityTests | 4 | 1.76 | SQL Server | 2 (Catalogue) | Real SQL Server: migrations applied per catalog, unique/filtered indexes, triggers and check constraints, serializable reads, EF query translation, HTTP endpoints over SQL. |
| BrandPersistenceTests | 3 | 1.42 | SQL Server | 2 (Catalogue) | Real SQL Server: migrations applied per catalog, unique/filtered indexes, triggers and check constraints, serializable reads, EF query translation, HTTP endpoints over SQL. |
| StaffSessionAuthorizationTests | 3 | 1.23 | host (no Docker) | 2 | Real JWT bearer pipeline: MapInboundClaims=false, role claim type 'role', audience and scope policy. Only Docker-free check of API role mapping. |
| ReferenceBatchTests | 3 | 0.51 | SQL Server | 2 (Catalogue) | Real SQL Server: migrations applied per catalog, unique/filtered indexes, triggers and check constraints, serializable reads, EF query translation, HTTP endpoints over SQL. |
| CatalogueTransactionTests | 1 | 0.48 | SQL Server | 2 (Catalogue) | Real SQL Server: migrations applied per catalog, unique/filtered indexes, triggers and check constraints, serializable reads, EF query translation, HTTP endpoints over SQL. |

#### FieldSales.Identity.Admin.IntegrationTests

| Class | Tests | Time (s) | Needs | Cadence | Regression it guards that faster tests do not |
|---|---:|---:|---|---|---|
| TemplateAdoptionBaselineTests | 5 | 58.95 | SQL Server | 3 | Identity migrations and template adoption against real SQL Server: fresh Development migrate, adoption script, schema-readiness gate, pinned-template host start and seeded role claim. |
| DevelopmentMigrationSqlServerTests | 2 | 25.96 | SQL Server | 3 | Identity migrations and template adoption against real SQL Server: fresh Development migrate, adoption script, schema-readiness gate, pinned-template host start and seeded role claim. |
| Task02SqlServerIntegrationTests | 7 | 8.60 | SQL Server | 2 (Identity) | Real SQL Server semantics in the Identity stores: transactions, serialised concurrent writes, and JWT-protected staff directory and role lookup endpoints over SQL-backed users. |
| AdminLayoutNavigationTests | 7 | 5.62 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| AdminSmokeTests | 9 | 5.38 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| WebComponentLoadingTests | 58 | 5.35 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| StylesheetSeparationTests | 7 | 5.31 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| DiagnosticsServiceTests | 4 | 5.31 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| UsersDetailsIntegrationTests | 24 | 4.66 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| MigrationAdoptionSqlServerTests | 4 | 3.63 | SQL Server | 3 | Identity migrations and template adoption against real SQL Server: fresh Development migrate, adoption script, schema-readiness gate, pinned-template host start and seeded role claim. |
| ApisEditorIntegrationTests | 14 | 3.60 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| ApiScopesIndexIntegrationTests | 12 | 3.27 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| DiagnosticsIndexIntegrationTests | 7 | 2.75 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| ClientsDetailsIntegrationTests | 13 | 2.41 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| UsersIndexIntegrationTests | 12 | 2.38 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| DevelopmentSeederTests | 6 | 2.22 | none (network/files) | 2 | Seeder failure path against an unreachable SQL Server connection. |
| ClientEditorTabsIntegrationTests | 10 | 2.20 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| UserDetailsServiceTests | 44 | 2.18 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| TabletClientRegistrationTests | 5 | 2.16 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| ClientsSecretsIntegrationTests | 11 | 2.08 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| ClientsAuthenticationIntegrationTests | 12 | 2.05 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| ValidationMessageAssociationTests | 8 | 1.93 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| ClientsIndexIntegrationTests | 8 | 1.83 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| ClientsPermissionsIntegrationTests | 8 | 1.76 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| IdentityResourcesCreateIntegrationTests | 7 | 1.72 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| ApiScopesEditIntegrationTests | 8 | 1.63 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| DestructiveHandlerCharacterisationTests | 8 | 1.61 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| ClientsTokenSettingsIntegrationTests | 8 | 1.53 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| ClaimEditorBindingTests | 8 | 1.50 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| SeededIdentityResourceProtectionTests | 6 | 1.47 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| IdentityResourcesEditIntegrationTests | 6 | 1.44 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| ApisIndexIntegrationTests | 8 | 1.42 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| ClientsCreateIntegrationTests | 6 | 1.36 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| ClientsBasicsIntegrationTests | 4 | 1.31 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| UsersCreateIntegrationTests | 6 | 1.21 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| StaffDirectoryEndpointTests | 3 | 1.20 | SQL Server | 2 (Identity) | Real SQL Server semantics in the Identity stores: transactions, serialised concurrent writes, and JWT-protected staff directory and role lookup endpoints over SQL-backed users. |
| ApiScopesCreateIntegrationTests | 6 | 1.10 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| AdminConsoleOptionsTests | 2 | 1.09 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| StaffRoleLookupEndpointTests | 1 | 0.74 | SQL Server | 2 (Identity) | Real SQL Server semantics in the Identity stores: transactions, serialised concurrent writes, and JWT-protected staff directory and role lookup endpoints over SQL-backed users. |
| UserAuditCoverageTests | 12 | 0.73 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| RolesIndexIntegrationTests | 4 | 0.67 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| AuditLogsIndexIntegrationTests | 4 | 0.60 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| GrantsIndexIntegrationTests | 3 | 0.52 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| IdentityResourcesIndexIntegrationTests | 3 | 0.46 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| KeysIndexIntegrationTests | 2 | 0.42 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| SigningCredentialStartupTests | 2 | 0.37 | SQLite host | 2 | Signing-credential startup rules in the real host (certificate required outside Development). |
| UserCreateServiceTests | 4 | 0.37 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| AdminWebFactoryTests | 3 | 0.35 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| ClientValidationTests | 11 | 0.35 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| ClientCreateServiceTests | 8 | 0.35 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| DatabaseSchemaReadinessTests | 3 | 0.35 | SQL Server | 3 | Identity migrations and template adoption against real SQL Server: fresh Development migrate, adoption script, schema-readiness gate, pinned-template host start and seeded role claim. |
| OutputEncodingIntegrationTests | 2 | 0.34 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| AdminAuthorizationIntegrationTests | 12 | 0.32 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| AuditWriterTests | 17 | 0.31 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| AntiforgeryTests | 3 | 0.28 | SQLite host | 2 | Antiforgery enforced on every admin POST through the real pipeline. |
| ClientAuthenticationServiceTests | 8 | 0.28 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| AdminBootstrapTests | 2 | 0.28 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| ApiScopeAuditCoverageTests | 6 | 0.26 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| ClientServiceRegistrationTests | 2 | 0.25 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| StaffLoginFailureTests | 3 | 0.24 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| ApiResourceValidationTests | 10 | 0.22 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| ClientAuditCoverageTests | 8 | 0.22 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| IdentityResourceListServiceTests | 5 | 0.20 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| ClientListServiceTests | 12 | 0.19 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| FallbackAuthorizationTests | 5 | 0.18 | SQLite host | 2 | Fallback SysAdmin policy applied to every admin endpoint in the real host. |
| IdentityResourceAuditCoverageTests | 8 | 0.18 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| Pkcs12CertificateLoaderTests | 2 | 0.17 | none (network/files) | 2 | Loads real PKCS#12 files from disk with the production key-storage flags. |
| ClientTokenSettingsServiceTests | 7 | 0.17 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| TransactionHelperSqlServerTests | 3 | 0.17 | SQL Server | 2 (Identity) | Real SQL Server semantics in the Identity stores: transactions, serialised concurrent writes, and JWT-protected staff directory and role lookup endpoints over SQL-backed users. |
| AuditLogListServiceTests | 5 | 0.16 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| UserListServiceTests | 6 | 0.16 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| TemplateAdoptionAdminAuthorizationTests | 4 | 0.16 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| ApiResourceEditorServiceTests | 22 | 0.15 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| TokenCleanupTests | 2 | 0.14 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| ClientPermissionsServiceTests | 9 | 0.14 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| GrantListServiceTests | 9 | 0.14 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| ApiScopeEditorServiceTests | 12 | 0.13 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| ApiScopeListServiceTests | 12 | 0.12 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| ClientSecretClockTests | 2 | 0.12 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| GrantAuditCoverageTests | 5 | 0.12 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| IdentityResourceEditorServiceTests | 17 | 0.12 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| SecretRevealServiceTests | 7 | 0.12 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| ClientDetailsServiceTests | 11 | 0.12 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| ClientSecretsServiceTests | 10 | 0.12 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| ConfigurationConcurrencyTests | 2 | 0.11 | SQL Server | 2 (Identity) | Real SQL Server semantics in the Identity stores: transactions, serialised concurrent writes, and JWT-protected staff directory and role lookup endpoints over SQL-backed users. |
| ApiResourceAuditCoverageTests | 8 | 0.11 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| ClientsCreatePageModelTests | 3 | 0.11 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| CookieHardeningTests | 4 | 0.11 | SQLite host | 2 | Real cookie options emitted by the Identity host (Secure, HttpOnly, SameSite post-config). |
| ApiResourceListServiceTests | 9 | 0.10 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| SecretRevealSqlServerIntegrationTests | 2 | 0.10 | SQL Server | 2 (Identity) | Real SQL Server semantics in the Identity stores: transactions, serialised concurrent writes, and JWT-protected staff directory and role lookup endpoints over SQL-backed users. |
| EfIdentityUserAdministrationStoreTests | 3 | 0.09 | SQLite host | 2 | Real Identity host on SQLite: Razor rendering and model binding, antiforgery, SysAdmin fallback authorization, cookie hardening, DI registration, EF mappings and audit writes through real services. |
| ReservedClaimTypePolicyTests | 37 | 0.05 | none | 1* | pure unit test in the integration project; move to the unit project |
| RoleServiceTests | 11 | 0.04 | none | 1* | pure unit test in the integration project; move to the unit project |
| AdminCreationPolicyTests | 4 | 0.02 | none | 1* | pure unit test in the integration project; move to the unit project |
| PageModelAuditCoverageTests | 3 | 0.02 | none | 1* | pure unit test in the integration project; move to the unit project |
| ConcurrentTestStartTests | 3 | 0.02 | none (network/files) | 2 | Self-test of the ConcurrentTestStart helper used by the SQL concurrency tests. |
| BuiltInIdentityResourcePolicyTests | 24 | 0.01 | none | 1* | pure unit test in the integration project; move to the unit project |

#### FieldSales.DemoData.IntegrationTests

| Class | Tests | Time (s) | Needs | Cadence | Regression it guards that faster tests do not |
|---|---:|---:|---|---|---|
| DemoSeedTests | 4 | 6.24 | SQL Server | 3 | Demo seed tool against real SQL Server (sample and scale runs). |

#### FieldSales.Web.IntegrationTests

| Class | Tests | Time (s) | Needs | Cadence | Regression it guards that faster tests do not |
|---|---:|---:|---|---|---|
| LocationCoverageActionIntegrationTests | 71 | 19.53 | SQL Server + 2 hosts | 2 (Coverage) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| PermissionRemovalCohortPageTests | 33 | 18.43 | SQL Server + 2 hosts | 2 (Coverage) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| CoverageMutationIntegrationTests | 32 | 17.22 | SQL Server + 2 hosts | 2 (Coverage) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| ContactIntegrationTests | 26 | 17.03 | SQL Server + 2 hosts | 2 (Directory) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| ReferenceListIntegrationTests | 33 | 12.59 | SQL Server + 2 hosts | 2 (Catalogue) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| PermissionRemovalCohortTests | 27 | 12.29 | SQL Server + 2 hosts | 2 (Coverage) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| CustomerIntegrationTests | 39 | 9.99 | SQL Server + 2 hosts | 2 (Directory) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| PermissionRemovalReviewPageTests | 19 | 9.98 | SQL Server + 2 hosts | 2 (Coverage) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| MainContactReplacementTests | 27 | 9.97 | SQL Server + 2 hosts | 2 (Directory) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| RestrictionPermissionCohortTests | 19 | 8.45 | SQL Server + 2 hosts | 2 (Coverage) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| AssignmentTransferIntegrationTests | 32 | 6.54 | SQL Server + 2 hosts | 2 (Coverage) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| AssignmentImpactIntegrationTests | 35 | 6.50 | SQL Server + 2 hosts | 2 (Coverage) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| BrandIntegrationTests | 24 | 6.29 | SQL Server + 2 hosts | 2 (Catalogue) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| DirectoryTypeIntegrationTests | 31 | 5.95 | SQL Server + 2 hosts | 2 (Directory) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| AssignmentPullIntegrationTests | 23 | 5.04 | SQL Server + 2 hosts | 2 (Coverage) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| GeographyRetirementIntegrationTests | 24 | 4.92 | SQL Server + 2 hosts | 2 (Directory) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| UnassignedCoverageIntegrationTests | 21 | 4.71 | SQL Server + 2 hosts | 2 (Coverage) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| RestrictionPermissionIntegrationTests | 9 | 4.17 | SQL Server + 2 hosts | 2 (Coverage) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| LocationCoordinateIntegrationTests | 16 | 4.16 | SQL Server + 2 hosts | 2 (Directory) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| ProductUnitIntegrationTests | 25 | 4.09 | SQL Server + 2 hosts | 2 (Catalogue) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| ProductAttributeIntegrationTests | 11 | 3.93 | SQL Server + 2 hosts | 2 (Catalogue) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| ProductIntegrationTests | 19 | 3.55 | SQL Server + 2 hosts | 2 (Catalogue) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| RepTerritoryIntegrationTests | 15 | 3.53 | SQL Server + 2 hosts | 2 (Coverage) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| TownCoordinatesIntegrationTests | 13 | 3.29 | SQL Server + 2 hosts | 2 (Directory) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| RestrictionPermissionCohortSafetyTests | 6 | 3.08 | SQL Server + 2 hosts | 2 (Coverage) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| RestrictionPermissionPageTests | 5 | 3.04 | SQL Server + 2 hosts | 2 (Coverage) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| TerritoryAssignmentIntegrationTests | 14 | 3.00 | SQL Server + 2 hosts | 2 (Coverage) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| ProductClassificationIntegrationTests | 9 | 2.69 | SQL Server + 2 hosts | 2 (Catalogue) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| RestrictionPermissionCohortPageTests | 5 | 2.67 | SQL Server + 2 hosts | 2 (Coverage) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| CategoryBrowseIntegrationTests | 11 | 2.52 | SQL Server + 2 hosts | 2 (Catalogue) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| LocationCoverageIntegrationTests | 20 | 2.49 | SQL Server + 2 hosts | 2 (Coverage) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| RestrictionPermissionHandOffTests | 4 | 2.39 | SQL Server + 2 hosts | 2 (Coverage) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| ProductSearchIntegrationTests | 12 | 2.14 | SQL Server + 2 hosts | 2 (Catalogue) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| LocationCoveragePageTests | 11 | 2.12 | host (no Docker) | 2 | Real Web host (cookie auth, antiforgery, authorization, Razor pages, resilience pipeline) against stubbed API handlers, or SQLite ticket/session stores. |
| GeographyIntegrationTests | 18 | 2.11 | SQL Server + 2 hosts | 2 (Directory) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| AssignmentHistoryIntegrationTests | 13 | 1.91 | SQL Server + 2 hosts | 2 (Coverage) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| ProductPricePageTests | 16 | 1.76 | host (no Docker) | 2 | Real Web host (cookie auth, antiforgery, authorization, Razor pages, resilience pipeline) against stubbed API handlers, or SQLite ticket/session stores. |
| CategoryIntegrationTests | 1 | 1.63 | SQL Server + 2 hosts | 2 (Catalogue) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| StaffWebsiteScenarioTests | 17 | 1.53 | host (no Docker) | 2 | Real Web host (cookie auth, antiforgery, authorization, Razor pages, resilience pipeline) against stubbed API handlers, or SQLite ticket/session stores. |
| LocationCoverageActionClientTests | 22 | 1.43 | host (no Docker) | 2 | Real Web host (cookie auth, antiforgery, authorization, Razor pages, resilience pipeline) against stubbed API handlers, or SQLite ticket/session stores. |
| UnassignedCoveragePageTests | 25 | 1.23 | host (no Docker) | 2 | Real Web host (cookie auth, antiforgery, authorization, Razor pages, resilience pipeline) against stubbed API handlers, or SQLite ticket/session stores. |
| ProductPriceIntegrationTests | 1 | 0.96 | SQL Server + 2 hosts | 2 (Catalogue) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| MainContactCharacterisationTests | 2 | 0.86 | SQL Server + 2 hosts | 2 (Directory) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| CategoryPageTests | 6 | 0.70 | host (no Docker) | 2 | Real Web host (cookie auth, antiforgery, authorization, Razor pages, resilience pipeline) against stubbed API handlers, or SQLite ticket/session stores. |
| RetiredTownPreflightIntegrationTests | 2 | 0.62 | SQL Server + 2 hosts | 2 (Directory) | Web pages through real HTTP to a real API host on SQL Server: page/API contract, antiforgery and authorization on writes, SQL constraints, rowversion conflicts and serializable transactions. |
| ProtectedAreasTests | 5 | 0.49 | host (no Docker) | 2 | Real Web host (cookie auth, antiforgery, authorization, Razor pages, resilience pipeline) against stubbed API handlers, or SQLite ticket/session stores. Only check that every staff area rejects anonymous and wrong-role users. |
| LocationCoverageActionPageTests | 4 | 0.48 | host (no Docker) | 2 | Real Web host (cookie auth, antiforgery, authorization, Razor pages, resilience pipeline) against stubbed API handlers, or SQLite ticket/session stores. |
| AssignmentPullPageTests | 4 | 0.37 | host (no Docker) | 2 | Real Web host (cookie auth, antiforgery, authorization, Razor pages, resilience pipeline) against stubbed API handlers, or SQLite ticket/session stores. |
| AssignmentTransferPageTests | 2 | 0.23 | host (no Docker) | 2 | Real Web host (cookie auth, antiforgery, authorization, Razor pages, resilience pipeline) against stubbed API handlers, or SQLite ticket/session stores. |
| SqlTicketStoreTests | 3 | 0.12 | host (no Docker) | 2 | Real Web host (cookie auth, antiforgery, authorization, Razor pages, resilience pipeline) against stubbed API handlers, or SQLite ticket/session stores. |
| TestReadResilienceTests | 1 | 0.04 | host (no Docker) | 2 | Real Web host (cookie auth, antiforgery, authorization, Razor pages, resilience pipeline) against stubbed API handlers, or SQLite ticket/session stores. |
| StaffAreaServicePersistenceTests | 6 | 0.03 | host (no Docker) | 2 | Real Web host (cookie auth, antiforgery, authorization, Razor pages, resilience pipeline) against stubbed API handlers, or SQLite ticket/session stores. |
| FixtureCleanupTests | 4 | 0.02 | host (no Docker) | 2 | Real Web host (cookie auth, antiforgery, authorization, Razor pages, resilience pipeline) against stubbed API handlers, or SQLite ticket/session stores. |
