# WI-032 — tablet runtime and storage decision (1C)

## Scope amendment — 23 October 2026

The developer chose a **scope change to decide 1C sooner**: decide the tablet runtime and storage
on the physical Samsung evidence, before the iPad mini check. The
[device plan](tablet-offline-device-spike-plan.md) allowed this ("only after the agreed iPad result
or an explicit scope amendment"). Unless the developer says otherwise **(inferred)**, the iPad
mini/Mac check is deferred, not dropped. It becomes a confirmation before any iPad use, and an
iPad failure would reopen 1C for iOS only.

1C closes the spike's two open questions on the WI-032 card:

- **MI-01 / `{{TABLET_CLIENT_STACK}}`:** the production tablet runtime.
- **MI-02:** the offline snapshot size and storage choice, its targets and its capacity policy.

## Evidence

All on the Samsung Tab S6 Lite SM-P610 (Android 13, Arm64, WebView 155) with the same Release
schema-v4 APK, synthetic fixtures and USB transfer with a 10 Mbit/s application simulation:
[pilot](tablet-offline-samsung-baseline-pilot-results.md),
[repeated matrix](tablet-offline-samsung-repeat-results.md) and
[recovery](tablet-offline-samsung-recovery-results.md). 46 original reports in total, all
independently verified.

| Profile (products / Locations / history lines) | Refresh median / worst | Offline open median / worst | Searches under 1 s | Peak sampled storage |
|---|---:|---:|---:|---:|
| Baseline (5,000 / 500 / 60,000) | 8.8 / 10.6 s | 498 / 751 ms | 100/100 | 99 MB |
| Target (25,000 / 500 / 120,000) | 56.6 / 63.6 s | 508 / 823 ms | 100/100 | 413 MB |
| Stress (100,000 / 2,000 / 1,440,000) | 227.3 / 228.8 s | 527 / 1,083 ms | 100/100 | 2.1 GB |

- **Correctness:** every refresh imported all payload families with verified hashes and counts.
  Edited Unsent work survived every refresh, reopen, cancel and force-close.
- **Recovery:** cancel during download or import kept the previous whole snapshot. A force-close
  before the switch reopened the old snapshot, and after it the new one. A second owner stayed
  fully isolated. Offline cold starts made no network requests.
- **Responsiveness:** the developer reported "Smooth" at target and stress scale. The emulator's
  multi-second startup delay did not occur on the device.
- **Where refresh time goes:** import (insert and search projection) dominates, at 30–43 s of
  each target refresh. Deleting the oldest retained copy took 0.8–8.1 s at target, 15–28 s at
  stress, and 69.7 s once when a stress copy was deleted inside a baseline refresh.
- **Gaps:** cleanup interrupted by a force-close is not resumed when the app reopens, so old copies
  stay until the next refresh. Real network conditions, real production volumes and the iPad are
  untested. Force-close tests do not certify power loss.

## Decisions for the developer

### D1 — Tablet runtime (MI-01)

| Option | Notes |
|---|---|
| **A. Adopt .NET MAUI Blazor Hybrid (net10.0) — recommended** | Release build runs and recovers correctly on the Samsung. It shares C#/Razor skills and code with the website and API. iOS stays subject to the deferred iPad check. |
| B. Evaluate another runtime | Not measured; it would restart the spike. |

### D2 — Storage design (MI-02)

Common to every option: SQLite through Microsoft.Data.Sqlite with the pinned native 3.53.3 and a
preflight that refuses another native build; WAL with synchronous FULL; one rep-bound snapshot per
signed-in identity; staged import verified before a single-transaction switch; previous snapshot
kept until the new one is active; Unsent work in a separate database that refresh never rewrites;
storage work off the UI thread.

| Option | Notes |
|---|---|
| A. Adopt the spike exactly | Old-copy deletion stays inside the refresh, and interrupted cleanup waits for the next refresh. |
| **B. Adopt it, with cleanup moved off the refresh and resumed at open — recommended** | Refresh ends at the switch; old copies are deleted afterwards in bounded batches, and any leftover cleanup runs on the next open. Smallest change that addresses both observed issues. Proven by automated tests in the production slice. |
| C. Adopt it, with one SQLite file per snapshot copy | Retiring a copy becomes a file delete and leftovers are reclaimed by deleting inactive files. Larger change and unmeasured; worth it if real volumes approach stress scale. |

The spike stored every payload family as per-entity JSON plus search projections to measure full
size. The production layout of those families follows snapshot contract v1, which lists only what
the first tablet slice needs; other families are added with their own work items.

### D3 — Targets

The source requires **500-Location search under one second**, and it was met everywhere. Proposed
working budgets for the production slice, on SM-P610-class hardware at target scale:

| Option | Notes |
|---|---|
| **A. Offline open under 3 s and refresh under 90 s — recommended** | Met with margin (worst 1.1 s and 63.6 s). Option D2-B would remove the deletion time from refresh. |
| B. Offline open under 3 s and refresh under 60 s | One of five target refreshes missed it; this needs import optimization before acceptance. |
| C. No refresh budget for the MVP | Measurements only. |

Target scale is the planning assumption until real product and history volumes are known (MI-02
keeps that question open for every snapshot change).

### D4 — iPad

| Option | Notes |
|---|---|
| **A. Defer the iPad mini/Mac check to before any iPad use — recommended** | 1C stands for Android. An iPad failure reopens 1C for iOS only. |
| B. Android only for the MVP | Removes the iPad check from WI-032 scope. |

### Proposed defaults (accept or override)

- **Capacity:** before each refresh, require free space of at least twice the current snapshot
  database size plus 512 MiB. Otherwise refuse with a message and keep the current data.
- **Data protection:** exclude the snapshot and Unsent databases from Android cloud/device backup
  and rely on Android's file-based encryption at rest for the MVP. No database-level encryption
  until a security review asks for it.
- **Contract details** (field list, schema ownership, version compatibility, cross-database
  consistency, clock rollback): settle in the WI-032 production plan for snapshot contract v1 and
  the server snapshot builder, not here.

## After the decision

The chosen options become a standing decision for WI-032 and the 17 items it blocks. The console
placeholder `{{TABLET_CLIENT_STACK}}` resolves to the chosen runtime. WI-032 then moves from the
spike to its production slice: the tablet app shell with staff sign-in, snapshot contract v1, the
server builder, Sync with staged swap, the separate Unsent store and the 24-hour local session.
That work needs its own plan and Human-Led scenario approval. The spike tool stays isolated, and no
production code changes until that plan is approved.

## Decision — 23 October 2026 (developer)

- **D1 runtime: A.** The production tablet app uses **.NET MAUI Blazor Hybrid on .NET 10**.
  `{{TABLET_CLIENT_STACK}}` resolves to this. iOS remains subject to the iPad check (D4).
- **D2 storage: B.** Adopt the spike's storage model, as listed under D2, with **old-copy cleanup
  moved off the refresh**: the refresh ends at the verified switch, old copies are deleted
  afterwards in bounded batches, and unfinished cleanup resumes when the app next opens.
- **D3 targets: A.** On SM-P610-class hardware at target scale (25,000 products / 500 Locations /
  120,000 history lines): **offline open under 3 s, refresh under 90 s**, and the source's
  **500-Location search under 1 s**. Target scale remains the planning assumption until real volumes
  are known.
- **D4 iPad: A.** The iPad mini/Mac check is deferred until **before any iPad use**. 1C stands for
  Android; an iPad failure reopens 1C for iOS only.

The proposed defaults (capacity check, data protection and where the contract details are settled)
were presented and not overridden. They remain **proposals** to confirm in the WI-032
production-slice plan, not settled decisions.
