# WI-037 — Tablet sign-out and retained Unsent work

T-4.7.1 / IAM-US-005, from `main` at `e383667` on
`feature/wi-037-warn-before-tablet-sign-out`.

## Approved scope and checkpoints

The developer approved the plan and full scenario matrix with **Approved Apply**
on 29 October 2026. They explicitly authorized session implementation under
checkpoint review, extending the source package's named agent slivers.
Human Tight-Loop remains in force: stop after every increment.

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
   Stop for review.
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
