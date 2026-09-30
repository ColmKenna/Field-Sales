# Minimum product foundation — WI-005

Date: 2026-09-30. Source: T-1.2.1 / Product Management US-001 S1–S5.

## Approved plan and product policies

The developer approved the WI-005 plan, scenario matrix, and proposed defaults on 2026-09-30. The feature branch starts from the completed WI-004 branch so it includes the category foundation. The existing `plan_docs/field-sales-delivery/plan-data.js` `workItemStatus` map is the authorized progress record.

- Codes are entered by head office, trimmed, and preserved in their entered casing. They are unique without case sensitivity, up to 100 characters. No generated code or additional format restriction is introduced.
- Names are trimmed, required, and limited to 200 characters.
- A category identity is required; a product may belong to a branch category as well as a leaf.
- The initial unit is Each. Measured units and quantity step/minimum remain WI-007.
- Prices use exact `decimal(18,2)`, accept zero, and reject negative amounts, values exceeding that SQL precision, and amounts requiring rounding to two decimal places.
- The create operation derives today's date on the server in Europe/Dublin, using a testable clock. The initial-price date is not chosen by the browser.

## Model-review checkpoint

The developer approved the model-review checkpoint after reviewing commit `8a18aa9`, authorizing the remaining API, website, and scenario implementation. The checkpoint preserved the task's intended review of product references, captured prices, and the Phase 2 allowance before adding endpoints or pages.

### Products

`Product` owns a stable GUID identity. Its persisted fields are `Id`, `Code`, `Name`, `CategoryId`, `Unit`, nullable `ParentProductId`, and `Attributes`.

`CategoryId` references the existing category table. `ParentProductId` is a nullable self-reference retained for Phase 2; minimum creation leaves it null. Both relationships use restricted deletes. Attributes are an empty collection of name/value records serialized into a column on Products, with structural change tracking. No attribute table, attribute editing, variant grouping, or parent selector is introduced.

The `UX_Products_Code` unique index uses the same case-insensitive, accent-sensitive SQL Server collation as the category foundation (`Latin1_General_100_CI_AS`). Domain creation trims codes before saving. All API duplicate lookups must use SQL's same code comparison. The index is the final authority for concurrent writes.

### ProductBasePrices

`ProductBasePrice` stores `ProductId`, `EffectiveFrom` as SQL `date`, and `Amount` as `decimal(18,2)`. `(ProductId, EffectiveFrom)` is its composite primary key, allowing at most one entry per product/day. A restricted foreign key references Products; a SQL check constraint rejects negative amounts.

`Product.Create(code, name, categoryId, basePrice, effectiveFrom)` builds the product and its initial entry together. Saving that aggregate with one `SaveChangesAsync` operation uses EF Core's transaction, so a rejected product cannot leave an orphan price. New dated entries do not overwrite the first entry; editing prices remains WI-008.

### Price on a date

Signature: `ProductBasePrice? Product.BasePriceOn(DateOnly date)`.

The caller loads `BasePrices`; the method selects the latest `EffectiveFrom <= date`, returning null before the first entry. The returned entry includes product identity, effective date, and exact amount. Future order and stock-check records can reference the product identity; order lines must store the price captured at ordering time rather than recalculate it later. Those record types are outside this slice.

### Schema boundary

The migration creates only Products and ProductBasePrices, their indexes, check constraint, and foreign keys. Existing category columns and indexes are unchanged. Migration execution during verification is confined to disposable SQL Server test databases; production deployment is not part of WI-005.

## Implemented website and API flow

- `GET /catalogue/products/category-choices` returns every category with its current full breadcrumb. Products may be placed on branches, as required by the catalogue source.
- `POST /catalogue/products/` validates the minimum fields and saves Product plus its first price entry in one transaction. Both the ordinary duplicate lookup and SQL-concurrency conflict use the persisted product's code and name in the required message.
- `GET /catalogue/products/{id}` returns the product, current category path, applicable price, price history, and attributes.
- These routes use the existing HeadOfficeCatalogue policy, including the live staff-role check. Website calls carry the existing server-held bearer token; no token is exposed in page content.
- `/HeadOffice` links to `/HeadOffice/Products/Create`. Its one short form has code, name, category, Each, and a base price effective today. A successful save redirects to `/HeadOffice/Products/{id}`.
- The record displays the unranged note and empty Profile, Attributes, Brands, supplier, Restriction Group, and Replacements sections. Later editing controls follow their own work items.
- Field errors retain entered form values. An expired session or removed head-office role prevents writes; sign-in does not replay the rejected form.

## Checkpoint verification

- Complete solution build: passed with zero warnings and errors.
- API tests: 20 passed, none skipped, including the existing category tests and 11 new product cases.
- Website tests: all 43 existing tests passed, including the website-to-API-to-SQL category flow after the new migration.
- SQL Server product checks prove concurrent case-insensitive duplicate saves produce exactly one product and one initial price, and a fresh context reads the correct price before, on, and after seeded effective dates.
- Only WI-005's status was added to the delivery plan at the model checkpoint.

