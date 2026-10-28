# WI-033 — product attributes from Head Office to the tablet

Date: 2026-10-27. Source: T-1.6.2 / PRD-US-004 S2.
Status: complete. All four source acceptance criteria and automated checks pass; device evidence is recorded below.

## Approved scope and checkpoint

The developer approved the plan/scenarios and then said `Apply`. After review of the written
1.1 contract checkpoint, the developer replied `approved` to authorize continuation.
The checkpoint evidence remains in [catalogue-attributes-checkpoint.md](catalogue-attributes-checkpoint.md).

The slice is: "A Head Office User adds name/value attributes to a product, and after the next
sync a rep sees them on that product on the tablet."

## Delivered behavior

Head Office adds rows in the existing product record using active global attribute names.
The existing serializable reference-assignment boundary, foreign keys and ordered value table
are reused. Values retain exact whitespace, empty strings, Unicode and repeated names; retired
names remain labelled. The protected API and BFF enforce current Head Office access and antiforgery.

Snapshot 1.1 adds a separate productAttributes family and manifest row count. The server reads
values and names in the same catalogue transaction and with the same inclusion predicate as
products. Every excluded product's attributes are excluded too. Archived names and current
labels travel with their saved values. Existing fields and major negotiation remain unchanged.

On the tablet, the existing version-one database upgrades transactionally to schema two by
adding one table. Existing generations, Home, prices and Unsent remain intact. Import is bounded
per row, accepts old 1.0 snapshots, requires the attribute family for 1.1+, validates every row,
count and product reference before activation, and includes attributes in generation cleanup.
A failed download/import keeps the previous whole snapshot. Local database downgrade is refused.

Home links to a minimal paged offline product list. Product detail reads the active generation's
code, name, breadcrumb, unit and ordered name/value rows, with archived labels. Reads are local
and off the UI thread; detail uses one SQLite read transaction so Sync cannot mix generations.
Both catalogue routes clear data and check the current rep's 24-hour session on navigation and
resume. Missing products have an explicit unavailable state; an empty catalogue and products
without attributes have empty states.

## Acceptance evidence

| Quoted source criterion | Evidence |
|---|---|
| "On a product, adding Weight 200 g, Shelf life 24 months, Barcode 5391234567890 shows them as name/value rows." | **Pass.** Website/BFF/API/SQL source-example scenario adds all three, checks row order and reopens after restart. |
| "Only non-archived attribute names are offered; existing values under an archived name stay, labelled." | **Pass.** Website and API tests cover active choices, renamed/archived retained rows and stale archive/delete rejection. The Samsung retains the pack-size value labelled `(archived)` offline. |
| "After the rep's next sync the attributes are in the snapshot and visible on the tablet when the rep opens the product's detail." | **Pass.** Full website-to-Sync-to-real-SQLite test passes. After Sync, the Samsung shows all three source values on DEMO-001. With Wi-Fi off and both service routes removed, cold reopening and a page-two product still show the values. |
| "Restricted products' attributes are never in a snapshot that excludes the product." | **Pass.** Snapshot endpoint test includes ordinary attributes and proves that a restricted product and its private value are absent on both downloads. |

Additional tests prove exact long/empty/repeated values, separate form saves, live access and
session failures without replay, both wire compatibility directions, SQLite v1 upgrade,
rep isolation, paged reads, out-of-order families, invalid/missing rows and counts, oversized
entry rejection, force-close before/after activation, bounded cleanup and preserved Unsent work.
No existing test was weakened, skipped or deleted. Future-minor fixtures now include the known
1.1 family; the newer-local-format refusal test now uses schema 3 because schema 2 is current.

## Device verification

The approved Samsung SM-P610, serial R52T30AZZXZ, is connected. The Release dev-trust APK
built successfully and installed over the existing app without clearing data. APK SHA-256:
`29DDD8E4090E073B30697656731206F4C8692259B81219E38333E3029C70F086`.
This final APK includes the pagination scroll-space correction verified on the device.

Only this repository's Aspire demo API/website were rebuilt/restarted. The isolated
FieldSalesDemo_CatalogueDb now contains 75,005 attribute rows: the three source examples on
25,000 existing scale products and DEMO-001, plus the predecessor's two pack-size values.
The seed helper checks the exact demo database name and preserves products, prices and other
references. Its connection details are read into process memory and are not printed.

