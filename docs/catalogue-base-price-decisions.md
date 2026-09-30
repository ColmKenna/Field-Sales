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
