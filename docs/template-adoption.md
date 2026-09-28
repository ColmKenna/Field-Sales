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
