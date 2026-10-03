# WI-021 — Increment 4 sequencing amendment

The developer's **Continue** approves the published read checkpoint and
authorizes increment 4. Preparing its history integration exposed one dependency
scheduled too late in the original plan.

## Confirmed dependency

The approved history policy requires immutable display-label snapshots for the
actor and previous/new reps. `TerritoryAssignment` and `RepReportingLine` store
opaque identity subjects only. The existing identity `/staff/current-roles`
endpoint returns the caller's subject and roles; it supplies neither labels nor
other reps. Identity already stores `ApplicationUser.FullName` and `UserName`.

The original plan introduces the trusted read-only staff directory in increment
5, after increment 4 must write these snapshots on assigned Location creation
and Town changes. Implementing history first would leave its name source absent.

## Proposed targeted amendment

Move only the trusted read-only staff directory contract, identity endpoint and
API lookup client/registration from increment 5 into increment 4. Use the existing
identity accounts and bearer/current-role boundary to obtain subject, display
label, current business roles and account availability. Fail closed on an
unavailable/invalid lookup when a history entry requires identities. Capture the
returned labels at write time; history reads never replace stored labels with
current account names. Credentials and account/role administration stay in their
existing services.

Then implement the already-approved increment 4 work: additive Assignment History
schema and SQL append-only guards; owner-diff writer/query/formatter; atomic
initial inherited history for first/additional Locations and geography-change
history for Town edits; one entry per changed owner and none for source-only or
unchanged-owner changes. Preserve position/contact/version behavior. Keep identity
network reads outside the DirectoryDb write transaction, and use the existing
serializable transaction/retry pattern for ownership diffs and persistence.

Increment 5 retains assignment add/remove handlers, current rep eligibility/team
authorization, business reporting-line management/provider, the Head Office
reporting page/BFF client, geography usage protection and final acceptance checks.
It reuses the lookup introduced in increment 4. The review stops remain intact.

## Files and verification

Bring forward `FieldSales.StaffAccess/StaffDirectoryContract.cs`, the protected
lookup in `FieldSales.Identity/src/FieldSales.Identity/Program.cs`, API client/
registration and focused tests in the existing Identity Admin/API test projects.
Add the planned Coverage history model/configuration, DirectoryDb migration,
history contracts/query/formatter and CustomerStore integration with API wiring.
Extend SQL fixtures only as necessary to isolate immutable history between cases.

Use the already-approved Human-Led scenario families: immutable readable history
after label changes/restart; first/additional Location creation; changed Town;
no entry for unchanged owners/source-only changes; atomic rollback/stale writes;
and fail-closed identity data. Characterise existing Customer/coordinate/contact/
geography behavior before modifying their paths. Run the focused checks, clean
solution build, full .NET regression and existing JavaScript suites before the
history checkpoint is committed and pushed.

## Approval boundary

No increment 4 production or test code has been changed. WI-021 remains active
and the console is unchanged. Reply **Approved** to accept this sequencing
amendment and continue increment 4 only.

The `console-delivery-next-item` skill requires: “If implementation reveals the
plan was wrong in a material way, stop, explain the divergence, and propose a
plan amendment. Wait for approval before continuing.” The missing label source
is the reason for this pause; the accepted business policy is unchanged.
