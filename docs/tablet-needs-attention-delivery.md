# WI-042 — Unsent work and Needs Attention

T-4.3.1 / REP-US-017. Feature branch: `feature/wi-042-show-unsent-items-needs-attention`.

The existing tablet Sync screen now shows Needs Attention first, followed by Ready to Send and
In Progress. Each technical refusal has a sentence, Open for review and a confirmed Delete action.
Orders can be reviewed and marked ready again without changing their captured body or identity.
Saved Calls count as Ready to Send until confirmed or refused. Needs Attention stays in the Unsent
total and is excluded from subsequent uploads until the rep reviews and re-marks an Order.

## Scope and approved amendments

The developer accepted the plan/scenarios/conflict resolutions with **Apply**, reviewed the mapping
at commit `6f17039`, then authorized **Finish all of WI-042**. No further checkpoint remains.

- WI-039's protocol wins over the source's duplicate-rejection example: a repeat is successfully
  confirmed as `AlreadyReceived`. Review/re-mark is demonstrated using a technical refusal instead.
- An additive **local** SQLite migration is authorized despite the source's schema-change exclusion.
  There is no server migration, endpoint change, upload body change or snapshot version change.
- Ordinary order editing and the edited-rejected-order edge remain WI-046; Call correction remains
  WI-055. Re-mark here deliberately does not edit the captured work or regenerate its id.
- Reminder scheduling remains E13. This item supplies the shared singular/plural wording and Unsent
  count used by Home and Sync, for that later reminder integration.

## Rejection mapping

| Server fault | Sentence |
|---|---|
| `incomplete` | Couldn't be sent - the upload was incomplete |
| `corrupt` | This couldn't be sent - contact the office |
| `unsupported` | This couldn't be sent - contact the office |
| `not-yours` | This couldn't be sent - contact the office |
| `id-in-use` | This couldn't be sent - contact the office |
| `too-large` | This couldn't be sent - contact the office |
| Unknown/missing code or unreadable reason | This couldn't be sent - contact the office |

`id-in-use` means another rep or item kind already owns the id. It is never treated as an ordinary
repeat. `Received` and `AlreadyReceived` both mark Sent. All six fault codes are technical;
there is no business-rule refusal. Reassignment/archive/catalogue changes after capture remain accepted.
Connection failures, server errors and expired sign-ins retain the WI-040/041 recovery behaviour.

## Storage and interaction

- Unsent format 3 adds `work_faults`: item id (foreign key to `work`), fault code, raw server response,
  HTTP status and refusal timestamp. No captured JSON is rewritten. Formats 1 and 2 upgrade in order;
  existing Unsent/Sent rows survive, and a newer unsupported format still fails closed.
- Support responses are bounded to 16 KiB plus a truncation marker, with a timeout for streamed
  response bodies. Raw details remain in the local store and never appear in the default screen.
- A refusal is attached only to the same id/body that was uploaded. Other items still upload, and
  the day still downloads. Mixed outcomes name only retryable items as going at the next Sync.
- Re-mark checks that the current item is a rejected, readable Ready to Send Order. Deletion checks
  confirmation and a current rejection. Removing work also removes its fault through the foreign key.
- The app rechecks the current rep and local access window before mutation. A confirmation is tied
  to the session in which its list was loaded; old confirmations cannot act after another sign-in.
- Review displays captured order lines and prices, or the saved Call's channel and time. An unreadable
  Order offers office help and can still be deleted from Needs Attention. No Call editing is introduced.
- Ready/In Progress rows open review through links; only Needs Attention rows have per-item buttons.
  Legacy work with an unrecognised state remains visible as Other saved work and in the overall total.
- Home includes Needs Attention in its Unsent link. Sync does not navigate away while items need attention.
  Removing the last item says **No unsent items**, without implying a deleted item was sent.

## Acceptance verification

