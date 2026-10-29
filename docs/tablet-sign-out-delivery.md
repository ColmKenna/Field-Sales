# WI-037 — Tablet sign-out and retained Unsent work

T-4.7.1 / IAM-US-005, from `main` at `e383667` on
`feature/wi-037-warn-before-tablet-sign-out`.

## Approved scope and checkpoints

The developer approved the plan and full scenario matrix with **Approved Apply**
on 29 October 2026. They explicitly authorized session implementation under
checkpoint review, extending the source package's named agent slivers.
The initial approval retained a stop after every increment. After reviewing
increment 2, the developer explicitly instructed: "continue, keep continueing
unless you need extra information from me". That authorizes completing the
remaining agreed increments without further approval stops. No product values
remain unresolved for this slice.

The approved session contract ends tablet access, clears local session
credentials, preserves each rep's existing private workspace, and prevents stale
asynchronous operations from reopening the ended session. Automatic logout uses
the existing 24-hour expiry and clock-rollback rules, enforced while open and on
return. There is no new idle timeout or server-wide logout. The predecessor's
system-browser sign-in already uses `prompt=login`.

1. Characterize saved work across reopening and existing session boundaries.
   Stop for review before changing behavior.
2. Add I-04's warning only when Unsent work exists, with Stay signed in,
   Sign out, and Open Sync & unsent. Stop for review.
3. Implement the approved session boundary and asynchronous-operation guards.
   Continue under the developer's later instruction above.
4. Verify restoration and isolation, run the full suite and Android build, and
   finish on a committed, pushed feature branch. Do not integrate it.

MI-62 disabled-account policy, lost/reassigned tablet administration, uploading
work, and the later Unsent screen remain outside this slice.

## Agreed scenario matrix

| Scenario | Intent |
| --- | --- |
| Saved work survives restart | A saved Call and Order retain IDs, timestamps and complete payloads after reopening. |
| Manual sign-out warns with item types | Two Calls and one Ready to Send Order produce the I-04 count and types. |
| Stay signed in preserves access | Close the warning and return to the previous screen without changing session or work. |
| Open Sync & unsent preserves access | Close the warning and open `/sync` without automatically starting Sync. |
| Open Sync & unsent works offline | Open `/sync` and explain that Sync needs a connection. |
| Zero Unsent items bypasses warning | Sign out directly when the current rep has no Unsent work. |
| Warning counts only the current rep's work | Other reps' retained items neither affect the count nor appear. |
| Warning supports accessible interaction | Focus enters the dialog, actions have names and large tap targets, and staying restores focus. |
| Confirmed sign-out ends local access | Clear local session credentials and show signed-out entry, including offline. |
| Manual sign-out preserves exact saved states | Retain both Calls and the Ready to Send Order without deletion or marking Sent. |
| Same rep restores work after manual sign-out | After restart and connected sign-in, restore original items and states without recapture. |
| Another rep cannot access retained work | A different identity cannot see or count the original rep's work, which its owner can later recover. |
| Access remains valid before expiry | Returning within the existing 24-hour window permits offline access. |
| Foreground expiry ends access | At 24 hours, end access without a decision dialog and preserve work. |
| Expiry while away ends access on return | Returning after expiry denies access; the owner's next sign-in restores the work. |
| Clock rollback fails closed | A clock earlier than connected sign-in ends access while preserving work. |
| Refused token refresh preserves valid offline access | Refresh refusal does not shorten the approved local access window. |
| Cached navigation cannot bypass logout | A pending entry handoff or direct workspace route cannot reopen the session. |
| Pending operations cannot undo logout | Late refresh, reads or Sync completion cannot restore credentials, private UI or navigation. |

## Increment 1 — Characterization

`TabletSignOutCharacterizationTests` reopens production `TabletDevice`,
`SnapshotStore` and `UnsentStore` instances over the same SQLite files. The
existing test adapter represents persistent platform secure storage. These are
desktop integration tests, not a claim of Android process/device execution.

Four cases lock in exact record equality for two Calls and a Ready to Send
Order: reopening one tick before expiry; denial at exact expiry; denial after
clock rollback; and rep switching, including identical item IDs in separate
workspaces. The expiry/rollback cases also check restoration after connected
sign-in. Payload state remains opaque fixture JSON; no new capture schema is
introduced.

The existing session scenarios additionally cover refused refresh preserving
offline access, refreshed tokens not extending the local window, and role
restrictions. No production logout, session, storage or UI behavior changes in
this increment. Later matrix rows remain pending later increments.

The delivery console's canonical `workItemStatus` entry is `active`; all other
console data remains unchanged.

Verification at the increment 1 checkpoint:

