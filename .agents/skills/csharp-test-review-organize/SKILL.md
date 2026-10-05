---
name: csharp-test-review-organize
description: Reviews automated tests in an existing C# ASP.NET Core Razor application, classifies each test method into Unit, Integration, E2E, or Ambiguous/Mixed Responsibility based on actual behavior and execution boundaries, and recommends how to organize them.
---

# C# ASP.NET Core Test Review & Organization

## Role

You are an expert .NET/ASP.NET Core software architect and test automation specialist. You analyze automated tests in an existing C# ASP.NET Core Razor application and determine how to organize them into appropriate Unit, Integration, and End-to-End (E2E) categories.

You work strictly from the actual codebase. You never assume that existing project names, folder hierarchies, namespaces, traits, or labels accurately describe the tests.

---

## Operating Modes

This skill operates in two distinct stages:

- **Stage 1 — Analysis & Classification Report (Read-only)**: Audit the test suite, define system boundaries, classify all test methods, run tests to verify execution baselines, reconcile inventories, summarize findings, and propose a concrete reorganization plan (Stage 2). **Make no code or project changes.**
- **Stage 2 — Reorganization & Refactoring (Execution upon approval)**: Only after explicit user approval of the Stage 1 report and plan, execute the reorganization (moving tests, splitting mixed-responsibility tests, adjusting projects/traits, consolidating fixtures) while preserving 100% of test assertions and passing status.

---

# Stage 1 — Analyse the Existing Test Suite

Stage 1 is analysis only, with test execution permitted where useful.
**Do not move files, rename tests, create projects, repair tests, or modify source code, dependencies, project configuration, or test organisation.**

## 1. Discover the Test Structure

Thoroughly inspect the workspace:
- Solution (`.sln`, `.slnx`) and project files (`.csproj`)
- Test projects, test classes, test fixtures, and test methods
- Helpers, mocks, stubs, and fakes (e.g., Moq, NSubstitute, FakeItEasy)
- Application startup (`Program.cs`), service registration, and dependency injection
- `WebApplicationFactory<TEntryPoint>`, `TestServer`, and in-memory test hosts
- Database providers (production provider, SQLite in-memory, EF Core InMemory, local containers)
- Database connections, migrations, schema creation, seeding, transaction rollbacks, and cleanup
- HTTP clients (`HttpClient`, `TestServer.CreateClient()`, flurl, etc.) and their network targets
- Authentication and authorization handlers, mock authentication schemes, claims principals
- Browser automation frameworks (Playwright, Selenium, PuppeteerSharp)
- Test configuration (`appsettings.json`, `.runsettings`, launch profiles), scripts, and CI workflows
- Setup and teardown lifecycles (`IAsyncLifetime`, `[SetUp]`, `[TearDown]`, `[OneTimeSetUp]`, `ClassFixture<T>`, `AssemblyFixture`)

Detect the actual test frameworks and assertions in use:
- Test frameworks: xUnit, NUnit, MSTest
- Mocking libraries: Moq, NSubstitute, FakeItEasy
- Assertion libraries: FluentAssertions, Shouldly, built-in asserts
- ASP.NET Core testing packages: `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.AspNetCore.TestHost`
- UI testing packages: Microsoft.Playwright, Selenium WebDriver

Do not assume any particular technology is present. Read enough production code to understand what the tests exercise. Trace fixtures, helpers, inherited setup, and substituted dependencies.

## 2. Define the System Boundary

Before classifying tests, document the explicit application boundaries used to distinguish Integration from E2E.

Identify and categorize:
- **Razor UI**: Rendered HTML, Razor Pages, tag helpers, view components, client-side assets/scripts
- **Application Services & Backend**: PageModels, API controllers, command/query handlers, domain logic, validation
- **Persistence / Database**: Relational databases, document stores, ORMs (EF Core, Dapper), migrations, connection strings
- **Identity & Auth**: Local ASP.NET Core Identity, cookie auth, external OAuth/OIDC providers, token issuance
- **External Services**: Third-party APIs, payment gateways, email/SMS dispatchers, microservices
- **Background Processing**: Hosted services (`IHostedService`, `BackgroundService`), message queues, scheduled jobs

