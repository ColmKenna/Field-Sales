# WI-032 increment 1A — synthetic snapshot generator

20 October 2026. The standalone generator exports all agreed capacity profiles, with a complete
JSON/gzip/manifest set and an independent owner-bound Unsent fixture. This is **desktop fixture
generation evidence**. Android/iPad download, database import, storage, search and reopen
measurements remain increment 1B; MI-02 is not closed and WI-032 remains active.

## Run the generator

From the repository root:

```powershell
dotnet run --project tools/FieldSales.SnapshotSpike -c Release -- --profile target --as-of 2026-10-20 --seed 42 --output .artifacts/wi032/my-target-run
```

Choose a new output directory for each export. `--help` lists all arguments. `--profile` accepts
`baseline`, `target` or `stress`; override `--products`, `--locations`, `--history-orders`
(per Location) and `--history-lines` (per historical order) to try another scale. The explicit
as-of date and seed make fixtures reproducible on the same toolchain. Canonical fixture data
does not contain wall-clock measurements or output-directory paths.

Each published output directory contains:

- `snapshot.json`: streamed synthetic data with schema label `spike-format-v1`.
- `snapshot.json.gz`: the same JSON compressed with gzip.
- `manifest.json`: scale, counts, owner, seed/as-of, file lengths and SHA-256 fingerprints.

The default separate Unsent file is `unsent-42.json` beside that output directory. `--unsent`
can specify another separate file. The manifest records its owner/format/length/fingerprint;
the CLI report gives its actual path. Existing owner-matched Unsent fixtures are read and
retained byte for byte, including edits. Different owners/formats are rejected. Exporting a
snapshot never clears, replaces or regenerates an existing Unsent fixture.

Outputs are staged in a new sibling directory and published by directory move only after
all files are complete. Existing output directories/files are rejected. Cancellation/write
failure removes the tool's known temporary files; an abrupt process kill can leave a hidden
staging directory, but cannot make it a published export. This fixture publication mechanism
does not establish the production tablet's power-loss or database-activation guarantees.

CLI exit codes are `0` success/help, `2` invalid options, `1` export failure, and `130`
cancellation. No production model references, server/database calls, identity registrations
or mobile workloads are required to run this tool.

## Recorded exports

One release run per final profile on Windows `10.0.26300`, x64, 32 logical processors, SDK
`10.0.401`; seed `42`, as-of `2026-10-20`. These single runs were on the development machine
under normal workload, not a controlled benchmark. Generation includes JSON writing, gzip,
fingerprinting and publication; it excludes any tablet transfer/import/query work.

| Profile | Products | Locations | Historical orders | Historical lines | JSON bytes | Gzip bytes | Generation seconds |
|---|---:|---:|---:|---:|---:|---:|---:|
| Baseline | 5,000 | 500 | 3,000 | 60,000 | 16,441,340 | 1,570,674 | 1.311 |
| Evaluation target | 25,000 | 500 | 6,000 | 120,000 | 64,337,468 | 6,294,888 | 4.579 |
| Stress | 100,000 | 2,000 | 48,000 | 1,440,000 | 342,711,599 | 33,229,222 | 23.125 |

Each profile also contains four-level breadcrumbs, eight attributes per product with varied
long text, three base prices, three tiers with three quantity breaks each, promotions on 15%
of products, replacements on 5%, thresholds, leads and master/branch links. These fields are
future-capacity stand-ins, not decisions about the production contract. Names contain
apostrophes and accents; generated values vary deterministically instead of using repeated
long dummy text. The generator retains only a record/order at a time during JSON export.

Final JSON fingerprints:

| Profile | SHA-256 |
|---|---|
| Baseline | `1411568b7938f006322ee34f0b6fcad252977dd3a5b1620c013608631642c273` |
| Evaluation target | `39fab31a592fca9b55315e65ae144a5e073de2dafb2bde2e6a0e875f16559e97` |
| Stress | `e3de723e76e1c23cb9c3dcb90259555d44abd1f70fbd6e5ed4e5eb6c67d0c57b` |

The shared Unsent fixture contains two Calls, one Order and one schedule change. It remains
983 bytes across all three exports, SHA-256
`aca81e6cd733dfb7b5151607efccf441e92074cff6245ce9614f3f0552aacbf3`.
Separate full-size verification checks actual lengths, JSON/gzip SHA-256, streaming gzip
round-trip to the JSON hash and the unchanged Unsent fingerprint for every profile.

Raw payloads/reports are ignored under `.artifacts/wi032/final-profiles/`; earlier preliminary
profiles under `.artifacts/wi032/profiles/` precede the varied-long-text refinement and are not
the final evidence. Do not use the export sizes alone to accept the proposed 60-second Sync
budget: device import/indexing/activation and the actual network have not been measured.

## Verification

The seven approved scenario names are in the [approved plan](tablet-offline-spike-plan.md).
Their implementation has 22 executable cases:

| Agreed scenario | Evidence |
|---|---|
| Reproducible fixture bytes | JSON, gzip, manifest and Unsent files match with fixed inputs, including a different current culture; changing the seed changes the snapshot. |
| Requested counts | All three profile defaults are fixed to the agreed values; small CLI overrides verify actual entity/nested counts without a product-by-Location cross product. |
| Valid future-payload references | Breadcrumb chains, product/Location/price/quantity relationships, promotions, replacements, thresholds, leads, branches and Unsent references are checked against the exported data. |
| Verifiable payloads | Manifest lengths/hashes match files and gzip decompresses to the exact JSON. |
| Unsent untouched | Edited existing work is byte-identical after a new snapshot; another owner is rejected. |
| Invalid input rejected | Thirteen argument cases plus date/path/collision checks publish nothing and preserve existing data. |
| Failed/cancelled export stays complete | Interrupt after a real partial write; previous files/Unsent stay intact, no incomplete export is published, and retry succeeds. |

Complete repository verification passed: **2,317 .NET tests across eight assemblies**, no skips
or failures, plus **34 JavaScript tests** (18 admin, 5 product-form, 11 console). This feature
branch is based on main `7f75ca9`; WI-031's additional tests remain on its separate feature
branch and are not included in this main-based count.

The full non-incremental build passed with 79 existing NU1902/NU1903 warnings and no errors;
the final source build passed with 45 warnings of those same codes and no errors. The release
generator build itself has zero warnings/errors. Focused generator tests pass all 22 cases.
The admin JavaScript suite ran from a scratch copy with all 15 source/test/package files
byte-identical to the repository, avoiding vendored-file changes.

Raw evidence is under `.artifacts/wi032/`: `full-build.log`, `final-build.log`, `release-build.log`,
`full-tests.log`, eight `full-verification/*.trx` files, `verification-summary.json`,
`generator-tests.log`, `final-generator-tests/`, `admin-tests.log`, `product-form-tests.log`,
`console-tests.log` and `profile-integrity.log`. All full-size final payload/hash/round-trip
checks passed. The console diff adds only WI-032 active to main's status map; the staged diff
has no whitespace errors. Production identity/API characterization changes belong to later increments.

## Next review

Review the profile realism and fixture payload sizes before authorizing 1B. Confirm representative
Android/iPad models/OS and Mac/Xcode/signing/build access, then agree the device-runner scenarios.
The next increment evaluates SQLite on those tablets, including interrupted download, force-close,
activation, disk/memory use and input-to-visible-result time. Production runtime/storage adoption,
contract fields/versions, address source, I-03 layout and MI-62 remain the later decision checkpoint.
