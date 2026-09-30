# Dated base-price history — WI-008

Source: T-1.3.1 / Product Management US-002 S1, S3 and S4.
The developer approved the plan and policies on 2026-09-30.

History is append-only for this slice: add future, current-day or past entries; never edit or delete
an existing row. The API's current-price date remains the Europe/Dublin business date derived from
the injected TimeProvider. User-entered Effective From values are calendar dates, not instants.

`Product.BasePriceOn(DateOnly)` continues to return the latest entry effective on or before its
argument, or null before the first entry. `Product.AddBasePrice(decimal, DateOnly)` uses existing
exact-money validation and rejects duplicate dates. Callers must load BasePrices before using
these methods. The existing `(ProductId, EffectiveFrom)` database key settles concurrent conflicts.

`Product.BasePricePeriods()` derives ascending ranges with an inclusive end date one day before
the next entry. The final range has no end date. No end dates are stored, and inserting an earlier
entry leaves every existing amount and Effective From unchanged. An intervening entry limits the
new entry's range: a September 30 entry prevents a September 1 insertion from lasting to October 31.

The website's same-date rejection must retain the source's exact "edit it instead" wording.
Editing is unavailable in this approved slice, so this wording remains a known limitation.
Order lines must eventually store prices at capture; this item adds no Order or tablet model.
Tablet switching and customer pricing follow their own work items.

## Increment 1 evidence

Existing ProductTests passed before production edits (11 cases); all six new history cases also
pass. The full API suite passes (73 cases). The complete solution builds with warnings as errors,
zero warnings and zero errors. Console tests pass (11) and product-unit JavaScript tests pass (5).

Full-suite acceptance is pending: website and identity SQL fixtures hit Windows Application
Control's Docker.DotNet.dll block, and the admin UI dependency restore hits GitHub Packages 401.
The handover notes contain the exact results. At the Increment 1 stop, WI-008 remained active
with no API or website add-price flow; Increment 2 adds the API and BFF client as described below.

## Increment 2 — protected price insertion

After reviewing Increment 1, the developer approved continuing to Increment 2.

`POST /catalogue/products/{id}/base-prices` accepts:

```json
{ "basePrice": 13.20, "effectiveFrom": "2026-11-01" }
```

The existing HeadOfficeCatalogue policy checks bearer authentication, fieldsales.api scope and
the identity host's current Head Office role. A successful save returns 201 with the entry and
the product record URL as Location. It appends only; no existing amount or date changes.

The handler rejects missing/invalid amounts or dates with field validation errors (400), a missing
product with 404, and an existing effective date with 409. The date conflict identifies the
EffectiveFrom field and formats the source message with invariant English dates:
"A price already starts on 1 Nov 2026 — edit it instead". If two requests pass the initial check,
the existing SQL primary key settles the conflict and the loser receives the same message.
No migration is required.

The BFF client's AddProductBasePriceAsync keeps the bearer token server-side and carries field
errors to the future website form. This increment has no page handler or form; website interaction,
the coming-price summary and cookie-expiry assertions belong to the next increment.

The focused SQL/API scenarios verify saved history after host restart, an entry effective today
in Dublin while UTC is still the previous date, preserved rows after invalid/denied submissions,
and the exact duplicate-date message. The concurrency case pauses both saves after history reads
and before SQL insertion to force the database conflict. BFF client cases verify exact amount and
ISO date serialization, server-token use and field errors through the HTTP boundary.

Increment 2 verification: all 78 API cases pass, including the five new SQL/API scenarios and
the forced concurrent duplicate. All five BFF client cases pass. The complete solution build
has zero warnings/errors; all 16 console and product-unit JavaScript cases pass. The full .NET
suite records 1,075 passes and the same 80 Windows Application Control failures in website and
identity SQL fixtures. Admin UI dependency access remains unresolved. This increment is committed
for review with WI-008 still active; full acceptance and the website form remain outstanding.

## Increment 3 — record form and history

The developer approved continuing to the website increment on 2026-10-01. The existing product
record, create form and unit form supply the Razor Pages and CSS conventions; the approved scope
is an in-place price form and record display, following H-13's price section.

The record adds amount per saved unit and Effective From fields with inline errors and antiforgery.
Saving redirects to the same product record. A rejected form retains the entered amount/date and
reloads the saved product's unit fields. The API's exact duplicate-date wording is displayed without
offering an editing action, consistent with the approved append-only slice.

CurrentPrice remains authoritative from the API. The nearest later history entry drives the
coming-price summary; the website introduces no separate date resolver or clock. A rise uses
"€12.50 · rising to €13.20 on 1 Nov"; a reduction uses "falling to" and an equal amount uses
"changing to". If no price is effective, the earliest coming entry is shown with its date.
The table lists full Effective From dates and prices newest first, keeping later changes visible.

Website scenarios run the real Razor page, cookies, BFF requests and antiforgery boundary with
scripted API responses. The SQL/API cases from Increment 2 remain the persistence evidence.
The past-dated explanatory notice and final source-criteria audit are still Increment 4 work.

The website cases caught the existing required Unit property rejecting price-only submissions.
The Price handler now excludes only the other form's fields from ModelState before checking its
own amount/date inputs. Price field errors remain visible and stored unit values are repopulated.

Increment 3 verification: all 15 page cases pass, including stale-form denial without replay;
all 78 API cases pass. The complete solution build has zero warnings/errors, and 16 console and
product-unit JavaScript cases pass. The full .NET run records 1,090 passes and the existing 80
Windows Application Control failures. The initial temporary block on the rebuilt website test
assembly did not persist on the normal subsequent build/run. Admin UI package access remains
unresolved. WI-008 remains active at the Increment 3 review boundary.
