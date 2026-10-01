# Product search — WI-013 layout checkpoint

Date: 2026-10-01. Source: T-1.7.1 / PRD-US-010, H-12.
Status: awaiting the approved populated desktop/narrow layout review.
Resume trigger: `Continue T-1.7.1`.

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
The full solution regression and criterion-by-criterion completion audit remain for
the approved continuation after this checkpoint. WI-013 stays active. No feature
push or main integration is performed at the checkpoint.
