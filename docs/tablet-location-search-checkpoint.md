# WI-034 — offline Location search checkpoint

Measured 28 October 2026. The developer approved this checkpoint with `Continue T-4.2.1`.
Final verification and completion are recorded in [the delivery report](tablet-location-search-delivery.md).

## Delivered before this stop

`LocationSearchIndex` projects Locations from a single active SQLite generation into an immutable,
rep-local memory index. A read transaction binds the generation and its rows. Names are decomposed
and normalized for case, accents and straight/curly apostrophes; queries are literal trimmed
substrings. Blank input returns no results, and the screen does not invoke the search for blank
or whitespace input. No snapshot field, version, SQLite schema or API changes.

The search screen reads off the UI thread, invalidates old work when input changes, and uses
virtualized 80 CSS-pixel result links. Names are dominant; Town is secondary. The assigned-only
explanation appears only with a nonblank query returning no matches. Result links open the
approved minimal name/Town destination. WI-035 owns the remaining Location screen.

Entry and resume reuse the existing current-rep / 24-hour guard. Refresh clears the index,
query and results before loading the current rep's active snapshot. Closure labels/scenarios
are deferred by explicit developer approval because the domain/snapshot have no closure state.

## Device and method

- Physical Samsung Tab S6 Lite SM-P610, Android 13, arm64-v8a, USB serial `R52T30AZZXZ`.
- Release dev-trust APK from `bda588d`; SHA-256
  `6B3F84017F94B74F481CA55DBE2626D5A15AC6A86DB54812437EC96C5C0CDD30`.
  Installed over the existing app; no data reset. Packaged timing JS matches source byte-for-byte.
- Exactly **500 assigned Locations**, alongside 25,003 products, 803 categories, 75,004 prices
  and 75,005 attribute rows. Fixture Sync completed in 52,650 ms.
- Wi-Fi disabled and both identity/API reverse routes removed for every search observation.
  Reads and navigation therefore used the downloaded snapshot.
- The browser captures `performance.now()` at the input event. After the result render,
  instrumentation waits for a virtualized row (for nonempty results) and two animation frames.
  This includes WebView event dispatch, filtering, rendering and an opportunity to paint.
  It is an application timing measure, not a hardware display-photon measurement.
- Separate native Stopwatch handler timings are retained. Logs contain match counts and durations,
  without search text or Location names. The fixture oracle and UI XML separately verify results.

## Measurements

Index build (SQLite read, normalization, sorting and worker continuation included):
**122.149 ms** on first entry; **53.221 ms** when reopening search.

| Final query in each trial | Matches | Input to visible result (ms) |
|---|---:|---:|
| Murphy | 2 | 86.2 |
| Quinn | 0 | 80.7 |
| cafe | 1 | 103.2 |
| pharm | 1 | 91.0 |
| shop | 496 | 90.3 |
| 049 | 1 | 107.3 |
| zzzz | 0 | 66.3 |
| Murphy's | 2 | 97.2 |
| %_ | 1 | 110.1 |
| Murphy (navigation check) | 2 | 109.6 |

All **16 completed observations**, including intermediate keystrokes, are retained. Minimum
66.3 ms; median 94.1 ms; maximum **110.1 ms**. No observation reached one second, and no failed
timing was replaced by a rerun. The broad query rendered 496 matches using virtualization.
The worst measured search consumes 11.01% of the one-second budget, leaving 889.9 ms of margin
on this device and fixture. Larger Location counts have not been measured in this item.

## Correctness evidence

- Murphy shows the two expected names; straight-apostrophe search also matches the curly form.
- `cafe` finds `Café de Santé` without changing displayed accents.
- Partial names, numeric fragments and literal `%_` return the oracle's exact counts.
- Quinn displays `No assigned locations match 'Quinn'` and
  `Only locations assigned to you can be searched on the tablet.`
- Clear and two spaces produce no rows, no no-match explanation, and no additional search
  timing events. The initial blank page also shows neither results nor that explanation.
- Tapping the first large row opens its name/Town destination; Back reopens search with an
  empty query and rebuilds the current generation's index.
- The device fixture's named shops both use Swords. The exact source towns Rathdrum/Aughrim
  and the 312-Location source scenario are covered by the approved automated Core test.
- Characterization passed before production changes. Full Core project: **86 passed, 0 failed,
  0 skipped**, including 14 new cases. Release Android build passed. Full application-suite
  verification remains after checkpoint approval.

Ignored raw evidence: `.artifacts/wi034-device/trials.json`, numbered trial XML/log files,
`final-timings.log`, `clear.xml`, `spaces.xml`, `murphy.png`, `detail.png`, and `reopened.xml`.

## Cleanup and remaining work

All six temporary demo row changes were restored from their pre-change backup. A second Sync
completed in 50,567 ms and restored the normal 502-Location snapshot. Wi-Fi is back to its
original enabled-in-airplane-mode state (`wifi_on=2`); the original empty reverse-route list
and 120,000 ms screen timeout are restored. The owned demo AppHost was stopped. Docker Desktop
remains running; no volumes, application data or unrelated files were removed.

At the checkpoint WI-034 remained `active`. The subsequent approved continuation completed
guard/stale-input verification and the complete application suite: see the delivery report.
WI-034 is now `done`. Stop before integration. No WI-035 implementation beyond the explicitly
approved minimal destination, and no next work item is started.
