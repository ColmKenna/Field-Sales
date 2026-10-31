# WI-040 — Say why a sync didn't complete

T-4.1.3 / REP-US-001 S2, S3, the no-connection edge case and the progress note, from `main` at `abba19f` on
`feature/wi-040-say-why-sync-didn-t`.

## Approved scope and decisions

The developer approved the plan, decisions D1 to D8 and the Human-Led scenario matrix with **Approved** on 30 October 2026,
and reviewed each increment before the next ("continue").

| # | Decision |
|---|---|
| D1 | Causes are decided on the tablet. **Sign-in expired:** no token, a refused refresh or a 401; nothing more uploads and the day doesn't download. **No connection:** nothing in that Sync got an answer. **Poor connection:** the connection failed during the uploads after something got through (an item was confirmed, or the day downloaded afterwards). **Server error:** an error status, or a confirmation that doesn't name the item; the uploads stop and the download is still tried. Items refused as technical faults keep WI-039's sentence until WI-042. |
| D2 | US-001 S2 verbatim: "2 items not sent — poor connection. They'll send next Sync."; one item: "1 item not sent — poor connection. It'll send next Sync." The T-01 wireframe's "Try again when you have signal." loses to the story. |
| D3 | US-001 S3: "Sign in again to send 3 items", counting the finished work still waiting (saved Calls and Ready to Send Orders); "1 item"; "Sign in again to sync." when nothing waits. A **Sign in** button replaces Sync now and starts the entry screen's connected sign-in. Resuming the Sync afterwards is WI-041. |
| D4 | Server error: "2 items not sent — Field Sales didn't respond. They'll send next Sync." |
| D5 | The download after a failed upload is still tried in the same Sync, never automatically later (T1.3). "Last synced" shows whether the day downloaded. One sentence about the connection is enough. |
| D6 | Work went, day didn't: "3 items sent." then "Your day didn't download — no connection." / "Your day didn't download. Sync again to try." / "There isn't enough free space on this tablet to download your day." / "Sync was cancelled before your day downloaded." No message says saved work is unchanged once something was sent; when nothing was sent, today's messages stay. |
| D7 | Progress "Sending 2 of 3…" was delivered by WI-039; this item adds a test at a dropped connection. |
| D8 | Tablet only: no API, server or snapshot-contract change. Confirmed items move to Sent at once and every Sync reads the queue fresh, so a repeated or resumed Sync never re-sends a confirmed item (the resume point WI-041 needs). |

## What was built

**Tablet logic (`FieldSales.Tablet.Core`)**

- `WorkUploader`: `UploadStop` now separates `ConnectionLost` (no answer: no route, refused, dropped, timed out) from
  `ServerError` (an error status, or a receipt naming another item). `UploadSummary(Total, Sent, Stop)` carries the reason, and
  `UploadAsync` returns the summary.
