# Identity template adoption (WI-001)

Field Sales uses [IdentityServerWithAdminTemplate](https://github.com/ColmKenna/IdentityServerWithAdminTemplate)
for sign-in, identity configuration and security administration. This record pins the revision
adopted and what was verified about it before any application change.

## Pinned revision

| | |
|---|---|
| Upstream repository | `https://github.com/ColmKenna/IdentityServerWithAdminTemplate.git` |
| Upstream commit | `6395488d1beeeaa57b0825a214a1859c0c045c93` (`main`, 2026-09-07) |
| Upstream subject | fix(tooling,docs): repair rename-script and README defects found by the release-gate audit |
| Tree | `7890994a1c1830ae58969a05ea1d4be2d16e80ae` |
| Adopted as | `55eed67` "Initial commit" on `main` of `github.com/ColmKenna/Field-Sales` (GitHub *Use this template*, 2026-09-28) |

*Use this template* copies the files without upstream history, so the adoption is identified by
content: the tree of `55eed67` is identical to the tree of upstream `6395488d`. Nothing was changed
between the two.

To re-check: `git rev-parse 55eed67^{tree}` here and `git rev-parse 6395488d^{tree}` in a clone of
the upstream repository both print `7890994a1c1830ae58969a05ea1d4be2d16e80ae`.

## Baseline verification at the pinned revision

Run on 2026-09-28 against the unmodified tree, before renaming, with .NET SDK 10.0.401 and
Docker 29.8.1 (Linux engine) on Windows 11.

| Check | Result |
|---|---|
| `dotnet build Sales.slnx` | Succeeded, 0 warnings, 0 errors |
| `dotnet test Sales.slnx` | 962 passed, 0 failed, 0 skipped (SQL Server Testcontainers suites included) |
| `npm ci` then `npm run test:admin-ui` in the identity host project | 18 passed, 0 failed |

`npm ci` runs a `postinstall` step that re-copies the vendored `ck-responsive-table` and `ck-tabs`
web components into `wwwroot/lib`. The copies differ from the committed files only in line endings.

## Rename

The template was renamed with its own script (prefix `FieldSales.Identity`), and the
application-wide names were then applied by hand: `FieldSales.slnx`, `FieldSales.AppHost` and
`FieldSales.ServiceDefaults`. Migration files were rewritten in place: each matches its
original apart from the namespace prefix and a dropped UTF-8 byte-order mark, so the migration
history and the reviewed bundles are unchanged. The one-shot rename script, its test and its CI
job were then removed.

Local ports use the Field Sales block (7200–7299): identity host `https://localhost:7201`
(HTTP 7202); Aspire dashboard 7210/7211, OTLP 7212/7213 and resource service 7214/7215.

## Client boundary for WI-002

Agreed on 2026-09-28. WI-001 records the contract. **WI-002 implements it**, replacing the
template's sample `razorclient` and `blazorclient` development registrations, which stay in place
until then.

| | |
|---|---|
| Identity host (issuer) | `FieldSales.Identity`, `https://localhost:7201` in Development. It owns discovery, authorize, token and end-session endpoints and the `/Admin` console. Browsers and the BFF call these directly, not through the BFF. |
| Staff web/BFF client | Client id `fieldsales-staff-web`. Confidential; authorization code with PKCE; the client secret comes from an AppHost secret parameter (named in WI-002), never from committed configuration. Hosted by the staff web/BFF project (named in WI-002) on `https://localhost:7203` (reserved). |
| Redirect URIs | Sign-in `https://localhost:7203/signin-oidc`; post-logout `https://localhost:7203/signout-callback-oidc`; front-channel logout `https://localhost:7203/signout-oidc` |
| Scopes requested | `openid`, `profile`, `fieldsales.api`. Whether `roles` and `offline_access` are also requested is decided with the business-role mapping in WI-002. |
| Field Sales API | API resource `fieldsales-api` with scope `fieldsales.api`; access tokens are validated for audience `fieldsales-api`. Hosted by the Field Sales API project (named in WI-002) on `https://localhost:7204` (reserved). |
| Tokens | Access and refresh tokens stay on the server, in the BFF. The browser only holds the BFF's protected session cookie. |
| Registration | In Development, through the host's development seed. In other environments, through **Admin → Clients**, as the template's production checklist requires. |

Security administration and business permissions stay separate: `SysAdmin` governs only the
identity host's `/Admin` console. Field Sales business roles (Field Salesperson, Sales Manager,
Head Office User) are mapped in WI-002, and a Head Office User is not a security administrator.

## Template behaviour at adoption, and what later cards add

| Behaviour | At the adopted revision | Delivered later by |
|---|---|---|
| Security administration | `/Admin` requires `SysAdmin`. A signed-in user without it is redirected to Access Denied, and an anonymous request is redirected to sign-in. A global fallback policy requires authentication. | — |
| Session revocation | `SecurityStampValidatorOptions.ValidationInterval = TimeSpan.Zero` (`Program.cs`), so a revoked identity-host session is rejected on its next request. This covers the identity host's own cookie only. | WI-003: removing a business role takes effect on the Field Sales website's next request |
| Sign-out | The post-logout page renders IdentityServer's `SignOutIFrameUrl`, so registered clients get front-channel logout. | WI-002: sign-out of the shared staff session |
| Administrator persistence | Outside Development, nothing is seeded at startup, and `--bootstrap-admin` refuses to change an existing account. In Development, the seeder creates only *missing* users, so a demoted seeded admin stays demoted after a restart, but a **deleted** `admin@sales.local` is recreated with `SysAdmin` on the next Development start. | Not changed; recorded and characterised by tests |
| Tablet sign-in, local retention and sync recovery | Not part of the template | WI-032 (MI-62 covers disabled accounts) |
| Customer identity and invitation | Not part of the template | WI-194 (MI-03) |
| Staff account recovery delivery | Template credential flow only | MI-03 |
