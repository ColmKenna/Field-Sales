# WI-032 — physical Samsung validation, first checkpoint

## Selection and scope

Resume **WI-032 — Sign in on the tablet and download the day's data offline**, T-4.1.1,
REP-US-001 / IAM-US-003. Iteration 1 / phase-1; Human Tight-Loop implementation, Human-Led
tests; Complex, High risk, Low confidence. It is the sole active item in
`plan_docs/field-sales-delivery/plan-data.js`; dependencies WI-002, WI-021 and WI-005 are done.
No item is skipped and no console edit is required. The previously agreed format adaptation
uses the data file's authoritative `workItemStatus` in place of embedded HTML `status-json`.

After pushed wakeup-analysis checkpoint `4a7d18f`, the developer selected **Physical Samsung
validation** and **USB data cable and USB debugging**. This is preparation of checkpoint
**1B-B1: a physical baseline pilot**, within the [original Samsung measurement plan](tablet-offline-device-spike-plan.md).
Approval authorizes only this pilot. The five-repeat baseline/target/stress matrix, recovery
cases and iPad check retain their later review stops; this pilot does not complete 1B-B or MI-02.

The [existing traces](tablet-offline-startup-handoff-trace-results.md) link two emulator delays
to main waiting behind a 2.75–2.88-second WebView draw. Why that draw is slow is unresolved.
The physical pilot establishes that the tool, reporting and offline behavior work on the
actual Samsung before a longer measurement matrix. A baseline pilot cannot settle whether
the stress-scale delay is specific to the emulator.

## Read-only planning evidence — 22 October 2026

The first ADB inventory was empty. On the developer's **search again**, USB detection found
one authorized device, serial **R52T30AZZXZ**. Reads on that explicit serial report:

| Property | Observed value |
|---|---|
| Device | Samsung Tab S6 Lite, SM-P610; manufacturer `samsung` |
| Android | 13, API 33; `ro.kernel.qemu=0` |
| ABIs | `arm64-v8a,armeabi-v7a,armeabi` |
| Active WebView | `com.google.android.webview` 155.0.8059.43, installed/enabled |
| Spike package | Not installed |

Evidence is `.artifacts/wi032/samsung-planning-preflight.json`. No install, launch, host,
route, network/wake change or measurement occurred during planning. USB identity is the one
the developer connected for this task; do not discover or select a wireless alternative.

Use the unchanged, non-debuggable, profileable **Release schema-v4 APK** from source `4998512`:
`.artifacts/wi032/startup-handoff-traces/handoff-release.apk`, **45,026,457 bytes**, SHA-256
`8b51486e7130e41e7ba9809e3b2f5b898a2b3d95c4e29b69616fa4d794964ec5`.
Its hash was rechecked during planning. The project includes ARM64; prior signature, native
SQLite 3.53.3 and alignment evidence remains in the tracing results. Installation/runtime
compatibility is still an execution outcome, not established by ABI and OS labels alone.

Reuse `.artifacts/wi032/final-profiles/`, seed 42/as-of 2026-10-20. Baseline is **5,000 products,
500 Locations, 60,000 historical lines**, 16,441,340 JSON bytes / 1,570,674 gzip bytes.
The manifest's hashes/counts and separate `unsent-42.json` remain the oracle. No regeneration.
The existing fixture-host DLL/runtime files are present under
`tools/FieldSales.TabletSpike.Host/bin/Debug/net10.0/`; archive their hashes/configuration
before use, without rebuilding. Host Debug configuration is recorded separately from the
tablet's Release configuration.

## One increment after approval

1. **Readiness and identity.** Human keeps this Samsung connected, unlocked and USB-debugging
   authorized, and observes the pilot. Agent rechecks the same serial/model/physical identity,
   active WebView, free storage, memory/battery/thermal point observations where accessible,
   initial network/wake settings and existing reverse routes. Hash APK, fixtures and host
   artifacts. If the serial is absent/unauthorized or identity differs, stop before any install.
   No shared ADB restart, alternate device, OS/WebView upgrade, reset or data clear.