- `TabletSync`: an expired sign-in reports the finished items waiting (`new(waiting, 0, SignInRequired)`), counted with
  `WorkUploader.CountWaiting` (0 if the Unsent database can't be read). The Sync sequence (WI-039 D8) is unchanged.
- `SyncMessages`: `PoorConnection`, `SignInToSend`, `ServerError` and `Sent` sentences, and `Explain(outcome)`, which returns
  `SyncReport(Work, Day)`: one sentence about the rep's finished work and one about their day, either of which may be absent.

**Tablet app (`FieldSales.Tablet`)**

- `Sync.razor` shows the report's two lines. When the sign-in has expired, **Sign in** takes Sync now's place, calls
  `TabletApp.SignInAsync()` once (a second tap starts nothing) and shows whatever it reports. Success changes the session, so the
  page reopens for the signed-in rep with Sync now.
- `SyncNotices` (Tablet.Core, held by `TabletApp`): the Sync screen's last notice (report, Sign in state, sign-in answer) for the
  rep and session it was said in. Returning from the sign-in browser resumes the app, and `MainLayout` rebuilds the page on every
  resume; the rebuilt page shows the same notice and hears a sign-in answer that arrives later. A new Sync, Back to Home or a new
  session (a successful sign-in) starts without one. Added after the device check found the rebuild (developer's choice,
  31 October 2026).

No server, API or snapshot contract change.

## Acceptance criteria

1. **"Signal dropping after the server confirms 1 of 3 items leaves that item Sent and the other 2 unchanged, with "2 items not
   sent — poor connection. They'll send next Sync."; the download is attempted when signal allows (S2)."**
   `TabletSyncFailureTests.Should_MarkTheConfirmedItemSentKeepTheOther2AndSayPoorConnection_When_SignalDropsAfterTheServerConfirms1Of3`:
   the first item is Sent, the other two are byte-identical in Unsent work, the report is exactly that sentence with no second
   connection line, the download was attempted and the morning's day stays active.
   `Should_DownloadTheDayAndStillSayPoorConnection_When_SignalAllowsTheDownloadAfterTheDrop`: when the signal allows, the day
   downloads in the same Sync and the sentence still shows; the rep stays on Sync to read it (screen C1).
2. **"With sign-in expired, nothing uploads and "Sign in again to send 3 items" appears with a Sign in action; all work stays on
   the tablet (S3)."** `Should_UploadNothingAndAskToSignInToSend3Items_When_MySignInHasExpired` uses the real token source with the
   identity host refusing the refresh: no upload and no download request, the Unsent fingerprint is unchanged, and the report is
   "Sign in again to send 3 items". Screen C2 shows **Sign in** in place of Sync now; C3 shows it starts the connected sign-in once
   and shows its answer; `Should_KeepTheSignInStateAndShowTheAnswer_When_ThePageIsRebuiltDuringSignIn` shows the sentence, Sign in
   and a later answer survive the page rebuild that returning from the browser causes. Confirmed on the Samsung.
3. **"Progress shows "Sending 2 of 3" (non-functional)."** Delivered by WI-039 (screen C1 there, and "Sending 1 of N…" from the
   tap); `Should_ReportSending2Of3ThenStop_When_TheConnectionDropsOnTheSecondItem` adds the drop: 1 of 3, 2 of 3, then finished.

## Scenario matrix and tests

| Area | Tests | Where |
|---|---|---|
| Causes (I1) | 8 cases: drop after 0/1/2 confirmations, 500/503/404, a receipt naming another item, a real expired refresh | `tests/Integration/FieldSales.Tablet.Core/TabletSyncFailureTests.cs` |
| F1–F12 | 19 cases (F10 and F11 four each) plus a 401 at the download after every item went | same file |
| Screen C1–C4 | 5 cases | `tests/Integration/FieldSales.Tablet.Components/TabletSessionScreenTests.cs` |
| Kept notice | 3 screen tests (page rebuilt during sign-in keeps the sentence and shows the later answer; a new session starts with Sync now; Back to Home forgets it) and 5 unit tests | same file; `tests/Unit/FieldSales.Tablet.Core/SyncNoticesTests.cs` |

**Existing tests that changed.** Five WI-039 tests (7 cases) compared upload summaries after a stop; they now assert the stop
too, and "upload nothing without a token" expects the 2 waiting items instead of zero. Run against the new code before the
update, exactly those 7 cases failed. WI-039's not-sent screen theory needed no change.

**Fault harness (test-only).** `Faults.NoSignal()` and `Faults.ExpiredSignIn()`, `FakeReceiver.DropAfter(n)` and
`FailFrom(arrival, status)`, the existing `SessionTablet` fake identity host, and `SynchronousProgress` (moved from WI-039's tests
to the shared support). Production Sync has no test hooks.

## Mutation checks

- A lost connection reported as a server error failed 3 tests; dropping the waiting count on an expired sign-in failed 2.
- Dropping "or the day downloaded" from the poor-connection rule failed F8; dropping the one-connection-sentence rule failed F1
  and the screen's no-connection case.
- Dropping the item count from the sign-in sentence failed 3. Removing the Sign in double-tap guard first made C3 hang (the
  second tap awaited the pending sign-in); C3 was reworked to await both taps after the answer, after which it fails in 49 ms.
- The no-connection rule, the server-error sentence and the D6 day lines each failed their tests (F7; F9; the four F10 cases).
- Kept notice: a rebuilt page ignoring it failed two screen tests; not keeping the sign-in answer failed the rebuild test;
  Back to Home not forgetting it failed its test.

## Final verification

**Device check (Samsung SM-P610, demo AppHost, dev-trust Release APK, 31 October 2026)**

| Check | Tablet | Server |
|---|---|---|
| Sign-in expired: refresh grant deleted in `FieldSalesDemo_IdentityOperationalDb`, access token past its hour, two orders Ready | "Sign in again to send 2 items" with **Sign in** in place of Sync now; Unsent 2 | nothing received |
| Signed in again, then Sync now | both sent, Home | 2 rows, Pending, resent 0, hashes match |
| No connection: API forward removed **and the app restarted cold** (a live pooled connection otherwise carries the Sync through) | only "No connection — your work is saved on this tablet"; Unsent 1; Last synced unchanged | nothing received |
| Forward restored, Sync now | sent, day downloaded | 1 row, Pending, hash matches |
| After the fix: no-connection result, then Home key and back (the layout rebuilds the page) | the sentence is still shown | — |

Found during the check and fixed: returning from the sign-in browser rebuilt the Sync page and lost its sentence, Sign in and the
sign-in's answer (see `SyncNotices`). Also learned: signing in needs the API as well as the identity host (the app asks
`staff/session`); with the API cut, the token exchange succeeds but the app answers "Sign-in needs a connection" and keeps nothing.
A poor connection can't be timed by hand on the device; F1 and F2 cover it. The sign-in answer after a rebuild is covered by the
screen test, not repeated on the device.

**Automated**

On the final code (31 October 2026, Docker running):

- `dotnet build FieldSales.slnx -c Release -m:1 --no-incremental`: 0 warnings, 0 errors.
- `dotnet test FieldSales.slnx -c Release --no-build -m:1 --settings sqlserver.runsettings`: **2,849 passed, 0 failed,
  0 skipped** across 12 projects (baseline 2,809, so 40 new): Tablet Core integration 200, Tablet Components 59, Tablet Core unit 12,
  API integration 174, API unit 185, Web integration 873, Web unit 219, Identity Admin integration 804 and unit 256, DemoData 4,
  SnapshotSpike 22, TabletSpike 41.
- `scripts/tablet-app.ps1 -Action Build -Configuration Release -DevTrust`: the signed Android APK builds and trusts the dev
  certificate for localhost only.
- `node --test plan_docs/field-sales-delivery/tests/status-store.test.js`: 11 passed. The only console change is `WI-040` → `done`.

## Left for later items

- WI-041: resume the same Sync automatically after sign-in, I-03's "After sign-in, this Sync resumes automatically.", and what
  Sign in says without signal (Sign-in US-004 S2–S4). The resume point is D8.
- WI-042: Needs Attention for items refused as technical faults, their sentences (T1.2), and "Everything has been sent."
- Sync reminders (Area 1 US-002); MI-62 (a disabled account with unsent work) stays undecided.
- From the WI-039 device check: Home shop rows aren't tappable, and New Order's footer sits under the Samsung taskbar.
