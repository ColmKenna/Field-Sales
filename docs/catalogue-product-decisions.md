# Minimum product foundation — WI-005

Date: 2026-09-30. Source: T-1.2.1 / Product Management US-001 S1–S5.

## Approved plan and product policies

The developer approved the WI-005 plan, scenario matrix, and proposed defaults on 2026-09-30. The feature branch starts from the completed WI-004 branch so it includes the category foundation. The existing `plan_docs/field-sales-delivery/plan-data.js` `workItemStatus` map is the authorized progress record.

- Codes are entered by head office, trimmed, and preserved in their entered casing. They are unique without case sensitivity, up to 100 characters. No generated code or additional format restriction is introduced.
- Names are trimmed, required, and limited to 200 characters.
- A category identity is required; a product may belong to a branch category as well as a leaf.
- The initial unit is Each. Measured units and quantity step/minimum remain WI-007.
- Prices use exact `decimal(18,2)`, accept zero, and reject negative amounts, values exceeding that SQL precision, and amounts requiring rounding to two decimal places.
- The create operation will derive today's date on the server in Europe/Dublin, using a testable clock. The initial-price date is not chosen by the browser.

## Model-review checkpoint

The implementation is paused for the source task's required model review before adding product endpoints or website pages. Resume trigger: `Continue T-1.2.1`.

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

## Remaining approved work after checkpoint review

- Protected create/detail API operations and category choices with current breadcrumbs.
- One short Razor create form opening the saved record, with the exact duplicate-code and missing-category wording.
- The unranged note and empty Profile, Attributes, Brands, supplier, Restriction Group, and Replacements sections.
- Complete the approved website, API, persistence, and role-removal scenarios; run the complete solution suite; verify each acceptance criterion; commit and push the feature branch.

## Checkpoint verification

- Complete solution build: passed with zero warnings and errors.
- API tests: 20 passed, none skipped, including the existing category tests and 11 new product cases.
- Website tests: all 43 existing tests passed, including the website-to-API-to-SQL category flow after the new migration.
- SQL Server product checks prove concurrent case-insensitive duplicate saves produce exactly one product and one initial price, and a fresh context reads the correct price before, on, and after seeded effective dates.
- Only WI-005's status was added to the delivery plan; it remains active. Product endpoints, product pages, and business-date clock wiring await checkpoint review.

## Later boundaries

Archived-category behavior belongs to later category archive work: the category foundation has no archived state yet. Range selection, tablet sync, product search, classification editing, attribute editing, measured units, and price-edit endpoints remain their own work items. No schema for those areas is inferred here.
