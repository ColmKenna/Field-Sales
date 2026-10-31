# WI-041 — Resume an expired tablet Sync after sign-in

T-4.6.1 / Sign-in US-004 S1–S4 and Rep at a Location US-001 S3, from `main` at `6634c9b` on
`feature/wi-041-resume-expired-tablet-sync-after`.

## Approved scope and decisions

The developer approved the plan, decisions D1 to D7 and the Human-Led scenario matrix with **Approved** on 31 October 2026,
and reviewed each increment before the next.

| # | Decision |
|---|---|
| D1 | The interrupted Sync is carried by a one-shot resume request, made only when the rep signs in again from the Sync screen as the same rep. It names the rep and the session that sign-in started, lives in `TabletApp` memory (`SyncNotices`), and is honoured once by that session's Sync screen. |
| D2 | It is dropped when the sign-in fails or is cancelled, when another rep signs in, or when a later session starts; signal returning or the app resuming never makes one (T1.3); an app restart forgets it. |
| D3 | The resumed Sync is an ordinary Sync of whatever is waiting then: confirmed items are never re-sent, and a lost confirmation is re-sent and confirmed as already received (WI-039). |
| D4 | The Sync screen shows I-03's line with Sign in: "After sign-in, this Sync resumes automatically." |
| D5 | Without signal, Sign in says "Sign-in needs a connection. Try again when you have signal." and does not open the browser. |
| D6 | Capture with an expired online sign-in inside the local 24-hour window already works; it is characterised. |
| D7 | No server, API, identity-host or contract change. MI-62 untouched. |

## What was built

- `SignInAnswer(SignedInRep, Problem)` (Tablet.Core): what `TabletApp.SignInAsync()` returns, so a screen can tell a sign-in as the
  same rep from another rep's. `Entry.razor` reads only the problem, as before.
- `SyncNotices.RequestResume(rep, version)` and `TakeResume(rep, version)`: one-shot, for the exact rep and session.
- `Sync.razor`: after Sign in, an answer naming the same rep in a new session records the request; the Sync screen takes it when it
  opens or when the request arrives later (returning from the browser rebuilds the page, so either order happens) and runs the
  normal Sync. I-03's line shows with Sign in. Without signal, Sign in shows the explanation and keeps it across a rebuild.

## Acceptance criteria

1. **"Expired sign-in at Sync uploads nothing and shows 'Sign in again to send 3 items' while all 3 remain stored (US-004 S1)."**
   WI-040's `Should_UploadNothingAndAskToSignInToSend3Items_When_MySignInHasExpired` (real token source), WI-041's R1 and R3; on the
   Samsung, "Sign in again to send 2 items" with nothing received.
2. **"After successful sign-in, that Sync resumes automatically and each item is sent once as confirmed (S2)."** R1 (each item once,
   then the day), R2 (a confirmation lost before the expiry: re-sent, stored once, Sent once), screen C1 in all three orderings of the
   answer and the page rebuild (exactly one resume). On the Samsung: signed in at 16:51:03; both orders received at 16:51:06 without
   tapping Sync, once each, Pending, hashes matching.
3. **"No signal or failed sign-in leaves the Unsent queue intact (S3); local capture continues within the 24-hour window (S4)."**
   R3 and screen C2 (failed or cancelled sign-in: queue, count, sentence and Sign in kept, no resume), C5 (no signal: explanation,
   no browser), R4 (capture inside the window, sent at the next signed-in Sync). On the Samsung with Wi-Fi off: the explanation,
   2 items still unsent.

## Scenario matrix and tests

| Area | Tests | Where |
|---|---|---|
| Core R1–R4 | 5 cases (R3 a theory) | `tests/Integration/FieldSales.Tablet.Core/TabletSyncResumeTests.cs` |
| Screen C1–C7 | 10 cases (C1 three orderings, C2 failure and cancel) | `tests/Integration/FieldSales.Tablet.Components/TabletSessionScreenTests.cs` |
| Resume request | 4 unit tests | `tests/Unit/FieldSales.Tablet.Core/SyncNoticesTests.cs` |

I1 locked today's behaviour first (R1–R4, C4); all passed before any change. Two WI-040 screen tests moved to `SignInAnswer`.

## Mutation checks

- No resume request: C1 failed in all three orderings. Not taking it on opening: only C1 "between" failed. Not taking a later
  request: only C1 "after" failed. Any rep resuming: C3 failed. Not one-shot: C1 ×3 and the unit test failed.
- Sign in without the signal check: C5 failed.

## Final verification

- `dotnet build FieldSales.slnx -c Release -m:1 --no-incremental`: 0 warnings, 0 errors.
- `dotnet test FieldSales.slnx -c Release --no-build -m:1 --settings sqlserver.runsettings`: **2,868 passed, 0 failed, 0 skipped**
  (baseline 2,849, so 19 new): Tablet Core integration 205, Tablet Components 69, Tablet Core unit 16, API integration 174, API unit
  185, Web integration 873, Web unit 219, Identity Admin integration 804 and unit 256, DemoData 4, SnapshotSpike 22, TabletSpike 41.
- `scripts/tablet-app.ps1 -Action Build -Configuration Release -DevTrust`: the signed Android APK builds.
- `node --test plan_docs/field-sales-delivery/tests/status-store.test.js`: 11 passed; the only console change is `WI-041` → `done`.

**Device check (Samsung SM-P610, demo AppHost, 31 October 2026):** two orders Ready; the tablet's refresh grant deleted after its
access token had expired. The Sync screen showed "Sign in again to send 2 items", I-03's line and Sign in. With Wi-Fi off (adb
`svc wifi disable`) Sign in showed "Sign-in needs a connection. Try again when you have signal." and opened nothing. With Wi-Fi back,
the developer signed in; three seconds later both orders reached the server without a tap, once each. The tablet was closed while
the day was downloading, so the download's completion was not seen on the device (it is covered by R1 and C1).

## Left for later items

- MI-62 (a disabled account with unsent work) stays undecided; WI-042 (Needs Attention); Sync reminders.
- The resume request is in memory only: if the app is killed between the sign-in and the Sync screen opening, the rep taps
  Sync now (D2).
