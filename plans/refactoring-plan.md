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
- [x] **3. Catalogue contracts and HTTP handling** — CR-030; R04–05. Shared response contracts, explicit read outcomes, response decoding, availability/authentication handling, safe-read retries only.
- [x] **4. Staff contracts and navigation** — CR-013, CR-032–033; R13. Constants/scope matching, subject lookup, landing/session helpers, role diff, configured ticket expiry.
- [x] **5. Reference data and pagination** — CR-015–016, CR-022–023; R10–12. Registry/keys, structured validation, unique classification, batch usage, paged-result/query helpers.
- [x] **6. Audit and client-secret services** — CR-011, CR-041; R08. Per-operation audit metadata/failure boundary, shared secret generation, deterministic time, create/clone persistence tails, preset/grant constants.
- [x] **7. Transactions and reveal presentation** — CR-012, CR-016, CR-040; R09, R14. Returned retry outcomes, narrow transaction helpers, Identity error/self-action helpers, purpose/target-aware reveal presentation.
- [x] **8. Startup and final verification** — CR-043 and remaining presentation findings. Configuration/certificate/schema helpers, bootstrap/seeding mechanics, pagination/model organization, constants, full solution validation.

## Acceptance gates

Run affected projects and SQL Server tests after each step; do not commit failed or unverified steps. Required cases include conflicting URL/form targets, status-independent messages, retained input, domain boundaries, serialization, unavailable services, authentication, cancellation, write non-replay, scope matching, landing/expiry, usage counts/query bounds, constraints/concurrency, audit counts/redaction, rollback/retry, notification timing, one-time reveals, and startup environment boundaries. Build and run the full solution at completion.

## Implementation evidence

Each step records its changes, exact validation outcome, and justified boundary decisions here before its commit.

### Step 1 — completed

- Both claim editor actions explicitly bind the target from the query, reject blank targets, and never use a conflicting form target. Kept handler parameter signatures for existing callers while making the binding source explicit.
- User mutations now expose AdminMutationStatus alongside their specialized payloads; missing-user/role outcomes and user/client handlers use statuses rather than message comparisons. Password reset also returns 404 directly for a missing outcome. UserUnlockResult retains its existing specialized typed status.
- Replaced protected-role literals, checked bootstrap assignment failures before success logging, and made product display dates invariant while retaining Irish currency formatting.
- Added eight real HTTP claim-binding cases, fourteen revised-message outcome cases, bootstrap failure logging coverage, password-reset status coverage, and rendered September date assertions. Existing protected-role guard tests remain the regression coverage for protected membership.
- Validation: Identity admin **1,000 passed**, Web **163 passed**, zero failures/skips, including their SQL Server suites. A concurrent focused rebuild hit Windows test-host file locks; reran the complete Identity suite after the earlier run ended, successfully. `git diff --check` passed.

### Step 2 — completed

- Added host-level ModelState extensions preserving existing prefixes and summary errors; migrated catalogue and Identity client/API editor mappings. Typed API basics validation retains its existing dedicated mapping boundary.
- Added shared domain/request name and price predicates, category path formatting, Web unit normalization/preview checks and category creation error mapping. UI annotations retain their distinct field-specific wording; domain validation is authoritative. The omitted Unit fallback remains create-only.
- Shared case-sensitive confirmation matching, validation-tab selection using actual errors, validation-summary and status-alert markup, and category breadcrumbs with the category-page separator. Authentication service errors now select their matching tab.
- Validation: full API **93 passed**, Web **169 passed**, Identity admin **1,011 passed**, zero failures/skips, including SQL Server tests. Final Identity alert markup was rebuilt and verified with its complete suite. Strengthened the existing authentication service-error tab assertion; both editor characterisation tests passed afterward. New tests cover ModelState prefixes/summary, confirmation case/trim, quantity binder-error clearing and retained unrelated input, legacy creation, price/name boundaries, and ancestry. Existing end-to-end preview/rejection tests passed. A parallel solution build encountered a static-assets cache lock; serial builds/tests avoid competing builds of aliased project references.

### Step 3 — completed