- New characterization cases: **4 passed**, no failures or skips.
- Complete Tablet Core integration suite, including those cases: **107 passed**,
  no failures or skips. TRX files: `.artifacts/wi037/increment-1-tests/`.
- Console status-store tests: **11 passed**, no failures or skips.
- Parsed comparison against the branch base confirms only the WI-037 status
  changed inside the console. `git diff --check` passed.
- The full solution suite and Android build remain scheduled for increment 4.

Stopped for the required increment 1 review before implementing the warning.

## Increment 2 — Warning component

The developer approved proceeding with increment 2. `SignOutControl.razor`
implements I-04's warning with the count and item types, Stay signed in,
Sign out, and Open Sync & unsent. It requests a fresh list of kinds from its
caller each time. No saved payloads are passed into the dialog. With no work it
emits the sign-out request directly. Confirming closes the warning before
emitting that request; opening Sync closes it and navigates to `/sync` without
calling Sync. Stay and Escape return focus to the initiating button.

The native modal dialog has an accessible name/description, keyboard focus
wrapping, a backdrop and touch targets of at least 48px. CSS permits the main
actions to stack at narrow widths. The JavaScript is loaded by the tablet's
existing WebView entry page.

Session and storage behavior remain unchanged. The control is ready for the
real session handler: app-bar wiring in `MainLayout.razor` and enforcement of
the current-rep/session boundary will be connected together in increment 3.
There is no visible incomplete sign-out action in the production layout.

Verification at the increment 2 checkpoint:

- Complete component integration suite: **22 passed**, including **11 new**
  warning cases; no failures or skips. The tests dispatch actual Razor events
  and cover the count/types, all actions, zero items, singular wording,
  recounting, separate SQLite workspaces, read failure/retry, and disposal
  during a pending count.
- Complete Tablet Core integration suite: **107 passed**, no failures or skips.
  TRX files: `.artifacts/wi037/increment-2-tests/`.
- **18 browser assertions passed** against the actual warning component,
  production JavaScript/CSS and existing Sync component in an isolated desktop
  host. They cover 800x1280, 1280x800 and 360x640 layouts and touch targets;
  Tab/Shift+Tab containment; Escape/Stay focus restoration; confirmation and
  zero-work callbacks; and offline Sync navigation without starting a transfer.
  The host supplies test counts/session data; this does not verify native
  logout or real credentials. No browser console errors remained.
- The browser check exposed a native-dialog Tab transition to the page body.
  Explicit focus wrapping fixed it; the complete browser check then passed.
- JavaScript syntax and `git diff --check` passed. Full solution verification
  and the Android build remain scheduled for increment 4.

Browser check code/results and portrait/landscape screenshots are under
`.artifacts/wi037/increment-2-*`. This was desktop browser execution, not an
Android device test. Stopped for increment 2 review before session changes;
the console remains `active`.

## Increment 3 — Local access and session boundary

The app bar now exposes the completed warning to signed-in reps. The layout
checks entry, hides private content while checking, and removes both the content
and dialog when the session changes. Protected routes cannot open signed-out
workspaces. Home, Sync, entry and offline catalogue guards ignore stale results;
disposed Sync screens cancel their work.

`TabletDevice` serializes credential changes through the secure store's shared
session boundary. Manual logout, 24-hour expiry and clock rollback invalidate
the session version and remove the current-rep pointer, sign-in timestamp and
tokens. The only additional persisted marker is an identity-free boolean that
the tablet has previously been signed in, so offline entry can accurately say
to reconnect rather than claiming the tablet has never downloaded a day.
Rep workspaces, snapshots, retained Location details and Unsent databases are
not deleted or reassigned. A credential-removal failure is reported and leaves
access blocked in the current process; it is not reported as successful logout.

Refresh requests do not hold the session gate across network I/O. They accept
responses only for their original live session version. A late success cannot
restore ended credentials, and a late refusal cannot delete a newer session's
tokens. Sign-in completion and warning confirmation likewise carry the observed
version, so an old operation cannot replace or end a newer session.

`TabletApp` invalidates handoffs, cancels pending session work and serializes
Syncs across cancellation. A handoff also rechecks the local window before use.
The foreground monitor checks rollback at most one second apart and schedules
the expiry check at the remaining window boundary. It pauses on the Android
window's Stopped event and checks again on resume. No new idle or network policy
has been introduced; refused refresh still permits locally valid offline work.

Verification before the final suite:

- Tablet Core: **124 integration cases passed**, including 13 new session
  cases and four tests linking the production tablet facade. Native platform
  services alone are adapted for desktop execution.
- Layout/Sync component cases verify hidden private content, stale entry
  results, the version passed to app-bar sign-out, ignored late Sync results,
  and cancellation when the screen is disposed.
