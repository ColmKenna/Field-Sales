# Checking the work so far

A hands-on walkthrough for verifying what has been built, from a clean checkout to a
signed-in browser session. It covers work items **WI-001 to WI-028** (the delivery console marks
exactly these as done): identity and staff sign-in, the Head Office catalogue, geography,
customers/locations/contacts, and rep coverage up to the Unassigned Locations list.

> [!IMPORTANT]
> **The repository seeds only two accounts, and commits no passwords.**
> Development seeding creates `admin@sales.local` (role `SysAdmin`) and `testuser@sales.local`
> (no role). Their passwords are whatever *you* put in AppHost user secrets (step 1). Nothing seeds
> a Field Salesperson, Sales Manager or Head Office User, so the staff website cannot be exercised
> until you create those accounts (step 3). The staff accounts and the shared password below are a
> **suggested development set to create yourself**, not credentials that already exist.

---

## 0. What you need

| Need | Why |
|---|---|
| .NET 10 SDK (`global.json` pins `10.0.100`+) | Build and run |
| Docker running | Aspire starts a SQL Server container; the integration tests use Testcontainers |
| Node 18+ (optional) | The JavaScript test suites |
| A browser that trusts the ASP.NET dev certificate | Services run on `https://localhost` — run `dotnet dev-certs https --trust` once |

> README paths such as `cd FieldSales.AppHost` predate the move into `src/`. Use the paths below.

## 1. Set secrets and start everything

```sh
cd src/FieldSales.AppHost
dotnet user-secrets set "Parameters:sql-password"            "YourStrong@SA!Password"
dotnet user-secrets set "Parameters:staff-web-client-secret" "a-random-secret-for-the-staff-web-client"
dotnet user-secrets set "Parameters:seed-sysadmin-password"  "SysAdminPass123!"
dotnet user-secrets set "Parameters:seed-test-user-password" "TestUserPass123!"
cd ../..
dotnet run --project src/FieldSales.AppHost
```

The two `seed-*` values above are the README's examples; any password of 8+ characters with
upper, lower, digit and symbol works. **Whatever you set is the password for the account in the
table below.** AppHost refuses to start if any of the four secrets is missing.

In Development all migrations are applied automatically on first start. Wait until the Aspire
Dashboard (URL printed in the console) shows `identityserver`, `staff-api` and `staff-web` as
running.

| Service | URL |
|---|---|
| Identity host + `/Admin` console | https://localhost:7201 |
| Staff website | https://localhost:7203 |
| Staff API | https://localhost:7204 |

## 2. Accounts

### Seeded automatically (Development only)

| Username | Role | Password |
|---|---|---|
| `admin@sales.local` | `SysAdmin` | the `seed-sysadmin-password` you set (`SysAdminPass123!` if you copied the example) |
| `testuser@sales.local` | none | the `seed-test-user-password` you set (`TestUserPass123!` if you copied the example) |

### Suggested sample staff — you create these in step 3

Use one shared **development-only** password for all of them: `FieldSales#2026` (meets the policy:
8+ characters, upper, lower, digit, symbol). Use the email as both **Username** and **Email**, to
match how the seeded accounts are named.

| Username / Email | Full name | Business role(s) | Purpose |
|---|---|---|---|
| `headoffice@sales.local` | Hazel Head-Office | Head Office User | Catalogue, geography, customers, coverage (all reps) |
| `manager1@sales.local` | Maeve Manager | Sales Manager | Manages Colm and Aoife |
| `manager2@sales.local` | Niall Manager | Sales Manager | Manages Brian — proves team scoping |
| `colm@sales.local` | Colm | Field Salesperson | Main rep for coverage scenarios |
| `aoife@sales.local` | Aoife | Field Salesperson | Second rep, for transfers |
| `brian@sales.local` | Brian | Field Salesperson | Rep outside Maeve's team |
| `both@sales.local` | Bea Both | Sales Manager **and** Head Office User | Multi-role: sees the area chooser |

`testuser@sales.local` (no business role) is your "signed in but not permitted" case.

## 3. Provision the staff (checks WI-002, WI-003)

1. Sign in at https://localhost:7201/Admin as `admin@sales.local`.
2. **Admin → Roles → New:** create exactly these three names (case and spacing matter):
   `Field Salesperson`, `Sales Manager`, `Head Office User`.
3. **Admin → Users → New:** create each account from the table above, then open it and assign
   its role(s).
4. Sign out of the admin console.

**Expected:** the admin account lands in the console; `SysAdmin` alone grants **no** staff website
area (confirmed next step).