- Added FieldSales.Catalogue.Contracts for response and error records without changing JSON property names, optional fields, or request payloads. API projections explicitly map domain attributes and breadcrumbs; EF entities and Web operation results remain separate.
- Centralized validation/conflict decoding and JSON reads. Catalogue pages now distinguish missing (404), unavailable (503), unauthorized (challenge), and forbidden (access denial). Malformed/empty responses and transport failures become unavailable; caller cancellation propagates. Writes retain their operation-specific outcomes, with unreadable error payloads treated as unavailable.
- Staff clients replace the inherited resilience pipeline with one whose retries exclude unsafe methods. The installed resilience package exposes handler removal as experimental; its diagnostic is suppressed only around that one call, with the reason documented there. This avoids retaining a second pipeline that could replay writes.
- Validation: solution build **passed with zero warnings/errors**; full API **93 passed**, Web **187 passed**, Identity admin **1,011 passed**, no failures/skips. Added distinct read/page outcome, malformed success/error, network failure, cancellation-before/during-send, serialization/legacy optional fields, and inherited-pipeline write non-replay tests. Existing payload/validation/conflict tests passed. All SQL Server suites passed. Restore/build required access to the existing NuGet/Aspire cache; elevated restore resolved the restricted-access failures. Checklist encoding normalized to UTF-8.

### Step 4 — completed

- StaffAccess owns the staff API scope, audience, Web client ID, lookup-failure key, exact scope matching, and first-sub lookup. Host policies/configuration use those contracts; Identity administration still uses AuditActorResolver.
- Role replacement returns its changed flag and removed roles, avoiding a second role diff in cookie events. Non-business claims are preserved.
- StaffAreaService owns landing decisions. Home retains local-return URL precedence and permission checks; the staff chooser retains its multi-area presentation. Session probing uses the typed bearer client and reports availability on the chooser.
- Ticket fallback expiry reads named cookie options inside store/renew calls, avoiding singleton/post-configuration dependency cycles and honoring explicit ticket expiry.
- Validation: solution build passed with **zero warnings/errors**; API **93 passed**, Web **200 passed**, Identity **1,011 passed**, including SQL Server suites, zero failures/skips. New tests cover multiple/space-separated scopes and decoys, subject precedence, removed roles, zero/one/multiple areas and revoked remembered areas, external return URL rejection, typed session responses/failure statuses, configured expiry/renewal and the real post-configurer cycle. Five session failure matrix cases passed again after strengthening their assertions. Updated the protected-area test host with the existing StaffApi:BaseUrl setting required by typed-client construction.

### Step 5 — completed

- Added shared reference keys and a scoped store/source registry. Actual hosts validate registrations at startup; Testing hosts retain deferred source checks so the existing failure-injection fixtures can verify fail-closed 503 responses. Registry tests explicitly verify registration rejection.
- Batch usage reads run sequentially on the scoped DbContext. Built-in product sources issue one SQL query per list/source and count distinct product/reference pairs; optional small providers retain a sequential interface fallback. Retirement still performs live single-item reads inside its transaction.
- Reference name validation uses the same domain predicate and structured field errors instead of parsing exception messages. Catalogue SQL unique classification is centralized locally; Identity's public detector is shared with the host secret-reveal adapter, retaining its SQLite and fake DuplicateKeyException fallbacks. Kept the two project dependency boundaries intentionally separate.
- Added ListResult.Page and ListQuery.ForPage, migrating the repeated result construction and matching Index queries. Feature-specific SQL filtering/order and grant/audit filter models stay explicit; pagination route values are preserved.
- Validation: solution build passed with **zero warnings/errors**; complete API **96 passed**, Identity **1,014 passed**, Web **201 passed**, including SQL Server suites. Added real SQL command-bound/count-distinct/archived-reference checks, registry/source failure tests, every-route unknown-key HTTP checks, and pagination normalization/extreme tests. Existing duplicate-constraint and retirement race tests passed. The combined run had one category restart failure that was not reproduced: that case passed in isolation, then the complete Web suite passed without application changes. No failed step was committed.

### Step 6 — completed

