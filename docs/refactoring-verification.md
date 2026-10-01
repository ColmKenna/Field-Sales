# Razor and EF Core refactoring verification

Implemented on `codex/razor-efcore-refactor`, created from `main` at `5be77e6`. All commits are local. The implementation follows the eight-step plan in [plans/refactoring-plan.md](../plans/refactoring-plan.md).

## Changes and finding coverage

Both populated source reports in `plans` are mapped below. Their recommendations informed the implementation; the approved user plan determined scope and commit boundaries.

| Finding | Related finding | Completed work | Step |
|---|---|---|---|
| CR-001 | Smaller date finding | Product dates use invariant formatting; Irish currency and ISO datetime attributes remain. | 1 |
| CR-002 | — | Protected administrator role constants replace matching literals. | 1 |
| CR-003 | — | Bootstrap checks role-assignment failures before logging success. | 1 |
| CR-004 | R01 | Claim actions explicitly bind the query target and reject blank targets. | 1 |
| CR-010 | R02 | User/client pages route by typed status, with missing resources returning 404. | 1 |
| CR-011 | R08 | AuditOperation owns one failure boundary per invocation and updated target metadata. | 6 |
| CR-012 | R09 | Identity retry delegates return outcomes; helpers retain explicit save/commit ownership. | 7 |
| CR-013 | — | Staff scope, audience, client ID and exact scope matching share a contract. | 4 |
| CR-014 | R03 | Each host shares its ModelState mapping, retaining prefixes and summary errors. | 2 |
| CR-015 | — | Unique-constraint classification is shared within existing catalogue/Identity boundaries. | 5 |
| CR-016 | R09, R12 | Paged-result/query factories and narrow transaction helpers remove repeated plumbing. | 5, 7, 8 |
| CR-020 | R06 | Name/price validation uses authoritative predicates; matching UI lengths use constants. | 2, 8 |
| CR-021 | Smaller breadcrumb finding | Category paths and Razor breadcrumb formatting share helpers. | 2 |
| CR-022 | R10, R11 | Reference usage is batched; validation is structured; registry resolution validates registrations. | 5 |
| CR-023 | — | Reference keys replace matching magic strings. | 5 |
| CR-030 | R04, R05 | Catalogue contracts and HTTP decoding are shared; typed reads preserve missing/authentication/availability distinctions; unsafe writes are never retried. | 3 |
| CR-031 | R07 | Unit normalization, create-only compatibility fallback, previews and category outcomes share helpers. | 2 |
| CR-032 | R13 | Staff landing decisions and typed session probing are shared. | 4 |
| CR-033 | Smaller cookie finding | Staff subject lookup, role replacement diff, named cookie expiry and alert markup are shared. | 2, 4 |
| CR-040 | R14 | Purpose/target-aware reveal presentation and a serializable handle-only TempData key are shared. | 7 |
| CR-041 | — | Secret generation, create/clone persistence, clocks and preset/grant constants are shared. | 6 |
| CR-042 | — | Confirmation matching, validation-tab selection and alerts are shared through composition. | 2 |
| CR-043 | — | Startup configuration/certificate/schema helpers, bootstrap/seeding creation mechanics, pagination delegation and shared-model file organization are complete. | 8 |

## Tested commit boundaries

| Step | Local commit | Validation before commit |
|---|---|---|
| 1 | `f7fa3af` | Identity 1,000; Web 163 passed, including required SQL Server checks. |
| 2 | `847fe28` | API 93; Identity 1,011; Web 169 passed, plus strengthened editor assertions. |
| 3 | `b48f121` | Zero-warning solution build; API 93; Identity 1,011; Web 187 passed. |
| 4 | `059ec53` | Zero-warning solution build; API 93; Identity 1,011; Web 200 passed. |
| 5 | `71d859c` | Zero-warning solution build; API 96; Identity 1,014; Web 201 passed. |
| 6 | `5c44a74` | Zero-warning solution build; final Identity suite 1,020 passed. |
| 7 | `0161b78` | Zero-warning solution build; API 97; Identity 1,030; Web 201 passed; all three SQL transaction-helper regressions passed after the final test addition. |
| 8 | `refactor: consolidate startup helpers and complete verification` | Zero-warning solution build; API 97; Identity 1,046; Web 201 passed, including all required SQL Server checks. |

The final solution run passed **1,344 tests**: API **97**, Identity **1,046**, Web **201**, with **zero failures and zero skips**. The build had zero warnings and zero errors.

