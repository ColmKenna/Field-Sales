# WI-032 tablet options and first spike plan

Increment 1A approved on 20 October 2026 by the developer's **Approved** response. The approval
accepts the evaluation direction, synthetic scales and seven generator scenarios below. Later
increments require their own review. This document records no device measurements and does
not establish a production snapshot contract. See [1A results](tablet-offline-spike-results.md).

## Selection and confirmed inputs

WI-032, **Sign in on the tablet and download the day's data offline**, is the first incomplete
item after WI-001–031. There are no active items. Its dependencies WI-002, WI-021 and WI-005
are done in the current console. Iteration 1 MVP; Human Tight-Loop implementation; Human-Led
tests; Complex; High risk from authentication/shared rules and snapshot failure modes;
Low confidence because the runtime and storage spike are new.

The developer confirmed **mainly Android and iPad**, requested a technology comparison, and
asked for proposed test scales. Exact device models, OS versions and production volumes are
unknown. No additional platform is included in the first spike (proposed scope).

## Options and recommendation

These judgments concern this repository and its requirement to retain Unsent work offline.
They are not measured performance rankings.

| Option | Fit for Field Sales | Cost or constraint |
|---|---|---|
| **.NET MAUI + Blazor Hybrid + SQLite** | Recommended candidate. C# application/storage code, local Razor components and HTML/CSS screens in an installed Android/iPad app. SQLite is accessed through native .NET services. | Introduces MAUI/Blazor and mobile packaging. WebView rendering and memory need device checks. Existing Razor Pages are not reusable Blazor components. |
| .NET MAUI + native XAML + SQLite | Same C# and native-storage advantages, with native controls. | A reasonable alternative if native controls are preferred; introduces XAML screen development and less HTML/CSS reuse. |
| Flutter + SQLite | Viable installed app with documented local SQL and offline patterns. | Adds Dart and a separate client toolchain/model layer to this C# repository. |
| React Native + Expo SQLite | Viable installed app with local SQL that persists across restarts. | Adds a TypeScript/React mobile stack and separate client models/tooling. |
| PWA + IndexedDB | Web deployment and a Home Screen entry; offline operation is possible. | WebKit storage has quota/eviction policies and persistent-storage requests use heuristics. The Unsent-work retention requirement needs explicit validation of installation, persistence and storage-pressure behavior. |

**Propose .NET 10 MAUI Blazor Hybrid and SQLite for evaluation.** C# and HTML/CSS fit the existing
codebase, and a local database fits the offline workload. Final adoption depends on device
results. The website remains Razor Pages/BFF; the tablet is a separate installed client.
SQLite lives in app data through .NET, with Unsent work in a separate owner-bound store;
it is not browser localStorage. Packaging does not by itself prove crash safety, encryption,
or survival of uninstall/user-cleared app data.

