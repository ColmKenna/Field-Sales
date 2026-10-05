# Manual testing: isolated demo data

Use this guide to test the demo environment through the terminal and browser.
Run the checks in order; later checks deliberately change and remove demo records.
Record the actual result and any screenshots or error messages for each check.

## Before you start

- Run commands from the repository root, containing `FieldSales.slnx`.
- Have Docker running, the project's .NET SDK available, and free disk space for SQL
  Server. Check `docker info` responds before continuing. Earlier live verification
  failed because storage ran out; a SQL creation error or an unresponsive Docker
  daemon is a setup blocker, not a passed test.
- Configure the four AppHost user secrets in [demo setup](demo-data.md#first-run).
  Remember the configured admin and test-user passwords; there are no built-in passwords.
- Use two terminals: **A** keeps AppHost running; **B** runs seed/status commands.
- Stop one AppHost before starting another. Demo and normal development use the
  same website ports. If needed, trust the local .NET HTTPS development certificate.
- Use separate browser profiles for different users, or sign out before switching
  accounts. Close old signed-in windows when switching database profiles or resetting.

## M01 — Record the normal development baseline

1. In terminal A, start normal development:

   ```sh
   dotnet run --project src/FieldSales.AppHost --launch-profile https
   ```

2. Wait for the three apps to be healthy in the Aspire dashboard.
3. Open `https://localhost:7201/Admin` and sign in as `admin@sales.local`, using
   `Parameters:seed-sysadmin-password`.
4. Open **Users**. Record the current accounts, especially any accounts you created
   yourself. If you already have normal business data and a Head Office account,
   record an existing customer or product as well.
5. Confirm the dashboard contains `sqlserver`, with the ordinary database resource names.
6. Stop this AppHost with Ctrl+C.

**Expected:** Normal development is usable. You have a baseline to compare after
demo removal. This check does not require adding or deleting normal data.

## M02 — Start a clean demo without sample business data

If you have used the demo before, stop its AppHost and run
`./scripts/demo-data.sh reset` first. This removes **all demo data**, including
records you added during earlier testing. Exact counts below require a clean demo.

1. In terminal A:

   ```sh
   ./scripts/demo-data.sh start
   ```

2. Wait for Identity, the staff API, and the staff website to be healthy.
3. Confirm the dashboard contains `demo-sqlserver`. Its SQL endpoint uses port
   `14339`. The six physical database names are:

   ```text
   FieldSalesDemo_IdentityDb
   FieldSalesDemo_IdentityConfigDb
   FieldSalesDemo_IdentityOperationalDb
   FieldSalesDemo_StaffWebDb
   FieldSalesDemo_CatalogueDb
   FieldSalesDemo_DirectoryDb
   ```

   The dashboard resource labels remain `IdentityDb`, `CatalogueDb`, etc.; check
   the database name in the resource details rather than expecting renamed labels.

4. In terminal B:

   ```sh
   ./scripts/demo-data.sh status
   ```

5. In a fresh browser session, open the demo admin console and inspect **Users**.

**Expected:** Staff, Catalogue, and Directory report `not seeded`; catalogue,
directory, and coverage counts are zero. The four demo staff accounts report
`not created`. The normal Development seed has created `admin@sales.local` and
`testuser@sales.local`, but no sample business data.

## M03 — Seed the sample and check the baseline counts

1. Keep terminal A running. In terminal B:

   ```sh
   ./scripts/demo-data.sh seed
   ./scripts/demo-data.sh status
   ```

2. Read the output and compare with this table:

   | Item | Expected on a fresh seed |
   | --- | --- |
   | Staff, Catalogue, Directory batch status | `seeded` |
   | Categories | 3 |
   | Products | 4 |
   | Brands, including the archived brand | 3 |
   | Customers | 3 |
   | Locations | 4 |
   | Contacts | 3 |
   | Territory assignments | 3 |
   | Assignment history entries | 3 |

3. Refresh **Users** in the admin console. Confirm the four demo staff accounts
   exist with their roles from M04.

**Expected:** The first seed reports `sample data created` for all three batches,
and status matches the table. No password is printed in the command output.

## M04 — Sign in with the sample accounts

Open `https://localhost:7203` and test each account in a separate signed-out session.
All four demo staff accounts use `Parameters:seed-test-user-password`.

| Account | Expected staff area |
| --- | --- |
| `headoffice@demo.sales.local` | Head office (`/HeadOffice`) |
| `manager@demo.sales.local` | Sales manager (`/Manager`) |
| `aoife@demo.sales.local` | Field sales (`/Rep`) |
| `colm@demo.sales.local` | Field sales (`/Rep`) |

Also check:

1. While signed in as Aoife, browse directly to `https://localhost:7203/HeadOffice`.
   Head-office access should be denied.
2. Sign in to the staff website as `testuser@sales.local`. It should not grant a
   business area because this account has no business roles.
3. Sign in as `admin@sales.local` with the admin password. The identity admin
   console should be available, but SysAdmin alone should not grant a staff area.

**Expected:** Each demo staff account reaches its permitted area. An account
without the required business role cannot open a protected staff area.

## M05 — Browse the connected sample scenario

Sign in as `headoffice@demo.sales.local` and use the links on the Head office page.
Complete this check before making changes in M06–M07.

### Catalogue and geography

1. Open **Browse categories**. Find `Demo Health & Personal Care` with the two
   children `Skin Care` and `Everyday Essentials`.
2. Open **Search products** and search for `DEMO`. Confirm codes `DEMO-001` through
   `DEMO-004` appear.
3. Open `DEMO-001 · Demo Moisturising Cream`. Confirm a current price of €9.25,
   an upcoming price of €9.75, and three history entries (€8.50, €9.25, €9.75).
   Dates are relative to the first seed run. Confirm primary brand `Demo Wicklow
   Care`, alternative brand `Demo Coast Care`, and pack-size attribute `100 ml`.
4. Open `DEMO-003`. Confirm restriction group `Demo Specialist Range`.
5. Open `DEMO-004`. Confirm unit `litre`, quantity step `0.25`, and minimum `0.5`.
6. Open **Manage reference data**, select **Brands**, and use **Show list**.
   `Demo Retired Brand` should be hidden until **Show archived** is checked and
   **Show list** is selected again.
7. Open **Manage geography**. Check `Demo Leinster` → `Wicklow` → `Rathdrum` and
   `Arklow`; `Demo Leinster` → `Dublin` → `Swords`; and `Demo Munster` → `Cork` →
   `Cobh`. Active towns have coordinates. `Demo Archived Town` appears under
   Wicklow only when archived entries are shown.

### Customers, contacts, and coverage

1. Open **Manage customers and locations**. Check:

   | Customer | Locations | Contacts |
   | --- | --- | --- |
   | Demo Valley Pharmacies | Rathdrum Branch, Arklow Branch | Niamh Demo links to both; Liam Demo also links to Rathdrum |
   | Demo Northside Retail | Swords Shop | Sarah Demo |
   | Demo Harbour Stores | Cobh Shop — Unassigned | No contacts |

2. Check each branch's **Main contact** belongs to its active contact roster.
   Arklow has Niamh Demo; Swords has Sarah Demo. Rathdrum has one of its two active
   contacts as Main. The seed does not prescribe which of those two wins first.
3. Open Rathdrum's location details. Its position is town-derived and approximate.
   Cobh deliberately shows **Position needed**, even though the town has coordinates.
4. Use **Who covers this shop?** on each sample location:

   | Location | Expected Primary |
   | --- | --- |
   | Rathdrum Branch | Aoife Demo, via Wicklow |
   | Arklow Branch | Colm Demo, assigned directly |
   | Swords Shop | Colm Demo, via Swords |
   | Cobh Shop — Unassigned | Unassigned |

5. Use **History →**. Each of the three covered locations has one initial ownership
   entry; Cobh has no assignment history yet.
6. Open **Manage rep reporting lines**. Both reps report to `Demo Sales Manager`.
7. Open **Coverage**. Aoife has one Location as Primary; Colm has two. Aoife's
   Wicklow assignment shows the Arklow carve-out to Colm.
8. Open **Unassigned Locations**. Only `Cobh Shop — Unassigned` should appear.
9. Sign in as `manager@demo.sales.local` and open **Coverage**. Both reps should
   be available as members of that manager's team.

**Expected:** The sample can be browsed without missing-reference errors. Coverage
reflects the location override, and the manager can see both reporting reps.

## M06 — Repeat seeding and preserve manual changes

1. Before changing anything, run `./scripts/demo-data.sh seed` again.
   All three batches should say `already seeded; existing data preserved`.
   M03 counts should remain unchanged.
2. Sign in as Head Office. Open **Manage reference data** → **Brands**. Rename
   `Demo Wicklow Care` to `Manual Test Brand`, using **Save name**.
3. Add a new brand named `Manual Added Brand`.
4. Open **Manage customers and locations** → **Create customer**. Enter customer
   name `Manual Demo Customer`, first location `Manual Demo Shop`, and town
   `Cobh` under Cork / Demo Munster. Leave the Eircode blank and save.
5. Bookmark `DEMO-001` and the new location's detail page, or record their URLs.
6. Open **Unassigned Locations**. On the original `Cobh Shop — Unassigned`, choose
   **Assign just this shop to...**, select `Aoife Demo`, and select **Preview
   assignment**. Review the impact, then select **Confirm assignment**.
7. Confirm the original Cobh shop now belongs to Aoife, while `Manual Demo Shop`
   remains unassigned. The original shop's history now has one entry.
8. Run seed and status again, then refresh the browser pages.

**Expected:** All batches are skipped. The renamed brand remains renamed and
appears on `DEMO-001`; the added brand, customer, and location remain. Bookmarks
still open the same records. The assignment to Aoife is preserved.

Counts should now be: **3 categories, 4 products, 4 brands, 4 customers, 5 locations,
3 contacts, 4 assignments, and 4 history entries**. M07 changes the brand count again.

## M07 — A deleted sample does not return

1. As Head Office, open **Manage reference data** → **Brands** and show archived entries.
2. For `Demo Retired Brand`, choose **Un-archive** and confirm it.
3. The now-active, unused brand should offer **Delete**. Select it and confirm deletion.
4. Run `./scripts/demo-data.sh seed` again and refresh the list with archived entries shown.

**Expected:** `Demo Retired Brand` remains absent; it is not recreated by seed.
There are now three brands: `Manual Test Brand`, `Demo Coast Care`, and
`Manual Added Brand`. The four products remain.

## M08 — Restart without reseeding

1. Stop the demo AppHost in terminal A with Ctrl+C.
2. Start it again with `./scripts/demo-data.sh start`; wait for the apps to be healthy.
3. Run **only** `./scripts/demo-data.sh status` in terminal B.
4. Sign in again. Check the renamed brand, added customer/location, changed Cobh
   ownership, and deleted retired brand.

**Expected:** Data and completed seed status survive restart. The business sample
is not applied automatically; M06–M07 changes remain. Existing Development Identity
configuration and admin/test-account seeding still run at startup.

## M09 — Reset refuses while the demo is running

1. Leave the healthy demo AppHost running.
2. In terminal B, run:

   ```sh
   ./scripts/demo-data.sh reset
   ```

3. Run status and refresh a sample page.

**Expected:** Reset fails with `Stop the demo AppHost (Ctrl+C) before resetting
its data.` The demo continues running and its data remains available.

## M10 — Remove the demo and recreate the original sample

1. Stop the demo AppHost with Ctrl+C. Run:

   ```sh
   ./scripts/demo-data.sh reset
   ./scripts/demo-data.sh reset
   ```

2. The first reset should report removal. The second should report
   `Demo data is already removed.`
3. Run `./scripts/demo-data.sh seed` while the demo is stopped.
   It should fail with instructions to start the demo environment first.
4. Start the demo again; wait for healthy apps, then run **status before seed**.
   Expect M02's empty business counts and missing demo staff accounts.
5. Run seed and status. Sign in again and revisit the sample.

**Expected:** The original M03 counts return. `Demo Wicklow Care` and the archived
`Demo Retired Brand` are restored. The manual brand/customer/location are absent.
Cobh is unassigned again. Old record URLs and sessions are not expected to survive
a reset; find recreated records through the lists and sign in again.

## M11 — Verify normal development was preserved

1. Stop the demo AppHost. Start normal development with the M01 command.
2. Sign in to its admin console again and compare **Users** with the M01 baseline.
   Compare the normal customer/product you recorded, if applicable.
3. Confirm normal development uses `sqlserver`, not `demo-sqlserver`.

**Expected:** Your original normal accounts and business records remain. Demo
seeding and resets have not added the demo staff, sample data, or manual demo
changes to normal development, beyond anything already present in your baseline.

## Record the results

| Check | Pass / Fail / Blocked | Actual result or evidence |
| --- | --- | --- |
| M01 Normal baseline | | |
| M02 Empty demo startup | | |
| M03 First seed | | |
| M04 Account access | | |
| M05 Connected sample | | |
| M06 Repeat and preserve edits | | |
| M07 Deleted sample stays deleted | | |
| M08 Restart persistence | | |
| M09 Active reset refusal | | |
| M10 Reset and restore | | |
| M11 Normal data preserved | | |

For a failure, record the check number, account, page URL or terminal command,
expected result, actual result, and relevant app logs from the Aspire dashboard.
Do not include passwords or unmasked connection strings in shared evidence.
