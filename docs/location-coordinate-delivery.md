# WI-019 coordinate storage and Town maintenance checkpoints

The user approved the implementation plan and 12 Scenario Review cases on
2026-10-02, then said Continue to proceed from storage to Town maintenance.
The complete Town-first feature is still in progress.

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

## Town coordinate entry and import

Head Office can supply coordinates when creating a Town or use Edit coordinates
on an existing Town in the geography list. Each coordinate form carries the
Town's current version. Both latitude and longitude are required together;
leaving both blank on that form clears the Town pin. Invalid inputs remain in
the open form with an error. Editing a Town pin never bulk-moves Location pins.

Migration `20261002195119_AddTownCoordinates` adds a nullable coordinate pair to
Towns with SQL range/pair checks. Stable identities, names, archive flags and
hierarchies remain unchanged. Town choices/reference reads expose the pair;
archived hierarchy remains excluded from new choices.

CSV supports two exact headers:

```csv
Region,County,Town
```

```csv
Region,County,Town,Latitude,Longitude
```

Existing three-column files retain their existing behaviour and preserve any
Town coordinates. In five-column files, supply both values using decimal dots;
leave both blank to preserve an existing Town pin or leave a new Town unpositioned.
Blank CSV values do not erase existing pins. Zero is valid. Identical repeats do
not change IDs, coordinates or versions. Conflicting supplied positions for the
same Town reject the whole file, as do malformed or out-of-range values.

The importer validates and saves the complete file atomically. Supplied
coordinates may update existing Towns under archived geography while preserving
their archive flags; adding a new Town beneath archived geography remains
rejected. The result reports how many existing Town positions changed.

A blank coordinate template is saved in docs/examples/town-coordinates.csv.
There is no real coordinate dataset or external provider configuration loaded.
The displayed Laragh coordinate example in the tests is fixture data only.

All routes retain Head Office/current-role/scope checks and browser antiforgery.
The Town forms and import communicate through the existing server-side BFF.

Check-constraint SQL for Towns, Locations and position history is normalized
to LF line endings in the model. The model then matches the migration
snapshot on Windows and Linux checkouts alike; a model test guards this.

## Review and next increment

Review the Town form and the file format above. No business question remains
open. The next increment connects Town-first defaulting and the optional
disabled-by-default Eircode adapter to Location creation/address edits.

The later resolver must run external requests outside long SQL transactions,
then recheck Location and Town versions/inputs before applying a result. It must
call the common defaulting method, preserve confirmed pins and skip unrelated
edits. WI-143 must add a separate authenticated confirmation operation; there is
no confirmation writer or GPS endpoint in this increment.

Defaulting on Location creation/address edits, external lookup and the Head
Office Location position display remain to be implemented. The full work-item
acceptance criteria have not yet been met.