## Acceptance evidence

All scenarios below are from the developer-approved matrix. Product end-to-end tests use the actual Razor Pages BFF, protected API, and SQL Server, with a test-only staff sign-in endpoint and controlled identity-role lookup. The API clock is fixed at 23:30 UTC on 30 September to demonstrate that the initial price starts on 1 October in Europe/Dublin.

| Source criterion | Evidence |
| --- | --- |
| "Code SUN-0342, name SPF30 Sun Lotion v2 200ml, Category Health > Skincare > Suncare > Lotions, Unit Each, Base Price €12.50 from today saves the product." | `Should_OpenSavedRecord_When_MinimumProductIsCreated` posts the website form, follows its redirect, and checks the persisted category, unit, exact price, and Dublin effective date. |
| "A duplicate code is rejected with Code SUN-0342 is already used by SPF30 Sun Lotion v2 200ml." | `Should_RejectDuplicateCode_When_CodeAlreadyExists` checks the exact message for both the original and whitespace/case variant; database counts stay at one product and one price. |
| "Saving without a Range saves it unranged with the note Unranged — orderable by all reps." | The minimum-create test checks the note on the saved record. No range picker or range assignment exists in this slice. |
| "Saving without a Category is rejected with Choose a category." | `Should_RejectSave_When_CategoryIsMissingOrUnknown` checks both omitted and unknown category identities, the required wording, and zero database writes. |
| "The saved record shows Profile, Attributes, Brands, supplier, Restriction Group and Replacements as empty sections." | The minimum-create test checks every empty-section message; the persisted attributes collection is empty. |
| "The base price is stored as the first entry of a price history with an Effective From date (today), not as a single overwritable field." | The minimum-create test checks the single persisted dated entry; the domain and SQL price-read tests demonstrate the latest applicable entry across seeded history. |

Additional approved evidence:

- `Should_SaveOnlyOneProduct_When_DuplicateCodesAreSubmittedConcurrently`: both direct API saves and direct persistence saves settle at exactly one product and one price entry; the API's rejected request reports the existing product.
- Required/long code and name checks retain the form and write nothing. Zero price opens a €0.00 record; negative and over-precision prices are rejected.
- `Should_PreserveProductAndPrice_When_ApplicationRestarts`: both website and API hosts are disposed and recreated; the same product URL, category, initial price, and effective date remain.
- `Should_RefreshProductBreadcrumb_When_CategoryIsRenamed`: the next website read shows the renamed path while the product identity, category identity, and price history remain unchanged.
- Unauthorized staff cannot read or post the product form or protected product API. Role removal and ticket expiry reject an already-open form; restoring access or signing in leaves the database unchanged.

## Changed-file map

- `FieldSales.Api/Catalogue/Product.cs`, `ProductBasePrice.cs`, `CatalogueDbContext.cs`, and the AddMinimumProducts migration/snapshot: domain model and SQL persistence.
- `FieldSales.Api/Catalogue/ProductEndpoints.cs` and `FieldSales.Api/Program.cs`: protected operations and business-date clock registration.
- `FieldSales.Web/Catalogue/CatalogueApiClient.cs`, `Pages/HeadOffice/Products/Create.cshtml[.cs]`, `Detail.cshtml[.cs]`, `Pages/HeadOffice/Index.cshtml`, and `wwwroot/css/site.css`: BFF calls, create/record pages, entry link, and scoped styles.
- `FieldSales.Api.Tests/ProductTests.cs`, `ProductPersistenceTests.cs`, and `FieldSales.Web.Tests/ProductEndToEndTests.cs`: agreed domain, SQL, and end-to-end scenarios.
- This decision record and only WI-005's `workItemStatus` entry in `plan_docs/field-sales-delivery/plan-data.js`: implementation evidence and progress.

## Later boundaries

Archived-category behavior belongs to later category archive work: the category foundation has no archived state yet. Range selection, tablet sync, product search, classification editing, attribute editing, measured units, and price-edit endpoints remain their own work items. No schema for those areas is inferred here.

## Completion verification

- All six source acceptance criteria above pass, with 19 product end-to-end cases and 11 product domain/persistence cases added from the approved scenario matrix.
- Complete solution build: zero warnings and errors.
- Complete .NET suite: 1,058 passed (20 API, 62 website, 976 identity/admin), zero failed or skipped.
- Existing JavaScript suites: 18 identity admin UI tests and 11 delivery status-store tests passed, zero failed or skipped.
- No floats are used for money or quantities. Products retain the optional parent reference and name/value attribute allowance without implementing variants.
- The delivery plan changed only WI-005's status: active at implementation start, done after acceptance and suite verification. No card content changed.
- No new missing information or unresolved policy decisions remain for WI-005. The next item is WI-006 — Show category counts, branch products and breadcrumb search.
- Commit subjects describe the delivered model, observable create flow, and verified scenarios; each includes the WI-005, T-1.2.1, and PRD-US-001 references and has no automatic attribution.