2. **Install this spike only.** Verify package ID/signature and supported ABI; install the exact
   APK as `com.fieldsales.tabletspike`, then verify the installed APK hash. If the package has
   appeared since planning, record it and preserve its data; a signature mismatch stops the
   pilot rather than prompting an uninstall. Resolve this package's launcher and record the
   first planned launch as setup, not an extra warm-up or a measured offline case. Human can
   bring the spike forward initially; subsequent inputs require it to remain foreground.
3. **USB fixture service.** Start the existing host on `127.0.0.1:7206`, serving only the
   allowlisted synthetic exports. Record the owned process identity and log. Use a hidden
   background process, no firewall/LAN exposure. If port 7206 or that device's reverse route
   is already occupied, stop without replacing it. Add only this serial's `tcp:7206` reverse.
   Select server `http://127.0.0.1:7206/`, owner `spike-rep-42`, baseline profile, transport
   **USB adb reverse**, **Simulate 10 Mbit/s + initial 100 ms**, pause **None**.
   This is an application simulation over USB, not a measured physical network target.
4. **Baseline setup download.** Run Download & apply once, preserving its original attempt
   even on failure. Record whether it was actually fresh or replacement; never clear data
   to force freshness. Verify all payload counts/hashes, owner, SQLite 3.53.3/WAL/FULL and
   original work identity. Run the twenty fixed terms once, independently check counts, and
   export the original setup report before editing the note.
5. **Edited-work replacement.** Set the synthetic Order note to **Samsung saved note WI-032**;
   capture the displayed note and new logical work fingerprint. SaveNote does not rewrite the
   prior report's work fields, so use that UI checkpoint as the edited-work expectation.
   Run one baseline replacement, twenty terms once, and export that original report. Its
   before/after fingerprints must equal the edited-work expectation. Keep two Calls, Order
   and schedule-change IDs/payload identity intact. Human observes touch/search responsiveness.
6. **One original offline process-cold open.** Remove only the route created here, enable
   airplane mode and disable Wi-Fi temporarily; confirm no active default network. USB ADB
   stays connected. Force-stop only the spike package, launch once with `am start -W`, and
   check the retained baseline generation, counts and edited work. Run the twenty terms once
   and export this original offline report. No server request or new download is allowed.
   OS caches are uncontrolled. No Perfetto capture, logical-payload scan or extra launch.
7. **Verify, restore and record.** Preserve unique run IDs, original JSON/UI/launch records,
   settings, host logs and independent results under a new ignored
   `.artifacts/wi032/samsung-baseline-pilot/`. Export means retrieve app-specific JSON; if the
   Android chooser opens, use Back after verifying it, never choose a recipient. Restore the
   actual initial network/wake state; remove only the created reverse route; stop only the
   host/wake helpers created here after PID/start-time verification. Keep app/data on the
   Samsung and leave devices, other apps and shared services running. Record installed hash.
   Add results/index/notes, commit and push the same feature branch, then **STOP for review**.

Expected pilot: **one setup download, one edited-work replacement, one offline cold open;
three original reports and sixty searches**. These are individual observations, not the
five-repeat matrix. No attempts are silently replaced. A correctness failure stops before
the next operation, preserving the failed/interrupted report and current generation/work.

Use existing observation limits: 900 seconds per download/replacement, 45 seconds for the
first observed offline-ready bound, 90 seconds per twenty-term batch and 600 seconds for
export retrieval. Cutoffs are controller bounds, not native clocks. If one expires, preserve
the original pending/failure state; do not start a new attempt or force-close an in-flight
refresh as an implicit retry. Read-only recovery/export and scoped cleanup remain allowed.

## Human-Led acceptance scenarios

These adapt the already agreed storage scenarios to this bounded physical pilot. They must
be accepted before installation or measurement; no new test source is proposed.