The full baseline was **1,226 tests passed**, zero failures or skips. Earlier reports' 143 focused tests were not treated as the baseline.

## Regression coverage

- Claim targets: conflicting URL/form values for both editors/actions, blank query targets and real HTTP binding.
- Outcomes: missing/denied statuses and changed messages preserving routes and destinations.
- Validation: field prefixes, summary errors, name/price boundaries, legacy omitted Unit, stale numeric inputs for Each, previews and rejected writes; second-tab errors reopen the appropriate tab.
- HTTP contracts: JSON shapes/optional fields, payloads, validation/conflict decoding, malformed responses, network failures, cancellation, 404/503 and authentication outcomes; inherited resilience performs exactly one POST/PUT/PATCH/DELETE request.
- Staff access: multiple and space-separated scopes, substring decoys, subject precedence, permitted-area counts, revoked remembered areas, local/external return URLs, session failures and configured cookie expiry/renewal.
- Reference data: registration/unknown-key/source failures, SQL Server duplicate constraints, distinct-product usage counts, archived references, bounded SQL reads and retirement/assignment races.
- Auditing/secrets: one failure event per invocation, original exceptions, loaded metadata, success/denied counts, redaction/persistence fallback, clone conflicts, fake-clock creation/rotation and hashed secrets/expiry.
- Transactions/reveals: real SQL Server rollback and transient timeout retries after save, fresh retry state, unique scope attachments, administrator/secret concurrency, notification timing, one-use reveals, wrong contexts, refresh, restart and concurrent consumption. Cloning now serializes the handle as a string, with a real HTTP redirect/reveal/refresh regression.
- Startup/presentation: required settings and legacy null-only policies, absolute/relative certificate paths, migration/check/Testing branches and scope disposal, bootstrap-versus-seed lookup and failure policies, preserved user fields, pagination route values and output encoding.

## Intentional boundaries and preserved behavior

- EF entities and domain projections remain separate from catalogue transport records; Web operation results remain separate from transport records. Request payloads, JSON property names, routes, authorization policies, configuration keys, database mappings and migration histories are preserved.
- Catalogue and Identity retain separate persistence helpers and unique-constraint detectors. Secret-reveal storage retains its supported SQLite and synthetic duplicate-key test fallbacks. No shared generic repository was introduced.
- Retirement still uses live single-item usage checks inside its serializable transaction. Batch reads run sequentially on the scoped DbContext; optional custom usage providers retain a sequential interface fallback. Actual hosts validate registrations at startup; Testing hosts defer source resolution so failure-injection fixtures can exercise 503 behavior.
- User administration retains pre-transaction lookups where its existing semantics differ; protected-administrator checks retain their transactional position. Transactions retain their isolation levels and caller-owned saves/commits. Notifications remain after commit.
- Identity keeps AuditActorResolver and its multi-database readiness validator. Handled grant-cleanup and notification failures retain distinct audit actions. AuditWriter retains its telemetry fallback and redaction.
- Bootstrap looks up by email; seeding looks up by username. Neither alters an existing user's credentials or roles. Bootstrap logs failures; seeding throws on role/user creation failures. Development seeding's existing best-effort claim/role assignment policy is preserved; tightening it would be a separate behavior change.
- Existing certificate loaders, exception policies and production environment restrictions remain. ServiceDefaults shares path resolution and orchestration without adding an EF dependency. Configuration callers that previously rejected only null explicitly retain that policy.
- Razor helpers use composition. Small page-specific resource guards, reload logic and wording remain local; no PageModel inheritance hierarchy was added. Tiny archive-label expressions and specialized locked-scope markup remain local.
- Shared component models now have named files; SafeMarkupBody's encoding documentation is attached to that type. Source reports and the empty report remain untracked in their original locations.

## Reproducing final verification

Docker with SQL Server container support and the repository's .NET SDK are required. Run from the repository root:

```powershell
dotnet build FieldSales.slnx --no-restore -m:1 -v:q
dotnet test FieldSales.slnx --no-build --no-restore -m:1 --settings sqlserver.runsettings -v:q
```

The runsettings limit test threads to two; serial project execution avoids excessive simultaneous SQL Server containers. Every test still runs. Concurrent earlier runs encountered SQL Server startup failures and one unreproduced category restart failure; passing reruns are recorded in the plan. The final gate passed before the step 8 commit.