| Quoted source criterion | Result and evidence |
|---|---|
| 1. "Counts by state: In Progress, Ready to Send, Needs Attention." | PASS: UnsentSummary separates these states; saved Calls are Ready to Send. Core and rendered component scenarios check mixed counts. |
| 2. "Needs Attention is listed first; each item shows its reason as a sentence." | PASS: section order is Needs Attention / Ready to Send / In Progress. Component tests and responsive screenshots verify several refused items and the plain sentences. |
| 3. "An Order rejected for 'Duplicate submission' can be opened, reviewed and marked ready again; it uploads at next Sync." | PASS under the approved amendment: a technically refused Order is reviewed, re-marked unchanged, and Sent on the next manual Sync. Ordinary repeats remain AlreadyReceived, covered by the existing receiver and lost-confirmation tests. |
| 4. "A Call in Needs Attention can be deleted after confirming; Calls cannot be deleted anywhere else." | PASS: both confirmation/cancellation are exercised through real component events; storage refuses deletion without a current refusal. Session/rep isolation is also tested. |
| 5. "The Unsent count includes Needs Attention items; reminder text reads '1 needs attention'." | PASS for the approved slice: shared singular/plural wording and total are shown on Home and Sync. Scheduling/trigger integration belongs to E13. |
| 6. "An incomplete upload shows 'Couldn't be sent - the upload was incomplete' with Open and Delete." | PASS: actual HTTP technical refusal is persisted; component tests assert the exact sentence and actions. |
| 7. "A reason the rep can't act on shows 'This couldn't be sent - contact the office'; the raw reason is kept for support." | PASS: six-code/fallback scenarios retain the raw bounded reason; rendered screens assert internal detail is absent. |
| 8. "An Order captured before its Location was reassigned uploads and is accepted; nothing appears in Needs Attention." | PASS: unchanged receiver has no business-data validation; existing API reassignment/archive scenario and tablet successful-confirmation scenarios remain green. |

## Scenario accounting

The agreed names/intents are recorded in `plan_docs/.agent-notes/WI-042.md`.

- Core `NeedsAttentionTests`: restart/rep isolation, no automatic retry, reviewed Order resend with
  unchanged bytes, Call/Order deletion, cancellation, refused deletion outside Needs Attention,
  each fault and fallback with raw support data, migration from v1/v2, bounded response data and
  mixed refusal/connection-failure wording.
- Components `NeedsAttentionScreenTests` (partial `TabletSessionScreenTests`): ordered mixed sections,
  exact incomplete/fallback sentences and actions, review before re-mark, confirmed/cancelled Call/Order
  deletion, singular Home count, staying on Sync and rejecting a stale confirmation.
- `TabletAppSessionScenarios`: real local-access guard refuses deletion after expiry, sign-out or a
  different rep signing in. Receiver reassignment and duplicate criteria reuse existing nontrivial tests.
- The edited-rejected-Order case is explicitly deferred to WI-046; no agreed scenario is silently dropped.

## Verification

- Release solution build, `--no-restore --no-incremental -m:1 -warnaserror` with the repository's
  existing NU1902/NU1903 exception: **0 warnings, 0 errors**. The first sandboxed attempt could not
  resolve the pinned Aspire SDK; the retry with package/cache access succeeded.
  Final incremental build of the completed source also passed with zero warnings/errors.
- Final component build/tests with warnings treated as errors: **79 passed**, 0 failed/skipped.
- `scripts/tablet-app.ps1 -Action Build -Configuration Release -DevTrust`: final signed APK built,
  no warning/error diagnostics; SHA-256 `764BF047099432853BD8B40AA578A4B6E91E5D4ECEC28690F3F3907F2EDF0B92`.
- Console status-store tests: **11 passed**; tablet sign-out JavaScript tests: **4 passed**;
  identity admin UI JavaScript tests: **18 passed**.
- Full application suite, `dotnet test FieldSales.slnx -c Release --no-build --no-restore -m:1
  --settings sqlserver.runsettings`: **2,900 passed, 0 failed, 0 skipped across all 12 projects**.
  This is 32 more than WI-041's 2,868: Tablet Core integration 227, Components 79, API integration 174,
  Web integration 873, Identity Admin integration 804, DemoData 4, SnapshotSpike 22, TabletSpike 41,
  API unit 185, Web unit 219, Identity Admin unit 256 and Tablet Core unit 16.
  Detailed TRX results are under `.artifacts/wi042/test-results`; full log `.artifacts/wi042/full-suite.log`.

Responsive preview: production Sync markup rendered with HtmlRenderer, then inspected in Chromium
at 390, 768 and 1280 px. Correct section order; no horizontal overflow; Open/Delete targets 56 px;
raw support reason absent. The only browser console error was the scratch preview's missing favicon.
Artifacts: `.artifacts/wi042/preview/output/screen-{390,768,1280}.png`.
Component event tests exercise review and confirmation; the browser preview is static markup.
No physical-tablet installation/sign-in was performed for this item.

## Delivery checks

- PASS: criteria 1–8 demonstrated within the explicitly approved amendments, with evidence above.
- PASS: provisional commits reconciled to actual changes; scoped feature commits use WI-042/T-4.3.1/REP-US-017 trailers.
- No merge, rebase, fast-forward, branch deletion or force-push. Unrelated existing review files/README edits are excluded.