Microsoft documents [Android/iOS support and the Mac requirement for iOS builds](https://learn.microsoft.com/en-us/dotnet/maui/supported-platforms?view=net-maui-10.0),
[local native execution of Blazor Hybrid components](https://learn.microsoft.com/en-us/aspnet/core/blazor/hybrid/?view=aspnetcore-10.0),
and [SQLite access from MAUI](https://learn.microsoft.com/en-us/dotnet/maui/data-cloud/database-sqlite?view=net-maui-10.0).
Flutter documents [SQLite persistence](https://docs.flutter.dev/cookbook/persistence/sqlite)
and [offline architecture](https://docs.flutter.dev/app-architecture/design-patterns/offline-first).
Expo documents [SQLite on Android/iOS and persistence across app restarts](https://docs.expo.dev/versions/latest/sdk/sqlite/).
WebKit explains [quota, eviction and requests for persistent storage](https://webkit.org/blog/14403/updates-to-storage-policy/).
Sources checked on 20 October 2026. The recommendation is engineering judgment based on these
capabilities and the repository; no comparative benchmark has been run.

## Proposed capacity profiles

These are synthetic evaluation sizes, not claims about the live catalogue or supported limits.
Keep counts and generation seed configurable. History below means completed historical orders
used for suggested-list sizing, separate from an Unsent fixture.

| Profile | Products | Assigned Locations | Historical orders per Location | Lines per historical order | Total historical lines |
|---|---:|---:|---:|---:|---:|
| Baseline | 5,000 | 500 | 6 | 20 | 60,000 |
| Evaluation target | 25,000 | 500 | 12 | 20 | 120,000 |
| Stress | 100,000 | 2,000 | 24 | 30 | 1,440,000 |

Each profile includes the full projected payload family from MI-02, even where production
features arrive in later work items. Proposed reproducible fixture distributions:

- Products: identity/code/name, four-level category breadcrumbs, eight attributes, one current
  and two future base-price entries, unit/quantity-step rules, three price tiers with three
  quantity breaks each. Values vary deterministically, including longer text, apostrophes and
  accents; repeated dummy text would give misleading compression results.
- Promotions on 15% of products and replacement links for 5%, rounded down with valid targets.
- Locations: customer/location identity and names, town/address fixture text, main-contact
  details and one threshold record each; historical orders/lines at the table's cardinalities.
- One lead per ten Locations and one master with five branch links per twenty-five Locations,
  rounded down. Relations point to generated records and have no self/dangling references.
- A separate owner-bound Unsent fixture containing Calls, Orders and a schedule change. Its
  fingerprint lets later device tests detect any accidental alteration during refresh.

The synthetic fixture's future fields are capacity stand-ins. Their shape does not settle later
business contracts. Text/address fixtures must not silently become new production address data.
The current Location model contains Town and Eircode, without street-address fields; that source
gap requires a decision before production contract approval.

Proposed device-review targets:

- **Location results in under one second for 500 assigned Locations**, as required by T-4.2.1.
  Measure input-to-visible-results as well as query time in the device runner.
- Evaluation-target download, validation, local indexing and activation within **60 seconds**
  on a controlled **10 Mbit/s connection with 100 ms round-trip latency** (proposed target).
  Report transfer/import/activation separately. Also measure a 2 Mbit/s degraded connection;
  treat it as diagnostic and preserve the previous snapshot on interruption.
- Cold offline reopen within **3 seconds** (proposed target). Record memory and steady/peak disk
  use, including old snapshot, staged snapshot and transfer file, before setting release budgets.
- Baseline and evaluation target must meet agreed targets on both representative devices;
  stress identifies the failure boundary and must fail safely if capacity is exhausted.

Use release builds, at least one representative Android tablet and one iPad, five measured
sync/reopen repetitions and a fixed set of at least twenty search terms. Report every run and
the median/worst result; identify cold/warm cache conditions. A desktop/emulator run is useful
diagnostics but cannot close MI-02. No final minimum OS/model or product-volume limit is assumed.

## Human-driven increments

Approval of this draft authorizes **increment 1A only**, accepting its test scenarios and the
proposed evaluation direction/scales. Later increments retain separate review stops. WI-032
becomes active when 1A starts and stays active until all of its acceptance criteria are delivered.

1. **1A — Synthetic data generator and reproducible fixtures.** Agent writes a standalone .NET 10
   tool under `tools/FieldSales.SnapshotSpike/` (`Program.cs`, `SpikeOptions.cs`, `SpikeProfiles.cs`,
   `SyntheticSnapshot.cs`, `SnapshotGenerator.cs`, `SnapshotWriter.cs`, project file), and file/CLI
   integration tests under `tests/Integration/FieldSales.SnapshotSpike/`. Add both projects to
   `FieldSales.slnx`; use existing central test packages. Generate streamed JSON and gzip payloads,
   a manifest with schema label `spike-format-v1`, seed/as-of/scale/counts/lengths/SHA-256, and the
   separate Unsent fixture. Operational timing is separate from deterministic fixture bytes.
   Outputs go under ignored `.artifacts/wi032/`; documentation goes in this file and the item
   notes. No production model/project references, API/database access, native app shell or
   identity registration. Run all three scales locally, recording generation sizes/times as
   desktop data-generation evidence. Human reviews coverage, sizes and profile realism. **STOP.**
2. **1B — Device storage runner.** After continuation and provision of device/build access, agent
   adds an isolated `tools/FieldSales.TabletSpike/` MAUI Hybrid runner plus portable storage-test
   helpers and tests; keep its mobile build in a separate solution from the server build. Evaluate
   the proposed SQLite storage using generated payloads, staging/activation and independent
   Unsent fixtures. The human chooses/provides actual devices and runs/observes the interruption,
   force-close, offline reopen and timing cases; agent supplies commands, collects exported
   metrics and analyses them. Use a synthetic-only transfer harness, separate from production
   API/identity. No personal or live business data. **STOP after results are recorded.**
   **20 October steering:** Samsung/Android first; the developer has Mac access and will check
   the older iPad mini at the end. Concrete checkpoints/scenarios are in
   [the Android-first 1B plan](tablet-offline-device-spike-plan.md), approved for 1B-A.
3. **1C — Decide runtime/storage and production design.** Human accepts or changes the candidate,
   capacity budgets, supported device/OS floor, full-download affordability and deployment path.
   Resolve MI-62, I-03 layout, address source, contract ownership and compatibility. Agent records
   the decisions and prepares exact production fields/scenarios. **STOP for production-plan approval.**
4. **2 — Connected sign-in and local session.** Proposed production paths
   `src/FieldSales.Tablet/`, a portable `src/FieldSales.Tablet.Core/`, the identity host's actual
   client registration/seeding/configuration, and tablet/identity tests. Public OIDC client with
   code + PKCE through the system browser, no embedded client secret; native secure token storage.
   Connected sign-in and the 24-hour local access timestamp remain distinct from bearer expiry.
   No offline reopen refreshes the timestamp. Review I-03 on both devices. **STOP.**
5. **3 — Versioned per-rep download.** Proposed `src/FieldSales.Sync.Contracts/`,
   `src/FieldSales.Api/Sync/`, API registration/policy and API integration tests. Initial payload
   is assigned Locations/main contact, unrestricted catalogue/breadcrumbs, current/future base
   prices and units/steps. Reuse `CoverageOwnershipReader`/`EffectiveOwnerResolver`, derive the
   rep from the authenticated subject and enforce current Field Salesperson access server-side.
   Propose an on-demand full snapshot initially; confirm affordability/consistency/version rules
   at 1C, including coherent reads across DirectoryDb and CatalogueDb. **STOP.**
6. **4 — Durable staged replacement, Unsent store and minimal Home/Sync.** Tablet core/store,
   tablet I-03/T-01/T-02 components and integration/device tests. Validate complete payload and
   version/owner, activate atomically, preserve the previous snapshot on every failed stage,
   retain Unsent work separately across expiry and account switching. First sign-in leads to
   manual Sync; show last synced only after activation, and the specified no-connection copy.
   The storage/session seam supports later real capture flows; do not fabricate a production
   order/call screen to satisfy this slice. **STOP for two-device review.**
7. **5 — Final acceptance.** Full suites, both-device evidence, a criterion-by-criterion report,
   handoff, console done and commit/push. No PR, merge, rebase or branch deletion without the
   applicable user authorization.

The exact production paths/steps in increments 2–4 remain a roadmap until 1C approval; they
must not be implemented using this approval. At every buildable increment, stop green,
committed and pushed on the card's feature branch, with the required `feat(wi-032)` / Refs body.

## Human-Led scenarios for 1A approval

These scenarios are the acceptance gate for the proposed first implementation increment.
Tests exercise generated files/CLI output with small fixtures; the three full-scale runs produce
capacity evidence, avoiding repeated million-record fixture creation in the normal test suite.

| Name | Intent |
|---|---|
| Should_ReproduceFixtureBytes_When_SeedAsOfAndScaleAreTheSame | Fixed inputs produce identical canonical JSON/gzip/manifest content; telemetry is separate. |
| Should_GenerateTheRequestedCounts_When_UsingAProfileOrOverrides | Product, Location and per-Location historical counts match the profile/overrides without a product-by-Location cross product. |
| Should_KeepAllReferencesValid_When_GeneratingFuturePayloadFamilies | Breadcrumbs, prices, tiers/breaks, promotions, replacements, historical lines, leads and branches resolve to generated records. |
| Should_ReportVerifiablePayloads_When_ExportingCompressedAndPlainData | Gzip round-trips to JSON; recorded counts, lengths and hashes match the actual exported files. |
| Should_LeaveUnsentFixtureUntouched_When_GeneratingAnotherSnapshot | A second snapshot export cannot replace or mutate the independent Unsent fixture. |
| Should_RejectInvalidInputWithoutPublishingOutput_When_ScaleIsInvalid | Invalid/negative/overflowing counts and unsafe output combinations produce a useful error/exit code before publishing a fixture. |
| Should_KeepPublishedFixturesComplete_When_ExportFailsOrIsCancelled | Interrupted writes never present partial output as a complete manifest/payload set. |

The generator is new behavior. Before later changes to identity/API, run existing characterization
tests for web client seeding, staff policies, ownership and restricted visibility unchanged.
The 1B/production scenario matrix must be reviewed before its tests are authored; source anchors
include interruption preserving the old snapshot/Unsent, offline use at two hours, the exact
24-hour expiry boundary, owner isolation, token expiry independent of local access, manual-only
Sync, future price/quantity fidelity, version rejection and last-synced changing only on activation.
MI-62's scenario has no chosen expected outcome yet.

Verification at implementation stops includes the agreed tests plus the existing .NET solution
suite using `sqlserver.runsettings` and existing JavaScript suites. Use the standing build audit
exceptions only for existing NU1902/NU1903 warnings; do not weaken tests. Planning documentation
itself requires no application test run.

## Out of scope

- WI-033 product attributes in the production snapshot; future attributes exist only in spike data.
- WI-034 production Location search and WI-035 Location screen; the spike measures a search seam.
- WI-036 inactive main-contact presentation and WI-037 deliberate tablet sign-out flow.
- WI-038 standalone order capture; other domain capture screens belong to their own cards.
- WI-039 exactly-once upload and upload-first orchestration; WI-040 detailed Sync failure states;
  WI-041 expired-Sync continuation; WI-042 full Unsent/Needs Attention UI.
- WI-043 permission-aware restricted products in production. The initial contract excludes all
  restricted products; the spike's future fields grant no real business access.
- Deltas, background Sync, speculative changes to staff web UI and reimplementation of identity admin.

## Missing information and risks

There is **no unresolved input needed to build 1A as proposed** once approved. The following
items are explicit stop conditions before their dependent increments, not assumptions silently
introduced during implementation:

- **MI-01:** runtime is a recommendation awaiting approval for evaluation; production adoption
  follows 1C. Device models/minimum OS are unknown. Current host is Windows with Android, iOS,
  Mac Catalyst and MAUI Windows workloads listed; no Mac connection/signing/device access verified.
  Mac/Xcode, signing and representative devices must be arranged before 1B iPad measurements.
- **MI-02:** proposed synthetic scales and time targets need human acceptance; real product/history
  volumes remain unknown. Actual Android/iPad measurements are required before closing the spike.
  Memory/disk, encryption/backup/device-management policy and safe free-space headroom need review.
- **MI-08:** I-03 is a layout proposal. Approve the tablet entry design before production UI;
  I-04 belongs to WI-037. No screen design is approved through this synthetic generator increment.
- **MI-62:** disabled-account treatment of earlier work requires a product decision before that
  path/production acceptance; do not invent upload, recovery or restoration behavior.
- Production address source is missing beyond Town/Eircode. Initial contract field list, schema
  ownership, version compatibility, cross-database consistency and clock-rollback handling need
  explicit resolution at 1C. Suggested defaults are proposals, not established decisions.
- The console uses `workItemStatus`, per standing decision, instead of the skill's `status-json`.
  Only that map is changed at implementation start/completion. No unparseable status/card data.
- All performance targets beyond the source's 500-Location/under-one-second requirement are
  **(inferred/proposed)**. The recommendation's advantage is code/tooling fit; its performance
  and crash durability remain unproven until the spike.

Approval boundary follows the invoked
[console-delivery-next-item skill](C:/Users/colmk/.codex/skills/console-delivery-next-item/SKILL.md):
“Execute only your sub-steps, one increment at a time, stopping after each.”
Increment 1A is authorized. Approval of later increments remains separate.
