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

## Identity client boundary (2026-09-28, developer, WI-001 Increment 3)

- WI-001 records the contract, and **WI-002 implements it**, replacing the sample
  `razorclient`/`blazorclient` development seeds.
- Staff web/BFF client id `fieldsales-staff-web`: confidential, authorization code with PKCE, and a
  secret from an AppHost parameter. URIs on `https://localhost:7203`: `/signin-oidc`,
  `/signout-callback-oidc`, `/signout-oidc` (front-channel).
- API resource `fieldsales-api`, scope `fieldsales.api`, served on `https://localhost:7204`.
- Still open for WI-002: project names for the web/BFF and API, whether `roles` and
  `offline_access` are requested, the AppHost secret parameter name, and the business-role mapping.
- The full contract is in `docs/template-adoption.md` § Client boundary for WI-002.

## WI-002 staff access contract (2026-09-28, developer, Increment 1)

- One staff website hosts the rep, manager and head-office areas. The new .NET 10 / Aspire projects
  are `FieldSales.Web` (Razor Pages BFF, HTTPS 7203) and `FieldSales.Api` (HTTPS 7204). The
  proposed I-01 sign-in and I-02 area-choice layouts are approved for this item.
- Staff authenticate with the adopted identity host's username/password page. I-01 redirects to
  that page; it does not collect a password in the BFF. A staff member needing recovery contacts a
  `SysAdmin`, who uses the existing identity-admin password-reset page. Customer invitations and
  recovery remain separate.
- The business roles are the exact ASP.NET Identity role names `Field Salesperson`, `Sales Manager`
  and `Head Office User`. `SysAdmin` remains a distinct security-administration role and grants no
  staff area by itself. The BFF requests the `roles` identity scope to receive role claims.
- The BFF requests `offline_access` for server-held refresh tokens. The browser holds only the
  protected BFF session cookie. The AppHost secret parameter is `staff-web-client-secret`. The
  WI-001 client id, API resource, scope and redirect/logout URIs remain as recorded in
  `docs/template-adoption.md`.
- The developer approved the Human-Led scenario gate: IAM-US-001 S1–S8 and IAM-US-002 S1–S2,
  including direct-link denial of protected content and actions. Role removal during an open
  website session is WI-003. The named scenario matrix is in `WI-002.md`.

### Increment 2 (2026-09-28, developer)

- Reuse **Admin → Roles** and **Admin → Users** to provision the three business roles and staff
  accounts; no new provisioning UI or automatic staff-account seed is added.
- An inactive staff account is represented by the existing **Suspend** action, which sets an
  indefinite Identity lockout. The existing **Unlock** action reverses it.
- Development seeding registers `fieldsales-staff-web`, `fieldsales.api` and `fieldsales-api`
  instead of the template sample clients and API. The seeder remains additive: existing sample
  registrations in a persistent Development database require SysAdmin review and removal through
  `/Admin`, rather than automatic deletion of potentially edited records.

### Increment 3 (2026-09-28, developer)

- The BFF persists encrypted OIDC tickets and Data Protection keys in dedicated SQL Server
  `StaffWebDb`; the browser receives an opaque, secure, HttpOnly cookie. Production requires a
  certificate for the website key ring and the `staff-web` migration bundle.
- The first API surface is authenticated `GET /staff/session`. It checks the signed bearer token,
  `fieldsales-api` audience, `fieldsales.api` scope and at least one business role. The BFF calls
  it with a server-held access token and refreshes that token using its server-held refresh token.
- I-01 returns rejected staff credentials to a retry state. Protected landings exist for rep,
  manager and head office; the sign-out POST ends the BFF and identity-host sessions. The
  last-used destination, I-02 and area switching remain in Increment 4.

### Increment 4 (2026-09-28, developer)

- Persist each staff member's last-used area in `StaffWebDb`, keyed by their identity `sub` claim.
  Resolve stored keys only through the known area map and select them only while the principal
  still has the mapped business role. Never take a destination or role from browser state.
- Use I-02 for first use when more than one area is permitted and no usable last-used area exists.
  Show only areas held by the current principal. Single-area users go directly to their area.
  Area navigation switches destinations inside the existing staff session. A valid permitted
  direct link has priority over the saved default; protected request endpoints deny an unheld
  area before protected page content or actions are reached.
- IAM-US-002 S3, immediate role removal during an open session, remains WI-003.

## WI-003 current staff-role boundary (2026-09-29, developer)

- The identity host's current ASP.NET Identity role assignments are authoritative for staff area access. Its bearer-protected `/staff/current-roles` endpoint returns only the caller's own business roles, using the existing `fieldsales-api` audience and `fieldsales.api` scope.
- The staff BFF refreshes business-role claims from that source before each protected request; the API checks the same source for authenticated staff requests. A lookup failure denies access without falling back to stale token or cookie roles. A removed last-used area is cleared while remaining permitted areas stay available in the same session.
- A denied request to an area whose role was removed opens I-05. A denied write says the change was not saved; a denied read does not. I-05 offers only current areas, or sign-out and the existing administrator-contact guidance if none remain. An area never held keeps the generic access-denied page.
- WI-003's protected Head Office POST is a Testing-only representative write. WI-004 owns production catalogue forms and persistence. No administrator contact URL was approved, so the existing guidance supplies the help text without a new destination.

## WI-004 category contract (2026-09-29, developer, plan approval)

- Use SQL Server and EF Core with stable GUID category identities and optional parent identities. Category depth is unlimited, with six levels as a display and verification target. Derive breadcrumbs from the current parent chain on read.
- Category names are unique among siblings; duplicate leaf names under different parents are valid. A rejected or expired create/rename form changes nothing and is not persisted or replayed after sign-in.
- The Human-Led scenario matrix is recorded in `WI-004.md`. Because Product, Order and Call records do not yet exist, WI-004 proves changing descendant category paths and stable category identities; direct assertions over those later record types follow their introduction.
- At the Increment 1 review, the developer approved an API-owned `CatalogueDb` separate from website session storage. The BFF calls its business endpoints with server-held tokens. See `docs/catalogue-category-decisions.md`.
