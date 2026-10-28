# Test technical-debt status and handoff

Updated 28 October 2026. This supersedes the earlier suite counts and the blanket claim that every test class has an explicit category. Findings retain their historical IDs.

## Selected remediation

The seven findings selected for this change are implemented across eight logical commits. Final acceptance passed for all selected findings; results are recorded below.

| Finding | Change | Status |
|---|---|---|
| TEST-TD-013 | CI runs locked admin JavaScript checks, Web Node checks, and mocked-Docker Python reset checks | Remediated |
| TEST-TD-016 | Application/per-test host disposal tolerates partial setup; startup failures retain their original exception | Remediated |
| TEST-TD-014 | Catalogue and Directory collections explicitly own and dispose their SQL containers | Remediated |
| TEST-TD-015 | Environment regression checks compare initial values through construction, startup and disposal | Remediated |
| TEST-TD-017 | SQL race coordination has bounded readiness, participant-failure cancellation and observed worker failures | Remediated |
| TEST-TD-011 | Real SQLite/PFX tests moved to integration; six tablet handoff cases moved into a new unit project | Remediated |
| TEST-TD-007 | Web scenarios grouped by feature and shared fixtures extracted into Infrastructure | Remediated |

No production implementation, schema, or public API changes were needed. Existing mutation/security assertions remain in place. No tests were skipped or disabled.

## Current structure and execution

- Four unit projects: API, Web, Identity.Admin, and Tablet.Core.
- Seven integration projects: API, Web, Identity.Admin, DemoData, SnapshotSpike, TabletSpike, and Tablet.Core.
- Tablet.Core.CrashHost is an auxiliary executable, not a test project.
- Web integration scenarios live in Catalogue, Directory, Coverage and Security. Shared application fixtures, test doubles and lifecycle helpers live in Infrastructure. Namespaces and existing type names were preserved, except the intentional split of database-backed StaffAreaService checks into StaffAreaServicePersistenceTests.
- Web Catalogue and Directory collections each own one SQL Server container. Tests within one collection are serialized; the two collections can execute concurrently. Application fixtures keep unique catalogs. HTTP-only tests are not placed in SQL collections.
- The main solution includes the new tablet unit project. The device solution remains separate because it requires platform workloads.
- xUnit and existing centrally managed dependency versions were retained; no assembly-lifetime framework was added.

From the repository root:

```sh
dotnet restore FieldSales.slnx
dotnet build FieldSales.slnx --no-restore
dotnet test FieldSales.slnx --filter "Category=Unit" --no-build
dotnet test FieldSales.slnx --filter "Category!=Unit" --no-build -m:1 --settings sqlserver.runsettings --blame-hang-timeout 2m
node --test tests/Unit/FieldSales.Web/product-unit-form.test.mjs
python3 tests/demo-data-script.test.py
```

From `src/FieldSales.Identity/src/FieldSales.Identity`:

```sh
npm ci --ignore-scripts
npm run test:admin-ui
```

The Python tests require Bash and use a mock Docker executable. On a Windows CRLF checkout, execute them from a Linux LF checkout; the validation run used an ignored LF-normalized copy in Ubuntu. SQL integration validation requires Docker access and successful NuGet restore. `Category!=Unit` intentionally includes untagged integration checks; project names alone do not control CI selection.

## Original remediation verification (before performance work)

Subsequent performance work is documented in [test-performance.md](test-performance.md),
including the parallel CI lanes and updated verification counts. The results below
are retained as the baseline for that work.

- Whole solution build: passed, zero warnings and errors.
- CI unit selection: 663 passed.
- New lifecycle regression cases: four passed; failed Docker startup reports its original failure without secondary null-reference cleanup errors.
- New concurrency coordinator cases: three passed, covering coordinated start, setup failure and readiness timeout.
- Six SQL race cases: passed twice against real SQL Server.
- Environment-preservation checks: three passed with absent settings and three passed with pre-populated settings in a child process.
- Locked npm install and admin JavaScript checks: passed, 18 checks; Web JavaScript: five passed.
- Python reset checks: five passed in Ubuntu from an LF-normalized copy.
- CI integration selection: 1,968 passed, including all 872 Web integration cases.
- Test identity reconciliation: all 2,624 original cases retained (normalizing only the intentional StaffAreaService persistence-class split), plus exactly seven new regression cases; 2,631 passed, zero failures or skips.
- SQL lifecycle observation: two additional SQL containers at peak during the combined Web run; both removed by collection teardown. The pre-existing demo container was left untouched.
- Mechanical reorganisation check: non-import code-line multisets match exactly before and after extraction/moves.

The original review executed 2,624 C# cases: 1,705 passed and 919 were blocked by restricted Docker access. NuGet/SDK resolution was also blocked in that restricted context. Those failures were not established product defects. Later validation used approved Docker/network access. Review TRX files and reconciliation evidence are retained under ignored `artifacts/test-debt-review/`.

## Historical completed findings

| ID | Original remediation | Historical evidence |
|---|---|---|
| TEST-TD-001 | Shared Docker containers replaced per-test startup | PR #14, commit 6ab984e; Web lifetime ownership subsequently addressed by TEST-TD-014 |
| TEST-TD-002 | Split the umbrella project into domain test projects | PR #15, commit d092c80 |
| TEST-TD-003 | Removed process-wide environment mutation from AdminWebFactory | PR #16, commit cb09ae2; regression assertions improved by TEST-TD-015 |
| TEST-TD-004 | Partitioned CI execution to avoid duplicate C# execution | PR #15, commit d092c80 |
| TEST-TD-006 | Introduced category-based selective execution | PR #15, commit d092c80; this does not mean every current class carries an explicit trait |

## Unselected historical backlog

These items are not silently included in this implementation and must be revalidated before further work:

| ID | Historical topic | Current review qualification |
|---|---|---|
| TEST-TD-008 | Task02 fixture/collection naming | Remains a lower-priority naming follow-up |
| TEST-TD-009 | Different method naming styles | Two clear conventions are used; style uniformity alone is not debt |
| TEST-TD-010 | Unused packages/dependency warnings | No .NET dependency audit/removal was selected |
| TEST-TD-012 | Fake category API duplication/drift | Real API/SQL category persistence and rename paths are also tested; no mock-only coverage gap was established |

## Performance follow-up

The subsequent five performance changes are implemented and verified in
[test-performance.md](test-performance.md). This also remediates **TEST-TD-005** for
the two read-only admin presentation groups: measured repeated host startup was
replaced with class-scoped hosts and fresh per-request clients. Migration and
mutating admin scenarios retain their original isolation.

The current suite has **2,633 passing C# cases**, including all previous 2,631 cases
and two new regression checks, plus 23 JavaScript checks and five Python checks.
CI integration projects now run in four independent lanes, while the existing
required `Build & Test` check aggregates every lane and the build/unit/script job.

## Newly discovered candidate

**TEST-TD-NEW-001 — JavaScript transitive dependency audit advisories.** The required locked install completed successfully but `npm audit` reported High advisories for `source-map-js` and `undici`. Neither is a direct dependency. The JSON audit is saved in `artifacts/test-debt-review/npm-audit.json`. Dependency changes were not selected and no automatic audit fix was applied; assess affected versions and upgrade options separately.

## Handoff rules

For subsequent remediation, select finding IDs explicitly, recheck Git status and the relevant behaviour, preserve meaningful assertions, and record commands actually run. Keep new scope separate from selected findings. Do not treat an environment-blocked SQL run as a pass. Preserve unrelated working-tree files and avoid production changes unless separately authorized.
