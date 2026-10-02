# WI-019 coordinate storage checkpoint

The user approved the implementation plan and 12 Scenario Review cases on
2026-10-02. This document records the first implementation increment; the
complete Town-first feature is still in progress.

## Accepted behaviour

1. Keep a position Confirmed on site when address inputs change.
2. Otherwise default to the Town's coordinates. A Town pin avoids an external call.
3. When the Town has no pin, optional server-side Eircode lookup may supply one.
   The planned Postcoder adapter stays disabled initially; no service is purchased.
4. If neither source supplies a pin, save the Location and show Position needed.
5. Head Office maintains Town coordinates manually or by CSV. Changed Town or
   Eircode recalculates unconfirmed positions and retains replaced pins in history.

These human decisions supersede the source's Eircode-first S1 and its assumption
that every new Location necessarily receives a map position. GPS capture and
reversion remain WI-143; map display remains WI-110.

## Storage delivered in this increment

- `Locations` receives nullable latitude, longitude, precision, source Town ID,
  source Eircode and positioned-at fields. An unavailable position has all six
  fields null. Zero is a valid coordinate, not a missing-position marker.
- Coordinates use decimal degrees with seven decimal places. Latitude must be
  between -90 and 90; longitude between -180 and 180. The value object validates
  before rounding to the database's precision. SQL constraints also reject
  partial positions, out-of-range coordinates and invalid precision/source pairs.
- Precision is Town, Eircode or Confirmed on site. Numeric storage scale does not
  imply physical accuracy; the precision label identifies the source.
- `Location.ApplyDefaultPosition` accepts only Town/Eircode defaults or a missing
  position. It preserves Confirmed on site, rejects incompatible inputs, and
  avoids changing timestamps/history when coordinates and source are unchanged.
- `LocationPositionHistory` stores replaced coordinates, precision, original
  source and positioned/replaced times. Replacing or clearing a pin appends its
  previous value in the same save transaction. Assigning a first pin creates no
  fictional previous position. Historical source IDs/text are snapshots, not
  navigations that would resolve to the Location's later address.
- The Location's existing rowversion protects coordinate updates. A concurrency
  failure rolls back both the new position and its history insertion.
- The shared Location detail/summary contract exposes an optional `Position`
  containing coordinates, precision and positioned time. Existing callers can
  omit it. Request payloads gain no coordinate or GPS override fields.
- Migration `20261002152415_AddLocationPositions` is additive. Existing Locations
  remain unpositioned until the later defaulting integration; customer ownership,
  geography, Type and Main Contact relationships retain their existing guards.

## Verification

Before modification: all 171 existing Customer/Location, Contact, geography,
Type and reference-list characterization tests passed.

Added tests cover boundaries including zero, rejection before rounding, upgrade
from WI-018 with an existing Main Contact, null read contracts, persisted precision
and history after reopening the context, no-op defaulting, clearing a pin,
confirmed-position protection, SQL constraints and atomic concurrency rollback.
Final regression results: 162 API, 1,046 Identity Admin and 376 website tests
passed (1,584 total); 34 JavaScript tests passed. Build: zero warnings/errors.
The first full run exposed seven test input conversion errors, fixed without
changing production code; the complete API rerun passed. Detailed evidence is
recorded in the WI-019 handover note.

## Review and next increment

Review the representation of missing/confirmed positions and the retained
history above. No business question remains open. The next increment is Town
coordinate entry and CSV support, retaining the original three-column format
and adding `Region,County,Town,Latitude,Longitude`.

The later resolver must run external requests outside long SQL transactions,
then recheck Location and Town versions/inputs before applying a result. It must
call the common defaulting method, preserve confirmed pins and skip unrelated
edits. WI-143 must add a separate authenticated confirmation operation; there is
no confirmation writer or GPS endpoint in this increment.

Town maintenance, defaulting on creation/address edits, external lookup and the
Head Office position display remain to be implemented. The full work-item
acceptance criteria have not yet been met.
