# iPad development implementation results

Branch: `feature/ipad-support`. Recorded 2 November 2026. Physical Debug sign-in and initial sync
passed; the remaining device checklist and deferred D4/Release validation are still open.

## Implemented

- macOS-only iOS target, iPadOS 16.4 minimum, iPad orientations, sign-in URL scheme, privacy manifest
  and simulator-only Keychain entitlement.
- Xcode 27 scene manifest/delegate and MAUI Controls/WebView 10.0.110, required for launch and scene
  authentication forwarding. Existing cross-platform Window access checks remain in use.
- iOS Application Support storage, backup exclusion, inherited file protection, native free-space
  measurement and Debug SQLite/readback diagnostics. Installation-bound Keychain session reset
  preserves saved work and unrelated entries.
- Development-only LAN identity/API endpoints, consistent authentication authorities and secret-backed
  HTTPS certificate configuration. macOS helper and [setup/run instructions](tablet-ipad-development.md).

## Environment and checks

| Check | Result |
|---|---|
| Mac toolchain | Apple Silicon, .NET SDK 10.0.101, iOS workload 27.0.10722, Xcode 27.0 |
| Paired physical device | iPad mini, 5th generation; iPadOS 26.7.1; Developer Mode enabled; locked during initial inspection |
| Fast test tier | **1,146 passed**, none failed/skipped; includes 5 installation-state cases and 11 LAN-profile cases |
| AppHost build | Passed, zero warnings/errors |
| Simulator Debug build | Passed with MAUI 10.0.110 and the Xcode 27 scene configuration |
| Physical `ios-arm64` Debug build | Passed with the user's Personal Team development certificate/profile, zero warnings/errors; installed on the iPad |
| Android Debug build | Blocked: no Android SDK installed on this Mac (`XA5300`) |
| LAN resource model | Manifest assertions passed for HTTPS target ports 7201/7204, consistent identity authority and secret certificate password reference |
| Mac certificate trust | `security verify-cert` verified the LAN hostname and trusted chain successfully |
| Helper checks | Shell syntax, missing-device/invalid-host rejection, mocked simulator/device build arguments, Debug-only selection, TLS URLs and signing/path arguments with spaces passed |
| Simulator runtime | Passed: native startup, SQLite 3.53.3/WAL/FULL and Unsent readback, backup exclusion, Keychain initialization and rendered sign-in screen |
| Physical sign-in | Passed after enabling full root-certificate trust; native token exchange and API session both returned 200 |
| Physical initial sync | Passed: snapshot returned 200, sync `Completed` in 813 ms, with 2 locations and 3 products |
| Physical offline cold launch | User confirmed locations and products remain usable after disabling Wi-Fi, force-closing the app and reopening it |
| Physical offline order persistence/upload | User confirmed the saved order remained visible after reopening and left Unsent after reconnecting/Sync; a read-only backend query verified the received order and ledger body |

The initial simulator attempts exposed a stale/unsigned native code-signature failure and the missing
Xcode 27 scene lifecycle. Simulator builds now use local ad-hoc signing (`CodesignKey=-`), without an
Apple development identity or profile, and the project includes the scene configuration.

The final simulator smoke used the iPad mini (A17 Pro), iOS 27.0. Debug startup logged
`sqlite=3.53.3 journal=wal synchronous=2 backup-excluded=True`. File protection is not reported by the
simulator and is checked strictly only on physical hardware. A screenshot of the rendered sign-in
screen is saved locally at `.artifacts/ipad/simulator-smoke.png`. The app was then stopped to release
resources. These results establish initial simulator usability, not browser sign-in or sync success.

## Machine setup and remaining validation

`mkcert` is installed. The development CA is trusted in this Mac's login keychain and was added to
the selected simulator. Ignored certificate files are under `.artifacts/ipad/certificates/`; only the
public `rootCA.crt` should be transferred to the physical iPad. AppHost certificate settings and missing
development credentials were saved to user secrets without changing existing configured values.

Initial runtime checks were constrained by low Mac disk space (roughly 300 MiB at the lowest check)
and an unresponsive Docker Desktop. Only generated task build outputs were removed to reclaim space.
Later checks showed several GiB free. Docker recovered after a force-stop/start, but subsequently lost
SQL connectivity again; identity discovery returned HTTP 500 with a SQL pre-login connection failure.
The diagnostic readers and failed normal development stack were stopped before recovering Docker.

The user signed into Xcode with their own Apple ID and chose their Personal Team. An Apple Development
certificate was created and verified as a valid signing identity. A temporary native project under
the ignored `.artifacts/ipad/provisioning/` directory generated the app/device development profile
after the user unlocked the iPad. The profile was checked for the correct team, app identifier,
selected device and development entitlement. It expires on **9 November 2026**.

The MAUI Debug app was signed with that profile and installed successfully. First launch was denied
by iPadOS's security/developer-trust check. After the user trusted their Developer App entry in
Settings, the app launched successfully. Physical startup verified SQLite **3.53.3**, WAL mode,
`synchronous=2` (FULL), Unsent readback, inherited **CompleteUntilFirstUserAuthentication** protection,
backup exclusion and native available-space measurement (about 17.5 GB at the check).