- Introduced AuditOperation as a per-invocation failure boundary, with loaded target metadata and no exception-data markers. Migrated editors, user/create services, grants, role mutations and list mutations; each keeps explicit success and denied events. Handled grant-cleanup and logout-notification failures retain their separate actions. AuditWriter retains redaction, cancellation and telemetry/persistence fallback; a custom writer cannot replace the original exception.
- Shared client-secret material generation and create/clone validation/persistence/conflict/audit tails. Injected TimeProvider controls creation, expiration comparisons and reported user/grant timestamps. Preset identifiers and Duende OIDC grant constants preserve their existing values.
- New tests cover failures before/after target loading, reused exception objects, writer failure with cancellation, secret hashing/expiry, and create/clone/rotation timestamps under a fixed clock. Existing audit-count, redaction, persistence fallback and clone-conflict tests remain the regression gate.
- Validation: solution build passed with zero warnings/errors; full Identity admin suite **1,020 passed**, including SQL Server checks, zero failures/skips. The suite was rerun after the final grant/role/list migrations. A duplicate-claim guard regression found during extraction was corrected before the passing gate. No failed step was committed.

### Step 7 — completed

- Identity mutation and reveal delegates return their outcomes directly. Shared Identity retry/transaction helpers clear tracked state per attempt; catalogue helpers remain local to catalogue. Callers retain explicit save/commit/rollback ownership and isolation levels. User adapters keep their original pre-transaction lookups where semantics differ, using the retry-only helper rather than broadening every transaction.
- Protected-administrator and retirement checks stay inside their existing transactions. Scope creation reloads its resource per retry; audit follows the transaction result. Grant deletion returns notification data from the transaction and still sends notifications after commit. Identity error formatting and ordinal self-action matching are shared.
- Shared reveal presentation requires explicit purpose/target and one handle-only TempData key. Clone now stores the serializable string handle rather than a boxed value object; a real HTTP redirect/reveal/refresh regression covers this presentation correction.
- Added real SQL Server tests for rollback after SaveChanges, original exception preservation, transient timeout retry with fresh state, and scope creation without duplicate scopes/attachments/audit events. Existing administrator/secret/retirement race, notification timing, wrong-purpose/target, restart and concurrent-consumption suites passed.
- Validation: zero-warning solution build; full sequential solution run passed API **97**, Identity **1,030**, Web **201**, zero failures/skips. Added final scope-retry coverage afterward and passed all **3** transaction-helper SQL Server tests. Concurrent project execution first encountered SQL Server startup failures; the affected migration case passed in isolation and the full suite passed with serial projects and at most two test threads. The clone assertion was updated to require a string handle and the full HTTP flow passed. No failed step was committed.

### Step 8 — completed

- Moved required-configuration handling and certificate-path resolution into ServiceDefaults, retaining host-specific blank/null checks, error messages, loaders and certificate policies. Shared API/Web schema-startup orchestration through delegates, avoiding an EF dependency in ServiceDefaults. Identity retains its multi-database readiness validator and development-only migration/seeding guards.
- Bootstrap/seeding share role and user creation mechanics. Their email-versus-username lookups, existing-user policy, and log-versus-throw behavior remain explicit. Existing development seeding best-effort claim/role assignment remains an intentional boundary; bootstrap still checks role-assignment failures.
- Consolidated pagination construction and matching validation lengths. Split all 17 shared public types into named files and moved the SafeMarkupBody encoding documentation to its actual type. A source comparison verified that moved declarations and implementations are unchanged. Trivial archive labels and specialized locked-scope markup remain local.
- Added startup environment/scope/configuration/certificate-path, lookup/failure/user-field and pagination-route regressions. All **26** focused startup/configuration/pagination tests passed. Added sqlserver.runsettings to reproduce bounded test concurrency without skipping checks.
- Final validation: `dotnet build FieldSales.slnx --no-restore -m:1 -v:q` passed with **zero warnings/errors**. `dotnet test FieldSales.slnx --no-build --no-restore -m:1 --settings sqlserver.runsettings -v:q` passed API **97**, Identity **1,046**, Web **201** — **1,344 total**, **zero failures/skips**, including all required SQL Server suites. `git diff --check` passed. No migration or appsettings files changed.
- Created [the verification report](../docs/refactoring-verification.md) in docs. It maps all **37** report finding identifiers to completed work or explicit preserved boundaries and records regression coverage, tested commit boundaries and reproduction commands. All eight approved steps are complete; commits remain local and the three pre-existing untracked reports are preserved.