State which components are considered part of the **system under test (SUT)** and which are **external dependencies**.
Explain any classification assumptions. Apply the same boundary consistently throughout the analysis.

## 3. Classification Taxonomy & Evidence Criteria

Treat frameworks and infrastructure as supporting evidence, not automatic classification rules.

### Unit Test
Exercises a small unit of application behavior in isolation.
- **Typical characteristics**:
  - Directly constructs or invokes the system under test (class under test)
  - Substitutes out-of-unit dependencies with test doubles (mocks, stubs, fakes) where needed
  - Does **not** start the ASP.NET Core hosting environment (`WebApplicationFactory`, Kestrel, `TestServer`)
  - Does **not** execute the ASP.NET Core HTTP request pipeline (middleware, routing, model binding, filters)
  - Does **not** depend on a database (relational DB, SQLite, EF Core InMemory), external service, network I/O, browser, or disk infrastructure
- **Examples**: Isolated tests of services, domain models, validators, mapping logic, utility classes, or Razor PageModels with mocked dependencies.
- *Caveat*: Direct instantiation alone does not guarantee a unit test; check if dependencies are real instances touching state or I/O.

### Integration Test
Verifies that multiple real components collaborate or that an application component interacts correctly with real infrastructure.
- **Typical characteristics**:
  - In-process HTTP requests through `WebApplicationFactory` or `TestServer` exercising ASP.NET Core middleware, routing, filters, authentication schemes, or Razor Page execution
  - Repositories or services interacting with a real or simulated database (distinguish EF Core InMemory, SQLite in-memory, LocalDB, Testcontainers, or live dev database)
  - EF Core query execution, navigation property loading, migration scripts, or concurrency checks
  - Multiple real application services collaborating without mocking the boundary between them
- *Caveats*:
  - Creating a `ServiceCollection` or DI container alone does not establish integration; check what services are resolved and executed.
  - Record the database provider used and document fidelity limitations (e.g. EF Core InMemory lacks relational integrity and SQL translation enforcement compared to SQLite or production engines).

### End-to-End (E2E) Test
Exercises a complete user or external-client workflow through the application's public boundary and across the declared system boundary.
- **Typical characteristics**:
  - Accesses a running application instance through its external public interface (HTTP port, public host)
  - Browser navigation, page rendering, DOM interaction, user input, and form submissions (e.g. Playwright, Selenium)
  - Workflows spanning the rendered UI, backend processing, persistence, and external messaging end-to-end
  - Complete client workflows through a public API with minimal or zero backend test doubles
- *Caveats*:
  - Playwright or Selenium usage alone does not guarantee E2E; inspect whether the browser hits a live application with real services, static mock HTML, or an in-memory server with mock backends.
  - HTTP client calls alone do not establish E2E coverage.
  - Document any substituted components that materially limit the full workflow.

### Ambiguous / Mixed Responsibility
Used when a confident single classification is not justified. Distinguish between:
- **Ambiguous**: Insufficient evidence, unresolved system boundaries, or atypical execution model where the test could be interpreted in multiple ways.
- **Mixed Responsibility**: A single test method or test class combines materially different scopes or independent responsibilities (e.g., asserts fine-grained domain calculations *and* performs multi-step browser UI interactions; or tests HTTP routing *and* performs deep direct DB manipulation outside the endpoint).

For each ambiguous or mixed test:
- State the concrete uncertainty or combined responsibilities
- Identify candidate categories
- Detail what evidence or refactoring would resolve the classification
- Explain whether splitting the test would improve isolation and maintainability

## 4. Run Tests Where Helpful

You may build the solution and run tests when doing so helps:
- Confirm test discovery and test runner compatibility
- Verify execution boundaries and runtime fixture lifecycles
- Resolve classification uncertainty
- Establish a verified baseline of passed/failed/skipped tests

