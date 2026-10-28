# WI-033 — product attributes and snapshot contract checkpoint

Date: 2026-10-27. Source: T-1.6.2 / PRD-US-004 S2.

The developer approved this checkpoint. Implementation is now complete; the sections below
preserve the evidence at the pause. Final results are in [catalogue-attributes-delivery.md](catalogue-attributes-delivery.md).

## Authorization and scope

The developer approved the plan and scenarios, then said `Apply`. The feature branch is
`feature/wi-033-record-product-attributes-carry-them`, created from current `main` at
`933c044`. WI-012 and WI-032 are integrated. Only WI-033's status changes in the console;
it remains active until all four criteria and final verification pass.

The slice is: "A Head Office User adds name/value attributes to a product, and after the
next sync a rep sees them on that product on the tablet."

## Head Office increment

The existing ProductAttributeValues table, stable AttributeName references, ordinal positions
and reference-usage reader are reused. No catalogue migration is needed.

`POST /catalogue/products/{id}/attributes` runs under the existing HeadOfficeCatalogue policy.
Its serializable transaction loads the product and name, validates every input before mutation,
rejects missing or archived names and appends one row. The database's unique product/position
index and foreign keys remain authoritative. Retirement uses the same transaction boundary.

The existing record shows name/value rows, followed by an Attribute name selector, Value field
and Add attribute action. Choices contain active names only. Rejected submissions retain their
value and field errors; unavailable selections cannot be reselected. Saved archived names stay
labelled. The website uses the existing BFF bearer client and antiforgery protection.

Predecessor semantics are preserved: repeated names are separate ordered rows, empty values
are allowed, whitespace and Unicode are retained, and renaming a name changes its displayed
label without rewriting values. Price, unit and classification saves preserve attributes.
Attribute editing/removal, filters, variants and additional restriction permissions are excluded.

## Contract for owner review

The written additive change is **1.0 to 1.1**, keeping major negotiation at 1. Existing fields
and the four existing payload families keep their names, types and meaning.

| Addition | Fields / default |
|---|---|
| `manifest.counts.productAttributes` | Integer row count; defaults to zero when absent in a 1.0 snapshot |
| `productAttributes` | Separate array; absent in 1.0 means no attribute rows |
| Each attribute row | `productId`, `position`, `name`, `value`, `isArchived` |

The post-checkpoint 1.1 builder must supply the array, including an empty array when the
count is zero. The optional constructor default represents the missing family in 1.0.

Rows are separate from products so the bounded parser reads one attribute at a time instead
of buffering every attribute on a product. Position preserves repeated names and row order.
Only included products may have attribute rows. The existing rule excludes every product
with a Restriction Group; WI-043 owns permission-based expansion.

The frozen 1.0 parser, contract and source-generated metadata under the tablet tests come
from `933c044`, with only namespaces changed. The compatibility test gives that parser a 1.1
manifest, the new count and 600 example attribute rows; it compares every existing payload
field and verifies the extra family is ignored. Later changes must not evolve those frozen files.

After continuation, the updated client will read 1.0 snapshots with no attributes, migrate its
snapshot database transactionally, import 1.1 rows into the staged generation and validate counts
and product references before activation. Unsent remains separate. A database written by a
newer app will continue to be refused on downgrade; wire compatibility does not promise local
database downgrade support.

## Size evidence and MI-02

An estimate in `.artifacts/wi033-size-estimate.json` uses 25,000 products, 500 Locations,
800 categories, 75,000 dated prices and the three source examples on every product (75,000
attributes). This attribute density is an **inferred measurement fixture**, not a product policy.
Stable UUIDs vary per entity; JSON is compact and gzip uses compression level 6.

| Payload | Plain bytes | Gzip bytes |
|---|---:|---:|
| Existing families | 12,991,543 | 2,189,360 |
| With attributes | 22,191,591 | 3,005,121 |
| Increase | 9,200,048 | 815,761 |

The largest example attribute row is 125 bytes. The transport increase is about 0.65 seconds
at the spike's simulated 10 Mbit/s, excluding processing and network overhead. This is a size
estimate, **not device performance proof**. MI-02's agreed budgets remain refresh under 90 s
and offline open under 3 s on SM-P610-class hardware. Full refresh/import and open timings
must be checked after the client implementation. The predecessor's open-time margin is thin.

