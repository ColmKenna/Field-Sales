# WI-048 — Home as the record of the day

T-4.5.1 / REP-US-003 S2, S5. Built on
`feature/wi-048-show-home-as-record-day` from `main` at `e2ffd43`.
The approved plan, boundaries and rendered review are recorded in
[the checkpoint report](tablet-home-record-checkpoint.md).

## Delivered behavior

Home reads the capturing rep's local Calls and Orders for the tablet's current
business day, including work already confirmed as Sent. A saved Call marks its
shop Done. Each Order shows its own state, line count and latest saved time.
Unplanned activity is grouped by Town and sorted by Location name. Supplied
planned rows keep their supplied order, including when they become Done.

The developer explicitly approved two boundaries before implementation:

- Visit planning is not available yet. Production supplies no planned visits,
  shows "No visits scheduled today", and keeps today's activity under
  Unplanned today. Only the S2 review fixture injects a planned Murphy's row.
- Orders use the existing `TakenOn` business date and `SavedUtc` timestamp.
  Editing changes **Saved HH:mm**; no original-capture time is invented.

The exception strip shows the complete Unsent queue, including earlier days'
work, with the existing Needs Attention detail when applicable. A positive
count opens Sync; zero is plain text. Search, Products and Sync remain in the
header and remain reachable if the day record cannot be read.

## Storage, access and extension points

`HomeRecordReader` and `UnsentStore.Home.cs` read the current storage format.
One SQLite statement reads queued and confirmed records together, so the atomic
move to Sent cannot momentarily duplicate or omit an item. No database schema,
wire contract, captured payload or Sync/capture rule changes.

Call dates/times come from `CalledOn`/`SavedAt`; Order dates/times come from
`TakenOn`/`SavedUtc`. Local-time conversion uses the existing TimeProvider.
State reflects Sent, an outstanding upload claim, a technical refusal, or the
captured In Progress/Ready to Send state, in that order.

Reads run off the UI thread. Session version, load and disposal checks prevent
old results from repopulating a different session. Existing per-rep workspace
and assigned/retained Location rules remain authoritative. If an identity is
no longer available, its activity remains with "Shop details unavailable".
Unreadable legacy activity keeps its queue count and produces a neutral notice
without exposing raw payloads or technical errors.

`HomeDayRecord.Counters` feeds `HomeExceptionStrip`; `Sections` feeds
`HomeDayView`/`HomeRecordSection` in supplied order. An optional
`AdditionalSections` content slot also lets a later feature provide its own
section model and interactions after the day record. The review appended a dummy
counter to the same strip and a dummy section after the real sections solely
through DTO values. Future counters/sections can use these boundaries without
new branches in the existing rendering paths. Review controls and sample
extensions are confined to ignored preview artifacts.

## Acceptance verification

| Criterion, quoted from the handoff | Evidence |
| --- | --- |
| "After saving a Call and a Ready to Send Order at Murphy's Pharmacy at 09:40, Home shows Murphy's Pharmacy marked Done with its Order and state beneath." | Core S2 projection and actual Razor rendering tests; `checkpoint-s2.png`. The fixture supplies Murphy's planned position, as approved. Production also shows the saved Call as Done in unplanned activity. |
| "A phone Order from Nolan's Pharmacy (not scheduled) at 11:15 appears under Today as unplanned, at 11:15, with its Order state." | S2 store/renderer tests and browser assertions show Nolan's under Unplanned today with Ready to Send and Saved 11:15. |
| "With no Scheduled Visits, Home shows 'No visits scheduled today' and still shows search and unplanned activity." | Production-mode fixture, component test and `checkpoint-production.png`; empty-day test confirms assignments alone are not invented visits. |
| "The Unsent count appears in a single exception strip, which later features add counters to." | Global count includes prior-day and unreadable work; Sent confirmation removes only the queue count. Zero-link and extension tests plus one-strip browser assertion. |
| "Rows are large one-handed touch targets with the Location name dominant." | Production CSS gives cards an 80px minimum and dominant names. Browser checks passed at 800x1280, 1280x800 and 360x800 without horizontal overflow. |

Self-verification: criteria 1–5 are demonstrated; a dummy counter and section
append without changing existing code paths; commit summaries describe the
actual read projection, interface and verification changes.

## Verification stages

- Characterization passed before behavior changes: Call date/time/body survives
  confirmation; an edited Order keeps its business date while saved time changes.
- The named rendered checkpoint was committed and pushed as `089df04` and
  `69851a7`. The developer then explicitly authorized batching the remaining
  work with a commit between completed stages.
- Date/recovery checks passed and were committed as `525fbd8`: local midnight
  while UTC remains on yesterday; latest saved time after reopening; empty day;
  missing Sent identity; and same-rep restoration through the production facade.
- The checkpoint browser checks passed all **16 assertions**, with no browser
  console warnings/errors. They ran the actual Home components and production
  CSS over records created by the real stores through a desktop session adapter.

### Final results

- Complete Release solution build: **zero warnings and zero errors**.
- Complete application suite: **3,111 passed, 0 failed, 0 skipped** across
  12 test projects, using `sqlserver.runsettings` and Docker. Results:
  `.artifacts/wi048/full-suite-results/`; parsed summary:
  `.artifacts/wi048/full-suite-summary.json`.
- After that suite, the independent `AdditionalSections` composition slot was
  added. The affected tablet component suite was rebuilt and rerun: **132
  passed, 0 failed, 0 skipped**. All other project code was unchanged. This
  yields **3,112 passed** when taking the latest passing result per project.
- Admin, Web, tablet JavaScript and console status checks: **38 passed**.
  Demo reset script checks: **5 passed** in a cached offline Python container
  with normalized temporary copies and mocked Docker calls.
- Checkpoint browser verification: **16 assertions passed**, including the
  S2 day, empty production plan, sample extensions, touch sizes and three
  screen widths. The browser reported no warnings/errors.
- Signed Android Release publish succeeded without compiler warnings. The APK
  contains both `arm64-v8a` and `x86_64` native libraries. Its Home stylesheet
  and entry page match the final source bytes. Artifact:
  `.artifacts/wi048/android/com.fieldsales.tablet-Signed.apk`; SHA-256:
  `58530384bce4f8f30970673c10611461ac1b8c539116158e8879b4a867ad52b8`.
  Logs: `.artifacts/wi048/final-build.log` and
  `.artifacts/wi048/android-build.log`.
- The full solution run precedes the optional section slot; the affected
  component rerun and Android publish both include that final change. No other
  project uses the slot. No physical device installation or execution was
  performed.

`git diff --check` passed, and a parsed comparison against the integration
base confirms the only console-content change is WI-048's status to `done`.
The feature branch is the delivery boundary; integration remains the
developer's decision. The next incomplete item is WI-049.

## Scope and limitations

Out of scope: Scheduled Visit persistence, Overdue, Due soon, deadlines,
website counts, cover/left visits, visit sequence, not supplied, customer
requests, MI-62, and changes to identity, Sync or capture rules.

No required value remains unresolved. The canonical console map is
`workItemStatus`; only WI-048's progress entry changes. No physical tablet
installation/execution is claimed by the desktop checks or Android publish.
The three unrelated review documents remain untouched.