Other Admin modules worth a glance (these are template features, already covered by 1,000+ tests):
Clients (`fieldsales-staff-web` is registered), Audit Logs (your role/user changes appear),
Diagnostics.

## 4. Staff sign-in and area routing (WI-002, WI-003)

Open https://localhost:7203, use its sign-in button (it redirects to the identity host), and sign in with each account:

| Account | Expected |
|---|---|
| `colm@sales.local` | Lands on **Field sales** (`/Rep`) — placeholder text "Your field sales workspace is ready." |
| `manager1@sales.local` | Lands on **Sales manager** (`/Manager`) with links to Coverage, Review an assignment change, Unassigned Locations |
| `headoffice@sales.local` | Lands on **Catalogue** (`/HeadOffice`) with the full link list |
| `both@sales.local` | **Choose your staff area** page (`/Staff`) listing two areas. Pick one, sign out, sign in again: you go straight to the last-used area |
| `admin@sales.local` | **"That area is unavailable to your account"** — `SysAdmin` is not a business role |
| `testuser@sales.local` | The same unavailable-area page |

**Live role removal (WI-003):** keep `colm@sales.local` signed in on the website. In the admin
console remove his `Field Salesperson` role. His **next** website request should show
**"Access to … has changed"** (and, if he was saving something, "A change you just tried to make was not saved")
rather than the old page. Re-add the role to continue.

**Wrong-area guard:** while signed in as `colm@sales.local`, browse to
https://localhost:7203/HeadOffice and `/Coverage/Territory` — both should be refused.

## 5. Seed sample data through the UI

No sample business data exists, so create the minimum below as `headoffice@sales.local`.
The names match those used in the acceptance evidence under `docs/`.

1. **Manage geography** (WI-014/015): either add manually, or use **Import towns from CSV** with

   ```csv
   Region,County,Town
   Leinster,Wicklow,Rathdrum
   Leinster,Wicklow,Arklow
   Leinster,Wicklow,Laragh
   Leinster,Dublin,Swords
   ```

2. **Manage reference data** (WI-017): add at least one **Location Type** (e.g. `Pharmacy`) and one
   **Contact Type** (e.g. `Owner`). *Nothing is seeded here, and the customer form needs them.*
3. **Manage customers and locations** (WI-016): create customer *Walsh's Shop* with a location in
   Laragh, and *Murphy's Pharmacy* with a location in Rathdrum. Add a few more shops in different
   towns (include Swords) so coverage lists have something to group.
4. **Manage rep reporting lines:** set Colm → Maeve Manager, Aoife → Maeve Manager,
   Brian → Niall Manager.

## 6. Head Office checks, by work item

| WI | Where | What to try — expected result |
|---|---|---|
| WI-004 | HeadOffice → Browse categories | Create a root category, a child, and a grandchild; rename one. Duplicate sibling names are rejected; the same name under a different parent is fine |
| WI-006 | Browse categories | Counts and breadcrumbs show; products appear on their branch |
| WI-005 / 007 | Create product | Create a product from the minimum record; set a unit of measure with a step and a minimum |
| WI-008 | Product detail | Add a dated base price, then a later one; history is kept, not overwritten |
| WI-009 / 010 / 011 | Manage reference data | Brands, Product Profiles, Attributes, Suppliers, Restriction Groups: **archive** rather than delete; archived items disappear from choices but stay on existing records |
| WI-012 | Product detail | Classify with profile, brands, supplier and restriction group |
| WI-013 | Search products | Find by code, name, brand or supplier |
| WI-014 / 015 | Manage geography | Rename keeps identity. A Town in use offers **Archive**, an unused one **Delete**; both show a confirmation you can Cancel. New Location choices exclude archived Towns |
| WI-016 | Customers → create | Customer with several locations in one go |
| WI-018 / 020 | Location / contact pages | Link contacts to a location; each location has exactly one main contact; replacing or retiring the main contact never leaves a gap |
| WI-019 | Create location | Coordinates default from Eircode or Town |

## 7. Coverage checks (WI-021 to WI-028)

Sign in as `manager1@sales.local` (Maeve). Colm, Aoife and Brian are the reps.

1. **Assign territory (WI-021, WI-022):** Coverage → Colm → **Add assignment**. Choose *Wicklow
   (County)*. You should see an **impact preview** before anything saves. **Cancel** and confirm
   nothing changed. Repeat and **confirm**; Colm now owns every Wicklow shop.
2. **Rep territory page (WI-023):** Colm's page lists his assignments at Region/County/Town/Location
   level; expanding Wicklow shows Towns with location counts.
