# WI-019 Location coordinates: delivery record

The user approved the implementation plan and 12 Scenario Review cases on
2026-10-02, then authorized each increment in turn: storage, Town maintenance,
Location defaulting and the Head Office Location display. All five increments
are complete; see the acceptance criteria and verification at the end.

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

## Location defaulting and optional Eircode lookup

Creating a Customer's first Location, adding a Location and changing a
Location's Town or Eircode now set its position automatically:

1. A position Confirmed on site is kept. No lookup runs and no history is added.
2. If the Location's Town has coordinates, the Location uses them with Town
   precision. No external lookup runs, even when an Eircode is entered.
3. If the Town has no coordinates and an Eircode is entered, the optional
   Eircode lookup may supply a position with Eircode precision.
4. Otherwise the Location saves with no position (Position needed). Lookup
   disabled, unavailable, timed out, not found or returning invalid data all
   end here. The Customer and Location are still saved together.

Name-only, Type-only and unchanged edits do not recalculate, look anything up
or add history. Replacing or clearing an automatic position keeps the previous
position, with its original Town/Eircode source, in history. Changing Town
coordinates later still does not move existing Locations; they update on their
next Town or Eircode change.

The store validates the request first, then resolves the position outside the
database transaction. Inside the transaction it re-reads the Location and Town,
and saves only if the Location's version, Town, Eircode and the Town's version
all still match what was resolved. Otherwise the save returns a conflict, and
nothing is written, including history. A slow lookup therefore cannot
overwrite another user's edit, a newly confirmed position or a Main Contact
change.

### Postcoder adapter (disabled)

`LocationCoordinates:Postcoder:Enabled` is `false` in appsettings, and no API
key is configured. While disabled or keyless, the adapter makes no request and
reports Unavailable. When enabled it:

- sends one request to Postcoder's Irish position endpoint, with a 5-second
  overall limit and a 3-second connect limit, no redirects and no retries;
- skips the request for input that cannot be an Eircode (not 7 letters/digits
  after removing spaces);
- accepts only a single result with a valid coordinate pair. Empty, ambiguous,
  partial or out-of-range results count as not found. Other provider errors,
  oversized or malformed responses and timeouts count as unavailable;
- removes HTTP logging and suppresses tracing, because Postcoder puts the API
  key in the URL path.

Live enablement remains blocked on MI-07: confirm the account's Eircode feature,
coverage and the right to store returned coordinates. Then supply
`LocationCoordinates__Postcoder__ApiKey` as an AppHost secret parameter
(`AddParameter(..., secret: true)`, like the existing secrets), never in
appsettings, and set `Enabled` to `true`. No provider was contacted or
purchased.

## Location record display (H-26)

The Head Office Location detail page shows the stored position below Type:

| Stored position | Map position | Precision |
|---|---|---|
| Town | `52.9234568, -6.2912346` | Town — approximate: the Town's position, not the shop's · set on 2 Oct 2026 |
| Eircode | `52.9387654, -6.2312345` | Eircode — from the Eircode lookup · set on 2 Oct 2026 |
| Confirmed on site | `53.1000000, -6.1000000` | Confirmed on site · set on 2 Oct 2026 |
| None | **Position needed**. No map position has been set for this location yet. A position is taken from the Town's coordinates, or failing that the Eircode, when the location is created or its Town or Eircode changes. | (row omitted) |

The coordinates above are test fixture values, not surveyed positions.
The page only reads the stored value through the existing BFF client; it has
no coordinate inputs, and edit requests still carry no coordinates. The
"Position needed" wording makes no claim about the Town, because Locations
created before WI-019 stay unpositioned even when their Town has coordinates.

WI-143 must add a separate authenticated confirmation operation; there is no
confirmation writer or GPS endpoint in this work item.

## Acceptance criteria and verification

> A Location with Eircode A67 X123 gets coordinates from the Eircode with
> Precision "Eircode" (S1)

Met as amended by the user's Town-first decision, which supersedes the
Eircode-first wording. Through the Head Office website, creating Hickey's
Pharmacies with Hickey's Rathdrum (Town Rathdrum, Eircode A67 X123, Type
Pharmacy) saves both records together. When Rathdrum has no coordinates, the
lookup is asked for exactly "A67 X123" outside any transaction, and the
Location saves its result with Precision Eircode, shown on the record. When
Rathdrum has coordinates, the Location uses them with Precision Town and no
lookup runs. Test: `Should_SaveTownDefaultOrOptionalEircodeFallback_When_FirstLocationIsCreated`
(both cases), using a lookup test double. The live Postcoder adapter stays
disabled until MI-07 is resolved. S1's No Visit Schedule and Unassigned lists
belong to other work items.

> A Location in Laragh with no Eircode gets coordinates from Laragh with
> Precision "Town" (S3)

Met. Adding a Location in Laragh with a blank Eircode through the add-location
page saves Laragh's coordinates with Precision Town, makes no lookup, and the
record shows "Town — approximate". Test:
`Should_SaveTownPrecision_When_LocationHasNoEircode`.

Every approved scenario has tests: 1–2 and 5 (defaulting and Position needed),
3–4 (unavailable/not found), 6 (recalculation and history), 7 (confirmed
protection), 8 (no noise on unrelated edits), 9 (Town entry and CSV),
10 (stale lookups), 11 (upgrade and restart) and 12 (access, antiforgery,
server-only credentials and no logged secret).

Final verification on 2026-10-03, after a `--no-incremental -warnaserror` build
with zero warnings and errors:

| Suite | Result |
|---|---|
| API | 192 / 192 |
| Web (`-- xUnit.MaxParallelThreads=4`) | 405 / 405 |
| Identity Admin | 1,046 / 1,046 |
| JavaScript (admin UI, console status, product unit form) | 34 / 34 |

No existing test was weakened, skipped or removed. The Web suite needs limited
parallelism on this machine: at the default it intermittently fails because
SQL Server test containers exit during start-up, before any test runs.

Open follow-ups, not blocking:

- Locations created before WI-019, and Locations saved as Position needed,
  only pick up Town coordinates added later when their Town or Eircode
  changes. If Head Office needs a way to refresh these, a per-Location
  "Update position" action is the smallest addition.
- Enabling the Postcoder lookup needs MI-07 resolved and an AppHost secret
  parameter for the API key.
