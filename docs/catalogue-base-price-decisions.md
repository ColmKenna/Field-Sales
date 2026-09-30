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
The handover notes contain the exact results. WI-008 remains active at the Increment 1 review stop;
no API or website add-price flow has been implemented yet.
