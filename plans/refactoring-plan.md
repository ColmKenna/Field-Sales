# Razor, EF Core, and service refactoring

Branch: `codex/razor-efcore-refactor`, based on `main` at `5be77e6`.

The approved plan covers both populated reports in this folder. Each step is a separate commit after its required tests pass. The source reports and empty report are pre-existing untracked files and are not staged automatically. Commits remain local.

## Decisions

- Share catalogue response records in a .NET 10 contracts project while retaining domain entities and Web operation outcomes in their own layers.
- Use typed mutation/read outcomes; unavailable catalogue services return 503, missing resources return 404, authentication failures retain distinct outcomes, and cancellation propagates.
- Staff API writes must not be replayed by inherited HTTP resilience handlers.
- Retain SQL Server schemas, migrations, routes, configuration keys, authorization policies, transaction isolation, and domain invariants.
- Prefer narrow composed helpers over generic repositories and PageModel base classes.

## Baseline

Full baseline started with `dotnet test FieldSales.slnx --no-restore --verbosity quiet`, using the local Docker engine for disposable SQL Server databases. Passed: API 87, Web 163, Identity admin 976; total 1,226, zero failures or skips.

## Steps and report coverage

- [x] **1. Regression coverage and behavior fixes** — CR-001–004, CR-010; R01–02. Query-only claim targets, status-based handler responses, protected-role constants, bootstrap assignment checks, consistent dates.
- [x] **2. Validation and Razor helpers** — CR-014, CR-020–021, CR-031, CR-042; R03, R06–07. ModelState mapping, authoritative validation, unit normalization, category outcomes/breadcrumbs, confirmation/tab/alert helpers.
- [ ] **3. Catalogue contracts and HTTP handling** — CR-030; R04–05. Shared response contracts, explicit read outcomes, response decoding, availability/authentication handling, safe-read retries only.
- [ ] **4. Staff contracts and navigation** — CR-013, CR-032–033; R13. Constants/scope matching, subject lookup, landing/session helpers, role diff, configured ticket expiry.
- [ ] **5. Reference data and pagination** — CR-015–016, CR-022–023; R10–12. Registry/keys, structured validation, unique classification, batch usage, paged-result/query helpers.
- [ ] **6. Audit and client-secret services** — CR-011, CR-041; R08. Per-operation audit metadata/failure boundary, shared secret generation, deterministic time, create/clone persistence tails, preset/grant constants.
- [ ] **7. Transactions and reveal presentation** — CR-012, CR-016, CR-040; R09, R14. Returned retry outcomes, narrow transaction helpers, Identity error/self-action helpers, purpose/target-aware reveal presentation.
- [ ] **8. Startup and final verification** — CR-043 and remaining presentation findings. Configuration/certificate/schema helpers, bootstrap/seeding mechanics, pagination/model organization, constants, full solution validation.

## Acceptance gates

Run affected projects and SQL Server tests after each step; do not commit failed or unverified steps. Required cases include conflicting URL/form targets, status-independent messages, retained input, domain boundaries, serialization, unavailable services, authentication, cancellation, write non-replay, scope matching, landing/expiry, usage counts/query bounds, constraints/concurrency, audit counts/redaction, rollback/retry, notification timing, one-time reveals, and startup environment boundaries. Build and run the full solution at completion.

## Implementation evidence

Each step records its changes, exact validation outcome, and justified boundary decisions here before its commit.

### Step 1 � completed

- Both claim editor actions explicitly bind the target from the query, reject blank targets, and never use a conflicting form target. Kept handler parameter signatures for existing callers while making the binding source explicit.
- User mutations now expose AdminMutationStatus alongside their specialized payloads; missing-user/role outcomes and user/client handlers use statuses rather than message comparisons. Password reset also returns 404 directly for a missing outcome. UserUnlockResult retains its existing specialized typed status.
- Replaced protected-role literals, checked bootstrap assignment failures before success logging, and made product display dates invariant while retaining Irish currency formatting.
- Added eight real HTTP claim-binding cases, fourteen revised-message outcome cases, bootstrap failure logging coverage, password-reset status coverage, and rendered September date assertions. Existing protected-role guard tests remain the regression coverage for protected membership.
- Validation: Identity admin **1,000 passed**, Web **163 passed**, zero failures/skips, including their SQL Server suites. A concurrent focused rebuild hit Windows test-host file locks; reran the complete Identity suite after the earlier run ended, successfully. `git diff --check` passed.

### Step 2 � completed

- Added host-level ModelState extensions preserving existing prefixes and summary errors; migrated catalogue and Identity client/API editor mappings. Typed API basics validation retains its existing dedicated mapping boundary.
- Added shared domain/request name and price predicates, category path formatting, Web unit normalization/preview checks and category creation error mapping. UI annotations retain their distinct field-specific wording; domain validation is authoritative. The omitted Unit fallback remains create-only.
- Shared case-sensitive confirmation matching, validation-tab selection using actual errors, validation-summary and status-alert markup, and category breadcrumbs with the category-page separator. Authentication service errors now select their matching tab.
- Validation: full API **93 passed**, Web **169 passed**, Identity admin **1,011 passed**, zero failures/skips, including SQL Server tests. Final Identity alert markup was rebuilt and verified with its complete suite. Strengthened the existing authentication service-error tab assertion; both editor characterisation tests passed afterward. New tests cover ModelState prefixes/summary, confirmation case/trim, quantity binder-error clearing and retained unrelated input, legacy creation, price/name boundaries, and ancestry. Existing end-to-end preview/rejection tests passed. A parallel solution build encountered a static-assets cache lock; serial builds/tests avoid competing builds of aliased project references.