After the developer unlocked the device, Sync completed in **57,813 ms**, within the 90-second
budget: 502 Locations, 803 categories, 25,003 products, 75,004 prices and 75,005 attribute rows.
Cleanup took 20 ms. Home reports Last synced 16:38. The product list and DEMO-001 detail render
correctly, including the three exact source examples and the existing pack-size value. Screenshots
are in `.artifacts/wi033-products.png` and `wi033-product-detail.png`.

The follow-up Sync took **49,924 ms**, also within 90 seconds, with the same complete counts;
post-switch cleanup took 4,978 ms in the background. Existing values under the temporarily
archived demo pack-size name remained labelled. Wi-Fi was disabled and both local identity/API
reverse routes removed: product detail still showed the source values and archived label.
Three cold offline starts reopened all 502 shops, and page-two product SCALE-00048 displayed
the three source values offline. The final APK's paging controls are fully visible above
Android's taskbar. Evidence: `.artifacts/wi033-archived-offline.png`, `wi033-final-paging.png`,
`wi033-final-offline-detail.png` and `wi033-device-timings.txt`.

**Existing startup limitation:** cold opens were 3,462 / 2,679 / 2,791 ms, and 3,273 ms after
installing the final CSS-only correction. The 3-second target is not met on every start.
This remains under the developer's standing WI-032 decision, "Open-time speed is deliberately
left as is", recorded in [WI-032 notes](../plan_docs/.agent-notes/WI-032.md). No authentication
or startup-policy change is introduced by WI-033. The two full refreshes meet their 90-second
budget, and all four functional criteria pass.

Cleanup verified: Wi-Fi restored to its original enabled-in-airplane-mode state (setting 2),
both original reverse routes (7201/7204) restored, screen timeout restored to 120,000 ms,
guarded awake helper exited and the demo pack-size name restored active. The application and
downloaded data are retained. The temporary desktop static-preview server was stopped.
Other applications and the other running repository's AppHost are untouched.

## Verification state and boundaries

- Focused server snapshot scenarios: 20 passed, including the new restriction/attribute case.
- Tablet Core: 72 passed, including 14 new storage/read/recovery cases.
- Attribute website scenarios: 11 passed, including the complete form-to-offline flow.
- Android Release APK and complete Release solution build pass. Existing NuGet advisories remain.
- Final JavaScript suite: 34 passed. Final Python suite: 5 passed unchanged in an LF Linux checkout.
- Final .NET checks: **2,604 cases pass**, none skipped, using the dependency versions already
  pinned in Directory.Packages.props. API integration 139, DemoData 4, identity integration 799,
  snapshot spike 22, tablet Core 72, tablet spike 41, website integration 859, API unit 182,
  identity unit 258, website unit 228. One initial identity-directory case and its collection
  cleanup failed; all three directory cases and then the complete 799-case identity project
  passed unchanged, including cleanup. No assertion or test scope changed to obtain green.

Out of scope: attribute editing/removal, attribute search/filters, full catalogue browsing,
variants, additional restriction-permission delivery (WI-043), Unsent capture/upload screens,
iPad use and branch integration. No required implementation placeholder remains unresolved.
The branch is feature/wi-033-record-product-attributes-carry-them. The console changed only
WI-033's workItemStatus, once to active and once to done; card/source text is unchanged.

## Required self-verification and handover

- **Pass — Criteria 1–4 demonstrated:** evidence is quoted individually above.
- **Pass — Snapshot version bumped; older fields untouched:** minor 1.1, major 1 unchanged;
  frozen 1.0 reader/metadata and current 1.0-only reads verify compatibility.
- **Pass — Provisional commit message reconciled with the actual diff:** commits describe
  Head Office capture, contract 1.1, sync/storage, tablet screens and final device verification.
  Each carries only the requested work-item references, with no automatic attribution.

Changed-file map: catalogue contracts/API/reference assignments and Head Office record/BFF;
snapshot contract/metadata and builder; tablet parser, schema upgrade, generation storage and
product reads; guarded tablet list/detail screens and CSS; domain, API, website, real-SQLite,
compatibility and recovery tests; this evidence, checkpoint notes and console status.

Next by console order: **WI-034 — Search assigned shops offline (T-4.2.1)**, informational only.
It has not been started. The feature branch is handed over committed and pushed; integration
remains with the developer. No merge, integration fast-forward, rebase, force-push or branch
deletion was performed.
