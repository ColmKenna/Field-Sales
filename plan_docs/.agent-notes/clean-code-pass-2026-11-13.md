# Clean code pass, 2026-11-13

Not a delivery-console work item. A language-level refactoring pass run with the `csharp.clean.code`
skill, on branch `refactor/clean-code-pass` (from `main` at `3182380`). No behaviour was meant to change.

## Current state

All three approved batches are committed on the branch. Nothing is pushed and no PR is open.

| Commit | What |
|---|---|
| `000f026` | Private fields lose the `_` prefix in Identity, Admin.Services, Api and Web (the `.editorconfig` rule). Fields that clash with a parameter or local are written `this.name`. |
| `246314b` | Classes sealed across 98 files in Identity and Admin.Services. |
| `18f3f8a` | `Lock` for the lock-only fields in `EntryHandOff` and `PriceDayWatch`; comments on four silent parse-fallback catches. |
| `be949e0` | 39 characterization tests for `DirectoryApiClient` (`tests/Unit/FieldSales.Web/DirectoryApiClientTests.cs`). |
| `80a1c31` | `DirectoryApiClient` and `CoverageApiClient`: reply validation moved out of `SendAsync`. |
| `24fbcee` | `ClientCreateService`: create split into `ReadDraft`, `ValidateScopes`, `NewClient`; clone copy moved to `CopyOf`. |
| `a90944d` | `ClientDetailsService`: authentication and token-settings updates split into validate, propose, apply. |
| `0785276` | `SnapshotStore.Apply` split into import, verify, activate. |

Verified with `./scripts/test.ps1 -Tier WorkItem` after every commit (2,342 tests at the end, all passing),
and once at the end with `-Tier SqlServer` (1,036 tests, all passing).

## Deliberately left alone

- **EF-mapped entity fields** keep the underscore: `Product`, `Contact`, `Customer`, `Location`, `PriceTier`.
  Their model configuration names the fields as strings (`HasField("_locations")`), so a rename breaks the model.
- **Test projects** still use `_camelCase` fields (129 files). Out of scope for this pass.
- **`FieldSales.Tablet` and `tools/FieldSales.TabletSpike`** (MAUI) were not sealed or changed: they were not built here.
  `TabletApp.workGate` could become a `Lock` once someone can build the app.
- **`ReferenceListStore<T>` and `DirectoryTypeListStore<T>`** are base classes and stay unsealed.
- **`SnapshotStore.Insert`** is still about 125 lines. It is one flat `switch` over the entity families that reads top to bottom;
  splitting it into eleven methods would not make it easier to follow.

## Flagged, not fixed (need a decision)

1. `InstallationSecureStore.Remove` blocks on an async task (`.GetAwaiter().GetResult()`). A fix changes `ISecureStore`.
2. Nine synchronous `Gate.Wait()` calls in the tablet stores; `SnapshotStore` awaits the same gate asynchronously elsewhere.
3. Fire-and-forget tasks with unobserved exceptions: `TabletApp.cs` (cleanup after sync, `ResumeCleanup`),
   `TabletSessionMonitor` timer callback, `MauiProgram` network probe.
4. `TabletSessionMonitor` locks on its timer instead of a dedicated lock object.
5. FluentAssertions 8.10.0 is referenced by both `Identity.Admin` test projects and used by no test.
   Version 8 needs a paid licence for commercial use; removing the reference removes the exposure.
6. Methods with 9 or 10 parameters: `IdentityResourceEditorService.CreateCoreAsync`, `SeedData.SeedAsync`,
   `AssignmentHistory.Capture`, `AssignmentHistoryWriter.Append`.
7. Four classes with seven constructor dependencies: `StaffCookieEvents`, `TabletSnapshotBuilder`,
   `TerritoryAssignmentStore`, `AssignmentTransferStore`.
8. After the field rename, some primary-constructor classes hold `private readonly T name = name;`. It compiles and
   behaves as before (the field hides the parameter); the fields could be dropped in favour of the parameter.

## Next step

Developer review of the branch, then push and PR if wanted.

## Progress log

- 2026-11-13: Phase 1 report approved ("Proceed"), all three batches done, WorkItem and SqlServer tiers green.