**Execution Safety Controls**:
- Inspect test configuration, `appsettings.*.json`, connection strings, and fixture setup/teardown for side effects before running.
- Use only local test settings and isolated test resources (e.g. ephemeral in-memory databases, local test containers).
- Never execute tests that contact shared, staging, or production environments without explicit approval.
- Do not modify source code, configuration, or dependencies to get tests to run.
- Record:
  - Exact command lines executed (e.g. `dotnet test --no-build ...`)
  - Environment settings and test runners
  - Pass / Fail / Skipped counts and duration
  - Tests that could not run, with the specific missing dependency or environment blocker
  - Root cause of failures (assertion failure vs. environment/configuration failure)
- Passing or failing does not determine classification. Do not fix failing tests during Stage 1.

## 5. Produce the Classification Report

Provide an exhaustive classification table covering every discovered test method:

| Test Project | Test Class | Test / Test Group | Method Count | Classification | Confidence | Evidence | Notes |
| ------------ | ---------- | ----------------- | ------------ | -------------- | ---------- | -------- | ----- |

- **Confidence levels**:
  - **High**: Boundary and dependencies are conclusively proven by code inspection and execution.
  - **Medium**: Likely category supported, but minor configuration or dependency details remain unverified.
  - **Low**: Evidence is conflicting, ambiguous, or incomplete.
- **Evidence requirements**:
  - Cite specific source paths with line references (e.g. `[MyTests.cs:L42-L68](file:///path/to/MyTests.cs#L42-L68)`)
  - State the concrete invocation mechanism, instantiated types, and substituted dependencies.
- **Grouping rules**:
  - Group methods only when their execution boundaries, dependencies, and setup fixtures are identical.
  - For any group, list all included method names and state the exact count.
  - Never group methods with different classifications together.

## 6. Reconcile the Inventory

- Test methods (`[Fact]`, `[Test]`, `[TestMethod]`, `[Theory]`) are the primary counting unit.
- Distinguish test method declarations from parameterized execution test cases (`[InlineData]`, `[TestCase]`).
- Account for all skipped (`Skip = ...`, `[Ignore]`) or conditionally executed tests.
- Verify:
  - Discovered Methods = Unit + Integration + E2E + Ambiguous + Mixed Responsibility
  - Every method appears in exactly one table row or group
  - Sum of grouped counts equals the total method count

## 7. Summarise the Current Test Architecture

Provide clear summary metrics and architectural observations:
- Method counts per classification category (Unit, Integration, E2E, Ambiguous, Mixed Responsibility)
- Parameterized execution case counts where available
- Project breakdown: existing test projects, their stated vs. actual purpose
- Shared infrastructure: base classes, fixtures (`IClassFixture`, `WebApplicationFactory`), test databases, auth stubs
- Misleading labels, traits, namespaces, or folder locations (e.g. tests labeled "Unit" that spin up a database)
- Duplicated infrastructure and consolidation opportunities
- Tests with unnecessarily broad scope or heavy dependencies
- Execution performance and environmental limitations

## 8. Recommend Future Organisation & Stage 2 Plan

Formulate a tailored reorganization strategy:
- Structure options: Dedicated projects (e.g. `*.UnitTests`, `*.IntegrationTests`, `*.E2E`), directory separation within existing projects, or trait-based categorization (`[Trait("Category", "Integration")]`)
- Target destinations for each discovered test class / method
- Shared infrastructure strategy: shared test utility library, base fixtures, database lifecycle management
- Migration plan for ambiguous and mixed-responsibility tests (e.g. refactoring into separate unit and integration tests)
- Build, CI, and local execution strategy (e.g. fast-running unit tests on pre-commit, integration tests on PR, E2E on staging pipeline)
- Verification plan: how to ensure zero loss of test coverage and verify identical pass/fail baseline before and after reorganization

**Stop at the end of Stage 1. Wait for explicit user review and approval before beginning Stage 2.**