| Scenario | Required evidence |
|---|---|
| Should_UseTheSelectedSamsung_When_StartingThePilot | Same explicit serial/SM-P610/physical identity, authorized USB, installed hash matches; Android/ABI/WebView/configuration recorded. No other device operation. |
| Should_ImportTheBaseline_When_TheFixtureIsComplete | Setup and replacement reports match owner, full manifest/counts/hashes, SQLite 3.53.3/WAL/FULL; original Unsent identity verifies. A partial run cannot claim success. |
| Should_KeepEditedWork_When_RefreshingAndReopeningOffline | UI checkpoint records edited note/fingerprint; replacement and offline reports retain that fingerprint and item identities. Offline generation equals the completed replacement generation. |
| Should_ReturnExpectedLocations_When_SearchingOnSamsung | All three twenty-term batches have exactly the fixed terms/order and independent expected counts, including case/accents/apostrophes and no-match. Record query and observed-visible clocks separately. |
| Should_ReopenWithoutAConnection_When_TheSpikeProcessIsCold | Route absent, airplane/Wi-Fi/default-network observations, one package cold launch, successful original local-open report and no fixture request; no warm-up/replacement. |
| Should_ReportActualClocksAndFailures_When_Exporting | Unique original reports, honest physical/USB/simulation conditions, separate refresh/local-read/activity/observed-ready clocks; unavailable metrics remain unavailable. Every failure/cutoff stays in results. |
| Should_RestoreOwnedResources_When_ThePilotEnds | Actual initial settings restored, own route/service/helpers ended, installed hash/work retained, no unrelated cleanup. |

The 500-Location observed-visible target is **under 1 second**. Candidate review proxies
remain 60-second refresh and 3-second local open. Report each pass/miss, including slow
successful observations; these proxies are not full cold-launch/first-paint targets.
The offline worker/body/continuation clocks can be discussed alongside emulator evidence,
but different scale/device/ABI/WebView/graphics conditions prevent a controlled improvement
or stress root-cause claim. Human touch observations supplement the existing search clock.

## Files, validation and subsequent stops

This planning change touches this file and `plan_docs/.agent-notes/WI-032.md`. Execution may
add `docs/tablet-offline-samsung-baseline-pilot-results.md`, an index entry to
`docs/tablet-offline-device-spike-results.md`, a checkpoint here and updated notes; controllers
and raw evidence remain ignored. No app, test, package, console or identity/API source change.

Existing source verification remains **2,358 .NET / 34 JS passes at `4998512`**. This plan
reuses unchanged artifacts; no build or suite repeat is needed. Verify hashes/source identity
and run the agreed native scenarios. A needed source/build/runtime change requires a focused
amendment and its appropriate suite; do not repair the pilot silently.

After this pilot's review, propose **1B-B2**, the existing five-refresh/five-offline-open matrix
per baseline/target/stress, with original attempts and search expectations. Recovery/cancel/
owner-isolation cases from the parent plan remain necessary later physical work. The iPad
mini/Mac check stays at the end. MI-02 and adoption 1C remain open; WI-032 stays active.

Out of scope: further emulator analysis or operation, Perfetto/new managed-stack captures,
startup/repaint/SQLite optimization, device upgrade/rooting/cache clearing, physical link
shaping, full matrix or crash/cancel cases in this first pilot, iPad setup, 1C adoption,
production sign-in/24-hour sessions/Sync, MI-62, WI-033/034 implementation, PR/merge/main.

## Missing information and assumptions

No unresolved design question is needed to approve this pilot. The connected device's serial,
model/OS/ABI/WebView are now known. Continued USB access, unlocked screen and human touch
observation are execution prerequisites, not facts inferred from the one planning inventory.
Runtime compatibility, free capacity and actual timings remain outcomes. The pilot's small
physical baseline is proposed as the next tight-loop increment, not substituted for the full
physical acceptance matrix. No new budget, runtime/storage adoption or production commitment.

Primary guidance checked 22 October 2026: Microsoft's [MAUI supported platforms](https://learn.microsoft.com/en-us/dotnet/maui/supported-platforms?view=net-maui-10.0)
requires API 24+ for Blazor Hybrid and an updated platform WebView. Android's
[ADB guidance](https://developer.android.com/tools/adb) covers authorized USB access, explicit
serial selection and package installation. The existing project declares Android API 24.

The [console-delivery-next-item skill](C:/Users/colmk/.codex/skills/console-delivery-next-item/SKILL.md)
requires Human-Led scenarios to be agreed **before implementation begins**, and tight-loop
execution **one increment at a time, stopping after each**. This concrete pilot and seven
scenario intents were approved by the developer's **Approved** after pushed plan `a817897`.
That approval authorizes only checkpoint 1B-B1, followed by its review stop.
