# Product search — WI-013 layout checkpoint

Date: 2026-10-01. Source: T-1.7.1 / PRD-US-010, H-12.
Status: complete. The developer supplied `Continue T-1.7.1` in this chat
on 2026-10-01 after reviewing checkpoint commit `ecced95`.

## Layout for review

The Products page has a search box, full-path category selector and brand selector,
with Search, Clear filters and + Product actions. Each result links to its product
record and shows code/name/category path, followed by primary brand, Active state
and today's base price per unit. Native controls and the existing website styling
wrap into a vertical form at 390px. Desktop and narrow browser inspections found
no horizontal overflow. The temporary viewport override was reset afterward.

The populated example searches SPF30 and shows three products: two under
Health > Skincare > Suncare > Lotions and an identically named product under
Health > Body Care > Lotions. SUN-0342 shows SunCo, Active and €12.50 per Each.

Availability and archived-category state are absent from the current model. All
current products are Active; the include-unavailable/archived-category checkbox
is disabled, as agreed in the implementation plan. Its helper text describes the
current catalogue. Availability and category archiving remain in their planned items.

Screenshots and HTML are ignored local evidence:

- `.artifacts/wi013-layout/desktop.jpg`: populated desktop page at 1365px.
- `.artifacts/wi013-layout/mobile.jpg`: populated narrow page at 390px.
- `.artifacts/wi013-layout/index.html`: HTML exported by the real SQL-backed website test.

The temporary preview at `http://127.0.0.1:7297` serves that rendered HTML and the
real stylesheet. It supports visual review; the automated website tests verify
search requests and record/create navigation through the live test API. Regenerate
the HTML by setting `WI013_LAYOUT_DIR` before running ProductSearchEndToEndTests,
copy the website's `wwwroot/css/site.css`, and start the local `serve.cjs` helper.

## Implemented behavior

Protected GET `/catalogue/products/search` searches code, name, primary or
alternative brand and supplier with trimmed, case-insensitive literal substrings.
Blank search lists all products. Category filtering includes the selected category
and descendants at arbitrary depth; brand membership includes either role. Filters
combine with AND. Products sort by code then ID, with no silent result cap. Archived
brand and supplier names remain searchable, and archived primary brands are labelled.

Results include complete breadcrumbs and the base price effective on the existing
Dublin business date, excluding future entries. The BFF keeps bearer tokens on the
server and preserves missing, unauthorized, forbidden and unavailable outcomes.
Malformed successful responses are rejected. Existing live head-office access
guards protect the page and endpoint. No migration or schema change is needed.

## Verification at the checkpoint

The external coverage run was left running as the developer requested. After it
finished, Docker responded and the unchanged category/price baseline passed all
22 checks (10 API, 12 website), with no failures/skips. Production changes were
applied only after that green baseline. Earlier attempts failed from runtime,
output-lock and Docker contention; they are not counted as passing evidence.

The warnings-as-errors sequential solution build passed with zero warnings/errors.
Focused checks passed: 23 API and 48 website/BFF tests, 71 total, no failures/skips.
They cover all search fields, both brand roles without duplicate products, archived
names, literal wildcard input, blank/no-match searches, combined filters, invalid
IDs, all 180 descendants without neighbours or truncation, Dublin date boundaries,
future-only prices, record/create navigation, live role/session denial, empty state,
API failures and malformed BFF payloads. JavaScript checks passed: 18 admin UI and
16 console/product-unit tests, 34 total, no failures/skips.

Logs and TRX evidence are in `.artifacts/wi013-baseline` and `.artifacts/wi013-focused`.
WI-013 remained active at checkpoint `ecced95`, with the full solution regression
and criterion audit reserved for the approved continuation. Nothing was pushed or
integrated at that checkpoint.

## Acceptance audit

The five criteria below quote the WI-013 card's implementation guidance, also
numbered in T-1.7.1. Each is satisfied within the approved current-phase scope.

