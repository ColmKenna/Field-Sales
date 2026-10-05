# Isolated demo data

The `demo` AppHost launch profile runs the existing applications against a separate
SQL Server container and volume, `fieldsales-demo-sqlserver-data`. All six databases
have names starting with `FieldSalesDemo_`: Identity, Identity configuration,
Identity operational grants, staff website sessions, catalogue, and directory.
Normal development continues to use `fieldsales-identity-sqlserver-data` and its
original database names.

## First run

Docker and the project's .NET SDK must be available. Configure the four existing
AppHost secrets if they have not already been set:

```sh
dotnet user-secrets set --project src/FieldSales.AppHost "Parameters:sql-password" "<SQL password>"
dotnet user-secrets set --project src/FieldSales.AppHost "Parameters:staff-web-client-secret" "<client secret>"
dotnet user-secrets set --project src/FieldSales.AppHost "Parameters:seed-sysadmin-password" "<admin password>"
dotnet user-secrets set --project src/FieldSales.AppHost "Parameters:seed-test-user-password" "<demo staff password>"
```

From the repository root, start the demo environment:

```sh
./scripts/demo-data.sh start
```

The dashboard opens as usual. Wait for Identity, the staff API, and the staff
website to become healthy, then use a second terminal:

```sh
./scripts/demo-data.sh seed
./scripts/demo-data.sh status
```

The staff website is at `https://localhost:7203` and the Identity admin console is
at `https://localhost:7201/Admin`. The demo profile uses the same application ports
as normal development, so stop the normal AppHost before starting the demo profile.
After switching profiles or resetting, sign in again; sessions and data protection
keys belong to each environment's separate databases.

## Accounts and scenarios

| Account | Role | Password source |
| --- | --- | --- |
| `admin@sales.local` | SysAdmin | `Parameters:seed-sysadmin-password` |
| `testuser@sales.local` | No business role | `Parameters:seed-test-user-password` |
| `headoffice@demo.sales.local` | Head Office User | `Parameters:seed-test-user-password` |
| `manager@demo.sales.local` | Sales Manager | `Parameters:seed-test-user-password` |
| `aoife@demo.sales.local` | Field Salesperson | `Parameters:seed-test-user-password` |
| `colm@demo.sales.local` | Field Salesperson | `Parameters:seed-test-user-password` |

The first two accounts come from the existing Development Identity seed. The other
four accounts and business data are created only by the explicit demo seed command.

The sample includes:

- Three categories and four products: retail items, a restricted product, and a
  product sold in litres with minimum and step quantities. One product has past,
  current, and future price entries, dated relative to the first seed run.
- Brands, an archived brand, a product profile, supplier, restriction group, and
  an attribute name with product values.
- Two regions, three counties, four active towns with coordinates, and an archived town.
- Three customers, four locations, two location types, two contact types, and three
  contacts. One buyer is shared between two branches; a branch has a second contact.
- Two reps reporting to the manager. Aoife owns Wicklow county; Colm owns Swords town
  and has an explicit Arklow location override. Cobh remains unassigned. Initial
  assignment history entries are included.

All records and email addresses are fictional testing examples. Town coordinates
are approximate, and no external Eircode lookup is needed.

## Repeatability

```sh
./scripts/demo-data.sh seed
```

Completed batches are skipped, preserving IDs, prices, passwords, roles, assignments,
and edits made while testing. A receipt in `__FieldSalesDemoSeeds` is committed in the
same transaction as each database's sample data. A failed batch rolls back; rerunning
finishes any incomplete batches. Concurrent seeds are serialized by database locks.
The three seed batches are separate transactions, not one distributed transaction.

Deleting a sample record does not cause it to reappear on the next seed or startup.
Use reset to restore the original scenario from scratch. Normal demo startup still
runs the existing Development Identity configuration and admin/test-account seed.

The standalone seed/status command reads AppHost user secrets and uses only
`127.0.0.1:14339` with the explicitly named demo databases. It accepts no arbitrary
connection string or database override and checks all six schemas before seeding.
The AppHost demo profile and command refuse non-Development environments.

## Remove or reset

Stop the demo AppHost with Ctrl+C, then run:

```sh
./scripts/demo-data.sh reset
```

This removes the demo SQL volume, including records added during testing, assignment
history, accounts, grants, and sessions. It refuses while the demo container is running.
It removes stopped containers attached to that specific volume if necessary. Reset
does not remove normal development storage or user secrets. Repeating reset is safe.

To recreate the original scenario, start the demo environment again and run seed.
To return to normal development, run:

```sh
dotnet run --project src/FieldSales.AppHost --launch-profile https
```

## Verification

For terminal and browser checks, follow the
[manual testing guide](demo-data-manual-testing.md). It covers sample browsing,
access, repeat seeding, preservation of edits, restart, reset, and normal-data isolation.

### Automated verification

```sh
dotnet test tests/FieldSales.DemoData.Tests
python3 tests/demo-data-script.test.py
```

The integration tests use disposable SQL Server databases to verify a complete
scenario, preserved testing edits on repeat, rollback/retry, and concurrent seeding.
The script tests use a mocked Docker executable to verify reset refusal, scope,
repeat removal, and failure handling without deleting actual storage.
