# WI-048 — Home record-of-the-day checkpoint

T-4.5.1 / REP-US-003 S2 and S5. Branch:
`feature/wi-048-show-home-as-record-day`, from `main` at `e2ffd43`.
The developer approved the plan and Apply on 3 November 2026. Dependencies
WI-032, WI-035 and WI-047 are done and present on that integration base.

## Approved boundaries

1. Visit planning is absent and outside this slice. Production supplies no
   planned rows: today's local activity appears as unplanned, and a saved Call
   marks the shop Done. An injected planned Murphy's row is permitted only in
   the S2 checkpoint fixture, demonstrating future ordered-plan composition.
2. Orders are selected by `TakenOn`. The displayed **Saved HH:mm** is their
   existing `SavedUtc`, converted to tablet-local time; edits change that time.
   No original-capture timestamp is invented or added to a contract.
3. Calls use `CalledOn` and their captured `SavedAt`. Today's Sent work remains
   in the record. The Unsent count covers the entire queue, including older
   work, and only a positive count is a link.

The original agent-assisted checkpoint remains mandatory. Resume only on
**Continue T-4.5.1**. WI-048 is `active`, not complete.

## What is ready to review

- `HomeRecordReader` projects the existing local Calls, Orders and Sent work.
  One SQLite UNION statement reads both sides of the atomic move to Sent,
  including rejection/claim metadata. There is no new schema, migration,
  captured field, network call or persisted Home cache.
- Supplied planned rows retain their order. Unplanned rows are grouped by Town
  and sorted by Location name. A Call makes a row Done; an Order alone does
  not. Every order retains its own current state and saved time.
- Home keeps Sync, Products and Search in reach. The old assigned-shop list is
  replaced by today's activity; Search still finds assigned shops. Shop cards
  link to the existing Location route and have dominant names and at least
  80px touch targets.
- The day read runs off the UI thread. Session/navigation/disposal checks stop
  stale results from being published. A failed record read preserves available
  download metadata and the Search/Products/Sync routes. Unreadable legacy
  activity produces a neutral notice while keeping usable activity and count.
- Location names come from the existing assigned/retained identity rules.
  Missing identities use the existing "Shop details unavailable" fallback;
  this slice does not expand retention or grant new-work permission.

## Extension points

`HomeDayRecord.Counters` is rendered by `HomeExceptionStrip`; each counter has
an id, label, value, target and optional detail. `HomeDayRecord.Sections` is
rendered in supplied order by `HomeDayView` and `HomeRecordSection`.

The checkpoint adds `Fixture counter: 2` to the same strip and a `Fixture
section` after the existing sections solely by appending DTO values. Existing
rendering paths are unchanged. These examples and fixture controls live only
in the ignored desktop preview, never in the production Home.

This leaves composition points for future visit plans, Overdue, Due soon,
not-supplied counters and customer-request sections without implementing them.
There is no fake production Scheduled Visit source.

## Source criteria at this checkpoint

| Criterion | Evidence |
| --- | --- |
| Call + Ready to Send Order at Murphy's at 09:40 shows Done with the Order/state beneath | Core S2 test, production Razor rendering test and `checkpoint-s2.png` |
| Nolan's unplanned Order appears at 11:15 with its state | Same S2 tests and browser assertion for Saved 11:15 |
| No Scheduled Visits shows "No visits scheduled today", Search and unplanned activity | Production-mode fixture, component tests and `checkpoint-production.png` |
| Unsent is in one exception strip | One navigation strip, queue-wide count, zero-count test and browser checks |
| Large touch targets with dominant Location names | Real production CSS, 80px minimum cards and browser checks at 800x1280, 1280x800 and 360x800 |
| Dummy counter and section fit without changing existing paths | DTO append test and `checkpoint-extensions.png` |

## Verification so far

Characterization came first, before changing behavior: two tests confirm Call
date/time/body retention on confirmation and the Order's unchanged business
date with an updated saved time on editing.

- Complete Tablet Core integration suite: **284 passed, 0 failed, 0 skipped**
  (includes 11 new characterization/day-record cases).
- Complete tablet component suite: **131 passed, 0 failed, 0 skipped**
  (includes six new view/extension/error/stale-read cases).
- **16 browser assertions passed** using the production Home page, shared
  components and CSS over real SQLite records created by CallStore/OrderStore.
  The desktop adapter supplies the session and approved fixture plan. No
  browser console warnings or errors were reported.
- Complete Release solution build: **0 warnings, 0 errors**. Console status
  tests: **11 passed**. Parsed comparison confirms only WI-048's status changed
  in the console; `git diff --check` passed.
- Initial fixture failures were corrected: price dates must remain unique,
  and a Ready to Send Order must be reopened before an edit. Production rules
  and assertions were not weakened.

Local review artifacts:

```text
.artifacts/wi048/checkpoint-s2.png
.artifacts/wi048/checkpoint-production.png
.artifacts/wi048/checkpoint-extensions.png
.artifacts/wi048/checkpoint-browser-check.js
.artifacts/wi048/checkpoint-browser-result.json
.artifacts/wi048/checkpoint-tests/*.trx
.artifacts/wi048/checkpoint-build.log
```

The preview is not an Android/device validation. The complete application test
suite, Android publish, remaining date/time/recovery checks and final acceptance
report are still required after the checkpoint is released. Do not mark done
or propose integration at this checkpoint.

## Scope and missing information

Out of scope: Scheduled Visit persistence, Overdue/Due soon behavior, deadlines,
website counts, cover/left visits, visit sequence, not supplied, customer
requests, MI-62, and changes to Sync/capture rules or database schemas.

Required values are resolved: repository `D:/repos/Field-Sales`; native source
`src/FieldSales.Tablet` and `src/FieldSales.Tablet.Core`; integration branch
`main`. The canonical console status map is `workItemStatus` rather than inline
`status-json`, as established for this console. Only WI-048's entry is changed.
The user's three unrelated untracked review files remain untouched.

Provisional commit messages are reconciled to the actual diff: derive the day
record from saved/Sent work, then render Home with extensible counters and
sections. Each commit carries `Refs: WI-048, T-4.5.1, REP-US-003`.