3. **Carve-out:** assign *Rathdrum (Town)* to Aoife. Colm's page marks Rathdrum as carved out to
   Aoife and his Primary count drops by Rathdrum's shops.
4. **Transfer (WI-024):** Colm → **Transfer…** — move some assignments to Aoife; preview, confirm.
5. **Pull (WI-025):** Aoife → **Take over from another rep…** — pull from Colm; preview, confirm.
6. **Who covers a shop (WI-026):** open a Location's **Who covers this shop?** page. It names the
   owner and the *why*, e.g. `Colm (via Laragh)` or `Colm (assigned directly)`, plus captured
   history.
7. **Change coverage from the shop (WI-027):** from that page, assign just this shop to someone
   else. Preview → confirm → source reads `assigned directly`; history records who and when.
8. **Unassigned list (WI-028):** create a shop in a Town nobody covers (Swords), then open
   **Unassigned Locations**. Shops are grouped by Town; each row leads with
   **Assign Swords (Town) to…** and a count, with **Assign just this shop to…** beside it. Assign
   the Town to Colm: every Swords shop leaves the list. When none remain the page reads exactly
   *All Locations have a responsible rep*.
9. **Scoping (WI-021/023):** as `manager2@sales.local` (Niall), Coverage should list only Brian.
   Browsing straight to Colm's territory should be **forbidden**. As `headoffice@sales.local` or
   `both@sales.local` all three reps are editable.
10. **Reporting-line change takes effect immediately:** as Head Office, move Colm to Niall; Maeve
    loses access to his page on her next request.

## 8. Security spot-checks

- Browse to any `/HeadOffice/*` URL while signed out → redirected to the identity sign-in.
- Sign out on the website, press Back → protected pages do not reappear.
- Suspend a staff user in Admin (indefinite lockout) → their next password sign-in is refused.
- Names containing `<b>x</b>` in a customer/shop render as text, not markup.
- Leave a preview open, change the same assignment in another tab, then confirm in the first: the
  stale preview should refresh instead of saving.

## 9. Automated tests

Docker must be running (Testcontainers starts real SQL Server).

```sh
dotnet build FieldSales.slnx -warnaserror
dotnet test  FieldSales.slnx          # last recorded full run: 2,171 / 2,171 passed
```

Projects: `tests/FieldSales.UnitTests`, `tests/FieldSales.Api.Tests`, `tests/FieldSales.Web.Tests`,
`tests/FieldSales.Identity.Admin.Tests`. Run one with
`dotnet test tests/FieldSales.Web.Tests --filter "FullyQualifiedName~Unassigned"`.

JavaScript suites:

```sh
cd src/FieldSales.Identity/src/FieldSales.Identity && npm ci --ignore-scripts && npm run test:admin-ui
cd ../../../.. && node --test plan_docs/field-sales-delivery/tests/status-store.test.js
```

Per-item acceptance evidence (criteria → tests) is in `docs/*-delivery.md` and
`plan_docs/.agent-notes/WI-0NN.md`.

## 10. Not built yet — don't look for it

- The **Field sales** (rep) landing is a placeholder; tablet sync, calls, stock checks and orders
  are later iterations.
- No Location Profile data (E12), no overview count of unassigned shops or manager-area scoping
  for the Unassigned list (E14), no gap lists (E17).
- WI-029 (grant/remove a rep's restriction permission) is next and untouched.
- `plan_docs/iterations/HANDOVER.md` still says "nothing is built"; it is out of date.

## 11. Troubleshooting

| Symptom | Likely cause |
|---|---|
| AppHost exits naming a missing key | One of the four `Parameters:*` secrets is not set |
| Sign-in loops or "invalid client" | `staff-web-client-secret` changed after the first seed; the client keeps the old secret. Reset the Docker volume `fieldsales-identity-sqlserver-data` or update the client secret in Admin → Clients |
| Seeded password rejected | You changed the secret after the first run; the seed only creates a missing user. Reset it at Admin → Users → Reset password (or wipe the volume) |
| Staff user signs in but sees denied | Role names must match exactly: `Field Salesperson`, `Sales Manager`, `Head Office User` |
| Rep missing from Coverage / reporting lines | Account lacks the `Field Salesperson` role, is suspended, or has no manager set |
| Customer form has no Location Type | Add Location/Contact Types under Reference data first |
| Browser certificate warning | `dotnet dev-certs https --trust` |
| Tests fail to connect | Docker is not running |

> This guide was written from the code and delivery docs in the repository. It has not been
> executed end to end: the authoring environment had no .NET SDK. If a step does not behave as
> described, treat the guide as the thing to correct.
