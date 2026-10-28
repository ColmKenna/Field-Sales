# Test performance and isolation

Updated 28 October 2026. The optimisations below preserve real HTTP/SQL boundaries,
existing assertions, migration tests, and the existing required CI check.

## Implementation milestones

| Commit | Change |
|---|---|
| `49f0700` | Four integration lanes on separate CI runners, plus an aggregate `Build & Test` check |
| `c00ce80` | Zero-delay retry backoff in the Web fixture, retaining retry attempts and dedicated production-policy tests |
| `09eb71a` | Unthrottled download for import cancellation; transfer cancellation retains slow-transfer simulation |
| `1e5e1de` | Class-scoped hosts for read-only admin component/stylesheet checks |
| `8702bf4` | Snapshot API schema migrated once per class, with transactional data resets between cases |

The API, admin and Web projects each have their own integration runner. DemoData,
SnapshotSpike, Tablet.Core and TabletSpike share a supporting lane and run
sequentially within it. All seven projects are assigned exactly once. The existing
`Build & Test` check now waits for both the build/unit/script job and every matrix
lane; failures, cancellations and skipped dependencies do not pass the aggregate.
Each integration lane restores and builds its own selected projects. Test results
are uploaded even when a lane fails.

## Isolation retained

- Web fixtures retain the real retry pipeline, retry count and unsafe-write rules;
  only delay and jitter are changed for integration tests. A new fixture regression
  checks that an unavailable GET is attempted four times. The existing production
  retry and single-write contract checks still run with the production policy.
- Import cancellation still occurs at the actual batch-written hook. Assertions
  continue to check partial progress, cancellation status, unchanged active data
  and unsent work, and a successful subsequent refresh. Slow transfer simulation
  remains in the transfer-cancellation scenario.
- Admin hosts are shared only within the two read-only presentation classes.
  Every request uses a fresh client with cookie handling disabled; authentication
  headers are client-specific. Mutating admin scenarios retain their own fixtures.
- The snapshot API class exclusively owns a uniquely named SQL catalog. Both EF
  schemas are migrated once. Data and role-lookup state reset before and after
  each case, including after assertion failures. The database reset transaction
  clears application tables while retaining migration history, then restores all
  triggers and trusted constraints. The initial schema must have enabled guards
  and no application seed rows; future seeded migrations fail this precondition
  instead of silently losing their reference data.
- Snapshot reset refuses catalogs outside its test-name prefix. Its regression
  verifies empty snapshots after reset, cleared access-failure state, current
  migration history, enabled/trusted SQL guards, and reuse of prior natural keys.
  Dedicated fresh-database migration/adoption tests were not changed.

## Measurements

The following are complete local project elapsed times from TRX timestamps,
including fixture/discovery/teardown work. Both full integration runs were executed
sequentially with the same run settings; the new CI concurrency is not included.

| Integration project | Previous | After changes |
|---|---:|---:|
| FieldSales.Api.IntegrationTests | 3m 43s | 1m 18s |
| FieldSales.Identity.Admin.IntegrationTests | 4m 38s | 2m 44s |
| FieldSales.Web.IntegrationTests | 5m 16s | 3m 52s |
| FieldSales.TabletSpike.IntegrationTests | 37s | 8s |

Summed project elapsed time across all seven integration projects decreased from
**14m 52s to 8m 25s**, about 43% in these recorded runs.
The API count increased from 139 to 140 and Web from 872 to 873 because of the two
new infrastructure regression checks. Every pre-existing case was retained.
Baseline TRX files are in `artifacts/test-debt-review/final/`; new results and
`reconciliation.json` are in `artifacts/test-speed/`.

Focused checks already established these case-time improvements:

| Scenario | Previous case time | New focused case time |
|---|---:|---:|
| Web location page returns unavailable | 14.81 s | 0.05 s |
| Tablet import cancellation | 27.00 s | 1.05 s |

These are individual recorded runs on the same workstation, not statistical
benchmarks. Case durations exclude some fixture work, so project elapsed times
are the better measure for schema/host reuse. Differences between full runs can
also include cache and machine-load effects; not every improvement can be
attributed solely to a single edit.

## Validation and CI limitation

Both full solution selections passed: **663 unit cases and 1,970 integration cases**,
for **2,633 passed with zero failures/skips**. A multiset comparison against the prior
2,631-case results found no missing cases and exactly two additions: retry-attempt
preservation and snapshot reset integrity.

The workflow was parsed with the existing Ubuntu YAML parser, and its matrix was
checked against every integration project. The whole solution builds with zero
warnings/errors. Both snapshot-class SQL runs passed all 21 cases, including reset
integrity. All 65 presentation cases and the existing 24 HTTP contract cases passed.
The 23 JavaScript checks and five mocked-Docker Python checks also passed.

The parallel GitHub Actions workflow has not been remotely dispatched. Its actual
runner wall-clock improvement remains to be measured on the next CI run. Local
full-suite validation is deliberately sequential to permit comparison with the
previous baseline without SQL-server contention between projects.
