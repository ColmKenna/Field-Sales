# Product classification — WI-012 layout checkpoint

Date: 2026-10-01. Source: T-1.6.1 / PRD-US-004, H-13/H13.2.
Status: complete. The developer approved the rendered layout in this chat with
`Continue T-1.6.1` on 2026-10-01, after reviewing checkpoint commit `f512711`.

## Layout for review

The existing product record keeps price, unit, ranges and replacements before
classification, with attributes afterward. The classification section spans the
record width. Saved profile, brand roles, supplier and restriction group appear
above one edit-in-place form. Four optional selectors and an alternative-brand
checklist share one Save classification action.

The rendered example is SUN-0342, SPF30 Sun Lotion v2 200ml, under
Health > Skincare > Suncare > Lotions, priced at €12.50 per Each. Classification
shows Chilled, SunCo (Primary), GlowCo (Alternative), Irish Health Supplies and
Pharmacy-only medicines. The acceptance scenario first saves without a restriction
group, then assigns the group for the fully populated checkpoint.

At a 390px viewport, fields and summaries stack vertically without horizontal
overflow. Controls use the existing native HTML, Razor validation and website styling.
The browser viewport override was reset after inspection.

Screenshots and HTML are local ignored verification artifacts:

- `.artifacts/wi012-layout/desktop.png`: complete desktop record.
- `.artifacts/wi012-layout/classification.jpg`: classification section detail.
- `.artifacts/wi012-layout/mobile.png`: complete narrow record.
- `.artifacts/wi012-layout/index.html`: HTML rendered by the real website test.

The temporary preview at `http://127.0.0.1:7298` serves that rendered HTML and the
real stylesheet/script assets from the working tree. It is a static layout
preview; form submissions are verified by the SQL-backed end-to-end tests.
Regenerate it by setting `WI012_LAYOUT_DIR` to that artifact directory before running
`ProductClassificationEndToEndTests`, copying `site.css` and `product-unit-form.js`
from the website's `wwwroot`, and running the local `serve.cjs` helper.

## Implemented behavior

The protected API validates every selected reference inside a serializable
transaction before changing the product. Missing or newly archived references,
repeated alternative IDs and alternatives without a primary reject the entire save.
The exact primary error is "Choose a primary brand first". Existing archived
references remain labelled and can be retained, cleared or replaced. The existing
allowance for one brand in both roles is preserved, with one product counted once.

The website retains submitted choices and field errors on failure. Bearer tokens
remain in the existing BFF client; live HeadOfficeCatalogue authorization and
antiforgery protect the save. Classification, price and unit forms preserve one
another's data. Usage counts reuse the existing shared reference-data reader.
No migration or production schema change is needed.

## Verification at the checkpoint

Characterization ran before changing production behavior: four new domain cases
passed, followed by the API/website baseline (106 API and 212 website cases).
The initial SQL baseline failed because Docker Desktop was stopped; starting the
existing engine resolved that prerequisite, and the complete baseline rerun passed.

Focused implementation checks passed: 16 API/characterization cases and nine real
website/BFF/API/SQL cases. Admin UI JavaScript: 18 passed. Console and product-unit
JavaScript: 16 passed. Solution build with warnings as errors: zero warnings/errors.
The full bounded solution run passed: 118 API, 1,046 identity and 221 website tests
(1,385 total), with zero failures/skips. TRX/log evidence is in `.artifacts/wi012-full`.

The review confirms the H13.2 section order and editing layout. Attributes,
restriction visibility on tablets, availability, replacements and quantity breaks
remain in their separately planned items.

## Acceptance audit

The five criteria below quote the WI-012 card's implementation guidance, also
numbered in T-1.6.1. All pass.

1. **"Profile \"Chilled\", Primary Brand \"SunCo\", Alternative Brand \"GlowCo\",
   supplier \"Irish Health Supplies\", Restriction Group none saves and shows."**
   `Should_SaveAndShowClassification_When_SourceExampleIsSubmitted` submits those
   exact values through the website, checks the saved summary and selected controls,
   then reopens the website/API against the same SQL database after an API restart.
2. **"Adding an Alternative Brand with no Primary Brand shows \"Choose a primary
   brand first\"."** `Should_KeepEnteredValuesAndSavedRecord_When_PrimaryBrandIsMissing`
   checks the exact message, retained inputs and unchanged persisted classification.
   `Should_RejectWholeSave_When_AlternativesHaveNoPrimary` covers both new assignment
   and removal of an existing primary.
3. **"A product may hold at most one Restriction Group; setting \"Pharmacy-only
   medicines\" saves it."** The source-example website test assigns and displays
   that group. `Should_UpdateUsageCounts_When_ClassificationIsReplacedOrCleared`
   verifies replacement and clearing. The existing scalar RestrictionGroupId stores
   one optional group; classification saves do not add another relationship.
4. **"Archived items (e.g. Profile \"Frozen\") are not offered, and a product
   already set to one shows \"Frozen (archived)\"."**
   `Should_PreserveRecordAndLabels_When_ArchivedLinksAreRetainedThenReplaced` excludes
   unused archived Frozen from choices and checks existing archived profile, both
   brand roles, supplier and group labels. Its existing profile is Chilled; the
   name-independent Razor label appends the same ` (archived)` suffix to Frozen.
   Domain characterization separately retains an existing archived Frozen profile.
   API stale-selection cases reject new archived references before any mutation.
5. **"Brand, profile, supplier and group usage now appears in each list's
   \"Used by …\" count through T-1.5.1's interface."** The source-example website
   test checks `Used by 1 product` for every assigned reference through the real
   reference-list API. The usage-count API test checks replacement and clearing;
   `Should_CountProductOnce_When_BrandHasBothRoles` checks distinct product counting.

Source self-verification: criteria 1–5 pass with the evidence above; H-13 layout
passes with developer checkpoint approval; the provisional message is reconciled
to actual commit `feat(wi-012): edit product classification atomically on the record`.
The full approved scenario matrix and verification counts are retained in
`plan_docs/.agent-notes/WI-012.md`. No required placeholder remains unresolved.
