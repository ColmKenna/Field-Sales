# Standing decisions

Human decisions that apply beyond the item that raised them. Each entry names who decided, when,
and where it came up. Don't change an entry without a new human decision; add a dated amendment.

## Placeholders resolved (2026-09-28, developer, during WI-001)

| Placeholder | Value |
|---|---|
| `{{APPLICATION_REPOSITORY}}` | `https://github.com/ColmKenna/Field-Sales`, checked out at `D:\repos\Field-Sales`. The planning docs live in this same repo under `plan_docs/`. |
| `{{TEMPLATE_REVISION}}` | `6395488d1beeeaa57b0825a214a1859c0c045c93` (upstream `main`, 2026-09-07). Evidence is in `docs/template-adoption.md`. |
| `{{INTEGRATION_BRANCH}}` | `main` ("for now"; the developer may revisit this) |
| `{{TEMPLATE_PREFIX}}` | `FieldSales.Identity` (see Naming) |

Still open and owned by later items: `{{TABLET_CLIENT_STACK}}` (MI-01) and the other
`missingInformation` entries in `plan-data.js`. They don't block WI-001.

## Naming

The top level is `FieldSales`, and every project is `FieldSales.<Name>`:

| Before | After |
|---|---|
| `Sales.slnx` | `FieldSales.slnx` |
| `AppHost/AppHost.csproj` | `FieldSales.AppHost/FieldSales.AppHost.csproj` |
| `ServiceDefaults/ServiceDefaults.csproj` | `FieldSales.ServiceDefaults/FieldSales.ServiceDefaults.csproj` |
| `IdentityServerProject/src/IdentityServerProject` | `FieldSales.Identity/src/FieldSales.Identity` |
| `IdentityServerProject.Admin.Services` | `FieldSales.Identity.Admin.Services` |
| `IdentityServerProject.Admin.Tests` | `FieldSales.Identity.Admin.Tests` |

The template rename script handles the `IdentityServerProject*` → `FieldSales.Identity*` part and
names the solution `FieldSales.Identity.slnx`. Renaming the solution to `FieldSales.slnx` and the
AppHost/ServiceDefaults projects is done by hand. Future application projects follow the same
pattern: `FieldSales.Web` (staff web/BFF), `FieldSales.Api`, and so on, with names agreed per card.

## Local ports

The developer wants every project on its own port, so Field Sales can run alongside other
template-based solutions. Block 7200–7299 is reserved for Field Sales:

| Resource | Port |
|---|---|
| Identity host HTTPS / HTTP | 7201 / 7202 |
| Staff web/BFF (reserved) | 7203 |
| Field Sales API (reserved) | 7204 |
| Customer web (reserved) | 7205 |
| Aspire dashboard HTTPS / HTTP | 7210 / 7211 |
| Aspire OTLP HTTPS / HTTP | 7212 / 7213 |
| Aspire resource service HTTPS / HTTP | 7214 / 7215 |

The reserved ports are proposals until the item that adds the project confirms them.

## Delivery console

- Work-item status is stored in `plan_docs/field-sales-delivery/plan-data.js` as a top-level
  `workItemStatus` map, and the console reads it from there (developer's decision, 2026-09-28).
- A status chip clicked in the browser only creates an unsaved draft in localStorage. The draft
  records the file value it was made against, and is dropped once the file changes that item.
  The developer picked "file wins, local drafts".
- Agents update `workItemStatus` directly: set the item to `active` when implementation starts and
  to `done` when it completes. They change nothing else in `plan-data.js`.
- The logic is in `status-store.js`, with tests in `tests/status-store.test.js`.

## Repository

- `plan_docs/` is committed to the application repository (developer's decision, 2026-09-28).
- The template's one-shot rename tooling (`scripts/rename-template.ps1`, `scripts/tests/test-rename.ps1`,
  and the `rename-smoke-test` CI job) is removed once the rename is applied, because it only works
  on an un-renamed tree (developer confirmed, 2026-09-28).