No new attribute-value length policy is invented. Existing very long values remain supported
by the catalogue; the tablet's existing bounded-entry parser still refuses an oversized entry
without replacing the old snapshot. The three-example estimate does not establish real-world
attribute density or maximum value size.

## Checkpoint and remaining work

The contract diff is written. Implementation is stopped for review of compatibility and MI-02 size.
Resume only on **`Continue T-1.6.2`**. No server attribute projection, tablet database migration,
attribute import or product-detail navigation is implemented before that continuation.

| Source criterion / self-check | Status at this checkpoint |
|---|---|
| "On a product, adding Weight 200 g, Shelf life 24 months, Barcode 5391234567890 shows them as name/value rows." | Pass: website/BFF/API/SQL scenario adds all three and reopens after restart. |
| "Only non-archived attribute names are offered; existing values under an archived name stay, labelled." | Pass: active choices, renamed/archived retained rows and stale-name rejection verified through website and API. |
| "After the rep's next sync the attributes are in the snapshot and visible on the tablet when the rep opens the product's detail." | Pending continuation: contract only; projection, local import and tablet screens remain. |
| "Restricted products' attributes are never in a snapshot that excludes the product." | Pending continuation: attribute projection/exclusion scenario remains; the predecessor's product exclusion rule is unchanged. |
| "Snapshot version bumped; older fields untouched" | Pass: minor 1.1, unchanged major 1 and original fields; frozen-reader and source-generated metadata checks pass. |
| "Provisional commit message reconciled with the actual diff" | Capture commit `f99b795` describes the actual Head Office increment; the contract checkpoint is a separate commit. |

All three contract checks pass: frozen 1.0 bounded reader, 1.0-only payload read by current
metadata, and 1.1 attribute round-trip with frozen 1.0 metadata. All 58 tablet Core checks
also pass after the contract bump. This proves wire compatibility; SQLite migration and
device performance remain part of the post-checkpoint implementation.

## Checkpoint verification results

- Before the contract edit, the complete Release .NET regression passed **2,586 cases**,
  zero failed/skipped: API integration 138, DemoData 4, identity/admin integration 799,
  snapshot spike 22, tablet Core 56, tablet spike 41, website integration 858, API unit 182,
  identity/admin unit 258 and website unit 228.
- After the contract edit, **19 snapshot API cases and 58 tablet Core cases** passed,
  zero failed/skipped. The latter includes the three explicit contract checks. Both
  compatibility directions and the actual version header/manifest negotiation are covered.
- Complete solution builds in Release before and after the contract edit: zero errors
  or new code warnings. Only standing NU1902/NU1903 package advisories remain.
- Existing JavaScript suites: **34 passed**, zero failed/skipped. Locked dependencies
  were restored with `npm ci --ignore-scripts --no-audit --no-fund`.
- Existing Python shell-script suite: **5 passed** unchanged in a disposable Linux container,
  from a temporary LF checkout matching Linux CI. Windows and CRLF-mounted attempts could
  not execute the shell fixture; no assertion or repository file was changed to get green.
- Actual Head Office Razor output inspected at desktop and 390px widths, with no horizontal
  overflow. Local screenshots/HTML are in `.artifacts/wi033-layout/`; the static preview
  at `http://127.0.0.1:7299/` demonstrates layout only, not form submission.
- Only WI-033's `workItemStatus` entry changed in the delivery console, once to active.
  It has not been marked done. No integration branch was changed.

After continuation: finish the server projection and integrity checks, SQLite upgrade and
generation cleanup, minimal offline product list/detail screens and session guards. Verify all
four source criteria, legacy-snapshot reads, migration/recovery, exclusion, rep isolation,
Unsent preservation and device budgets; run the complete suite and Android Release build.
Mark done, commit and push the feature branch only when the final acceptance gate is green.
Do not integrate the branch.

## Missing information

No required implementation placeholder is unresolved. The repository is `D:/repos/Field-Sales`,
integration branch `main`, with the existing .NET 10/Aspire/Razor Pages/BFF/EF Core application
and MAUI Blazor Hybrid tablet under `src`. The console uses workItemStatus rather than an
embedded status-json block; only that status map is changed. Separate novelty/ambiguity
ratings are absent; the card gives Simple complexity, Medium risk and Medium confidence.