The public CA was converted to DER and temporarily served by a separate HTTP server that exposes
only `.artifacts/ipad/public-ca/`. At the user's request, its certificate URL was opened in Safari
on the iPad. The user confirmed profile installation and full HTTPS trust in Settings.

The current backend uses the isolated demo databases and volume, with LAN HTTPS enabled:

```sh
TabletLan__Enabled=true ./scripts/demo-data.sh start
# After the services are ready, in another terminal:
./scripts/demo-data.sh seed
```

The demo seed completed: two salesperson accounts, four products, four locations and representative
territory assignments. Normal development databases remain separate. Owner-only login instructions
for `colm@demo.sales.local` are in the ignored local `.artifacts/ipad/demo-login.txt`; no password is
stored in tracked documentation. Trusted HTTPS discovery and the API's unauthenticated 401 were
verified during backend setup. Physical browser sign-in and Sync later passed as recorded below.

During the later sign-in attempt, the Mac's DHCP address changed to `192.168.1.17`. The server
certificate was recreated using the existing trusted development CA, AppHost was restarted with
the new authority, and the physical Debug app was rebuilt (zero warnings/errors) and reinstalled
with the updated addresses. Trusted LAN discovery returned 200 and the protected API session
endpoint returned 401 as expected before authentication.

Remote sign-in could not be completed: after USB connection and user approval, Xcode 27 Device Hub
eventually displayed **“Screen Sharing Unavailable”**, stating the iPad must run iOS 27.0 or above.
The attached iPad runs iPadOS 26.7.1. Device installation and launch are supported, but this
screen-control path is unavailable; sign-in and Sync must be exercised on the physical device.

The user reported an invalid-user error during physical sign-in. A read-only account check confirmed
the demo username, confirmed email, Field Salesperson role, no active lockout and a matching password.
The same credentials then authenticated successfully through the live HTTPS login page in Safari on
the Mac; the resulting page explicitly identified the signed-in demo user and correctly denied access
to the administration console. That temporary browser session was signed out. This verifies backend
credentials, not the iPad callback or Sync; the exact iPad input/error remains to be confirmed.

The user subsequently clarified the physical error as **“Sign-in needs a connection. Try again when
you have signal.”** This is the app's generic native HTTP failure message, rather than the browser's
invalid-credentials message. Identity discovery and the API session endpoint remain reachable on the
Mac (200/401 respectively). Local Network permission on the iPad is being checked. Debug-only HTTP
diagnostics now record endpoint host/port/path, status and transport error category, without headers,
bodies, query strings or exception messages; physical reproduction is still required.

The next physical reproduction localized the failure to `/connect/token` after the browser callback.
An opt-in, Debug-only credential-free probe (`FIELDSALES_NETWORK_PROBE=1` at process launch) then
reproduced **NSURLErrorDomain -1202** on both identity discovery and the API session endpoint, confirming
device certificate-trust evaluation was failing. The Mac passes Apple's default ATS check. The public
CA download and current CA have identical SHA-256 fingerprints, the server chain verifies against that
CA, and its SAN includes `192.168.1.17`. The user then enabled full root-certificate trust and confirmed
successful sign-in. TLS validation remains enabled. The diagnostic build and linked desktop integration project both
compile with zero warnings/errors.

Physical logs at 16:13 UTC confirmed `/connect/token` 200, `/staff/session` 200 and `/tablet/snapshot`
200. Sync completed in **813 ms**, with `Locations=2`, `Categories=3`, `Products=3`, `BasePrices=4`
and `ProductAttributes=2`; cleanup completed in 9 ms. This is one initial Debug sync against the small
demo dataset, not a Release performance measurement. The user then confirmed offline cold reopen:
after disabling Wi-Fi and force-closing/reopening the app, both downloaded locations and products
remained usable. The user then created an offline order, confirmed it remained visible after reopening,
and reported that it left Unsent after reconnecting and syncing. A read-only join of demo `Orders`
and `ReceivedWork` independently verified order `f784be89-149e-49ed-8834-1f47c1a0e4f1`, received
**2 November 2026 at 19:31:04 UTC**, for **Arklow Branch**, captured by `fieldsales-demo-colm`.
It contains **2 Each of Demo Moisturising Cream (DEMO-001) at €9.25** and has lifecycle status
**Pending**. The stored JSON order ID and SHA-256 both match the ledger; no changed-body resends
were recorded. Local query evidence is saved in ignored `.artifacts/ipad/upload-verification.json`.
This proves server receipt/storage; Pending is not a later business approval. The current tablet
UI has no Sent-history screen. Its uploader moves an item from Unsent to local Sent only after a
valid server receipt naming that same item.

Physical acceptance still requires the remaining checklist in the setup document: cancellation/roles,
token refresh, rep isolation, interrupted sync,
24-hour access, background/resume, network permission recovery and layout/keyboard checks.

Release builds/signing/distribution, Release AOT/trimming, performance budgets and the formal D4
storage matrix remain deferred by the user's instruction.