1. **"Typing \"SPF30\" lists every matching product with code, breadcrumb,
   Primary Brand, availability state and current price."**
   `Should_FindDistinctProductsWithFullDetails_When_AnySearchFieldMatches` checks
   every match, unique IDs, different breadcrumbs for identical names, primary
   brand, Active state and €12.50 current price. The website scenario
   `Should_ShowDistinctPathsTodayPriceAndRecordLinks_When_SPF30IsSearched` verifies
   those rendered fields and navigates to the linked records. The reviewed desktop
   and narrow screenshots show the populated three-product example.
2. **"Search matches code, name, any brand (primary or alternative) and supplier."**
   The API theory exercises all four fields and both brand roles, including a brand
   in both roles on one product without duplicate results. Website theory cases
   exercise code, brand membership and supplier through the BFF. Archive tests
   retain searchability of existing archived names and label the primary brand.
3. **"Filtering Category \"Suncare\" lists all products beneath it at any depth,
   each with its own breadcrumb."**
   `Should_ListAll180AndNoNeighbours_When_CategorySubtreeIsSelected` includes direct
   category products and descendants at seven breadcrumb levels, checks all 180
   results and their breadcrumbs, excludes the neighbouring Body Care category,
   and checks stable ordering. The website's
   `Should_RenderEveryDescendantWithoutNeighbours_When_CategoryHas180Products`
   verifies all 180 rendered result links and the deepest breadcrumb without
   truncation. Combined-filter tests verify AND rather than OR behaviour.
4. **"By default Unavailable products are excluded; ticking \"Include unavailable
   and archived-category products\" includes them, labelled. (Availability states
   exist from E8; until then every product is Active.)"**
   Pass for the explicit current-phase condition and approved plan: the current
   model has no availability or archived-category state, all results show Active,
   and the checkbox is disabled. The website source-example test checks the disabled
   checkbox. Unavailable inclusion and archived-category labelling remain deferred
   to E8 and WI-100/101; those future behaviours are not claimed as implemented.
5. **"\"+ Product\" opens the minimum create form (T-1.2.1)."**
   The website source-example test checks the + Product link, follows its exact
   destination, and verifies the existing Create product page. The create form
   remains the predecessor's minimum form.

Source self-verification:

- Criteria 1–5: pass for the scoped implementation, with criterion 4's explicit
  current-phase condition and approved limitation recorded above.
- Today's rather than future-dated price: pass. The existing Dublin date function
  is reused; the API midnight theory checks both sides of the business-date change,
  future-only prices produce no current price, and the website excludes €99.00
  future pricing while showing €12.50.
- Commit message reconciled with the actual diff: pass. Checkpoint `ecced95` is
  `feat(wi-013): search products across catalogue fields and descendants`, with
  WI-013, T-1.7.1 and PRD-US-010 references. No schema or unrelated feature changes.

The agreed scenario matrix also covers invalid filters, blank/no-match and empty
catalogue states, literal wildcard input, server-held bearer/query encoding,
expired sessions/removed roles, unavailable APIs and malformed successful payloads.
The populated H-12 checkpoint is approved; full regression results are recorded
below.

## Completion verification — 2026-10-01

The full bounded solution run exited 0: 131 API, 1,046 identity and 238 website
tests passed (1,415 total), with zero failures/skips. Each TRX counter reports every
test executed and passed. Evidence is in `.artifacts/wi013-full/test.log` and the
three sibling TRX files. Production code and tests are unchanged from checkpoint
`ecced95`, so its warnings-as-errors build and all 34 JavaScript results remain
valid. All 11 console status tests passed again after the final status-map update.

All five scoped criteria and the agreed scenario matrix pass as detailed above.
The source's future availability/archive condition remains explicitly deferred.
The parsed console comparison confirms only WI-013 changed from active to done;
all other statuses and card/spec content are identical to the checkpoint.
No unresolved technical placeholder or checkpoint remains.

The completion commit records layout approval, the acceptance audit and done
status. Delivery stops at the committed/pushed feature branch; main integration
requires a later developer request. The next console item is WI-014 — Maintain
Regions, Counties and Towns (T-2.1.1), informational only.