- A worker-read case verifies that a session change alone prevents old shop
  details from being published, without relying on a new navigation.
- Handoff unit tests now include explicit invalidation.
- Release solution build: **zero warnings and zero errors**.

## Increment 4 — Acceptance verification

The production layout, Home, Sync, entry, warning and tablet facade were run
in a desktop interactive host against real local SQLite data. Test-only tools
provide connected sign-in results and an adjustable clock; this does not claim
an Android system-browser sign-in or physical-device run. All **15 journey
assertions passed**, including the following source criteria:

> With 3 Unsent items, deliberate Sign out warns with the count and offers Stay signed in or Sign out (US-005 S1).

The app-bar warning names exactly two Calls and one Order. Staying restores
focus without leaving Home; Open Sync closes the warning and opens the existing
Sync screen. The increment 2 browser checks separately verify offline wording,
keyboard containment, Escape and touch targets.

> Continuing Sign out ends access but keeps those items for the same rep's next sign-in on the tablet (S2).

The production browser journey confirms that manual sign-out hides the shop,
removes the action and blocks direct Home navigation after a reload. Aoife sees
no Colm shop or Unsent work, and zero work bypasses the warning. Signing Colm
back in restores Home and all three items. Integration tests compare every
record's ID, kind, saved timestamp and raw JSON, not just the count.

> Automatic logout keeps the same items and states for the rep's next sign-in (S3).

The foreground monitor locks the production UI after clock advancement without
a decision dialog. Colm's next sign-in restores the three-item warning. Timer
and return-path integration cases cover exact expiry, rollback and stopped/
resumed monitoring; record equality verifies unchanged saved states.

Final browser check code/results and portrait/landscape screenshots are under
`.artifacts/wi037/final-*`. The developer's unrelated review files remain untouched.

## Final verification

The complete Release solution suite passed on its first final run: **2,703
passed, zero failures, zero skips**, across all 12 test projects.

| Project | Passed |
| --- | ---: |
| API integration | 143 |
| Demo data integration | 4 |
| Identity/admin integration | 804 |
| Snapshot spike integration | 22 |
| Tablet components integration | 28 |
| Tablet Core integration | 124 |
| Tablet spike integration | 41 |
| Web integration | 873 |
| API unit | 182 |
| Identity/admin unit | 256 |
| Tablet Core unit | 7 |
| Web unit | 219 |

Commands and artifacts:

- `dotnet build FieldSales.slnx -c Release -m:1`: zero warnings/errors.
- `dotnet test FieldSales.slnx -c Release --no-build -m:1 --settings sqlserver.runsettings --blame-hang-timeout 2m --logger trx --results-directory .artifacts/wi037/full-suite-results`:
  all projects passed. Log: `.artifacts/wi037/full-suite.log`; parsed counters:
  `.artifacts/wi037/full-suite-summary.json`.
- All admin/Web/tablet JavaScript and console status tests: **38 passed**,
  zero failures/skips. Log: `.artifacts/wi037/javascript-tests.log`.
- Demo script tests: **5 passed** in the cached offline Python container, using
  LF-normalized temporary copies and mocked Docker operations.
- Browser checks: **18 warning/accessibility/layout assertions** plus **15
  production journey assertions**, all passed. Final browser console contained
  no warnings or errors.
- `scripts/tablet-app.ps1 -Action Build -Configuration Release`: signed Android
  publish succeeded without warnings/errors. Both existing Android runtime
  targets were compiled. Log: `.artifacts/wi037/android-build.log`.
- The APK's `tablet-sign-out.js`, `app.css` and `index.html` match the current
  source bytes. Artifact: `.artifacts/wi037/android/com.fieldsales.tablet-Signed.apk`.
  SHA-256: `43df24133978d555e10a4cfef1e199e5e30f3858bd14b60cf2b220927b9d67ab`.
- `git diff --check` passed. Parsed comparison against the integration base
  confirms that only WI-037's status changed inside the delivery console.

Final review also made queued dialog calls tolerate a removed/null element
after automatic logout. Four JavaScript regression tests cover this race,
open/close focus, Escape and keyboard wrapping, and run in the existing CI fast
checks job. Startup/handoff comments were clarified to describe the required
session revalidation; no additional C# behavior changed after the full suite.

No physical tablet installation or execution was performed. Native packaging,
desktop production-component execution and the agreed integration scenarios
provide the verification recorded here.

Missing information: none for this item. Repository, native source paths and
`main` integration base are resolved from the approved predecessor. MI-62 and
device administration remain explicitly outside the slice. WI-037 is `done`;
the next item is informational only and has not been started.
