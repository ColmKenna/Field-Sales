# iPad Debug development

The production tablet app supports `net10.0-ios` on macOS alongside Android. This workflow builds
**Debug only**. Release signing/distribution, Release trimming/AOT and formal D4 storage/performance
acceptance remain deferred. See [the implementation results](tablet-ipad-development-results.md).

## Prerequisites

- .NET 10 SDK, MAUI/iOS workload and a matching Xcode, selected with `xcode-select`.
- An iPad running iPadOS **16.4 or newer**, paired with this Mac, unlocked, with Developer Mode enabled.
- Mac and iPad on a reachable LAN, without client isolation; allow incoming traffic to ports 7201/7204.
- Docker Desktop running and the existing AppHost database/password setup from the [README](../README.md).
- `mkcert` (`brew install mkcert`) for a local development CA. No certificate-validation bypass is used.

The centrally pinned MAUI Controls and Blazor WebView packages are **10.0.110**. The scene delegate
and scene manifest are required for Xcode 27 launches; this package version also forwards scene
authentication callbacks correctly. Cross-platform `Window` lifecycle events continue to enforce
session access. See [Microsoft's Xcode 27 guidance](https://devblogs.microsoft.com/dotnet/migrate-dotnet-maui-apps-uikit-scene-lifecycle/).

`scripts/tablet-ipad.sh help` describes every action. Device identifiers are always explicit. Discover
them with `xcrun devicectl list devices` or `xcrun simctl list devices available`.

## HTTPS and the backend

Find the Mac's active LAN IPv4 address in System Settings → Network. For Wi-Fi, the interface may be
`en0` or `en1`; `route -n get default` identifies it, then `ipconfig getifaddr en1` reads its address.

```sh
./scripts/tablet-ipad.sh certificates --host 192.168.1.18
```

Replace the example address with the Mac's current address. Approve macOS's login-keychain trust
prompt. This creates a server certificate covering the LAN address, localhost and loopback addresses.
It saves `TabletLan:Host`, `TabletLan:CertificatePath` and the secret
`Parameters:tablet-lan-certificate-password` in AppHost user secrets. Certificate files are under the
ignored `.artifacts/ipad/certificates/` directory, with private files accessible only to the current user.

Transfer **only** `rootCA.crt` to the iPad, for example with AirDrop. Install the downloaded profile in
Settings → General → VPN & Device Management, then enable the CA under General → About → Certificate
Trust Settings. Never transfer `server.key`, `server.pfx`, or mkcert's `rootCA-key.pem`.

If the browser returns to the app but sign-in reports no connection, capture the native HTTP error
with `scripts/tablet-ipad.sh logs --device IPAD_UDID`. Debug logs include endpoint host/port/path,
status and iOS error domain/code, without tokens, query strings or response bodies. Error
`NSURLErrorDomain -1202` indicates server certificate trust failed; check full root trust under
Settings → General → About → Certificate Trust Settings, in addition to installing the profile.
To test native HTTPS without entering credentials, launch the Debug app with an opt-in probe:

```sh
xcrun devicectl device process launch --device IPAD_UDID --terminate-existing --console \
  --environment-variables '{"FIELDSALES_NETWORK_PROBE":"1"}' com.fieldsales.tablet
```

This sends unauthenticated requests to discovery and `/staff/session`. It does not change TLS trust
or sign in. The probe and HTTP handler diagnostics are excluded from Release builds.

For a simulator, boot the intended device and install the public root:

```sh
xcrun simctl boot SIMULATOR_UDID
xcrun simctl bootstatus SIMULATOR_UDID -b
xcrun simctl keychain SIMULATOR_UDID add-root-cert .artifacts/ipad/certificates/rootCA.crt
```

Start the backend in another terminal:

```sh
./scripts/tablet-ipad.sh backend
```

The Development-only `TabletLan:Enabled` switch exposes direct HTTPS Kestrel listeners on 7201 and
7204. Identity, API and the staff website use the same LAN identity authority. The dashboard and staff
website remain accessible on this Mac at their existing addresses. A normal AppHost launch leaves
the LAN profile disabled. If the Mac address changes, recreate the certificate, restart AppHost and
rebuild the tablet app. Existing Android localhost builds should use the normal AppHost profile, or
be rebuilt with the LAN URL build properties when using this profile.

Open `https://MAC_LAN_IP:7201/.well-known/openid-configuration` in Safari on the iPad. It must load
without a trust warning and advertise the LAN identity address. The API's `/staff/session` should
return 401 without a token. The helper checks both endpoints before building.

## Personal Team development signing

This is local **development** signing, required even for a USB-connected Debug app. No distribution
certificate, App Store registration, TestFlight setup or Release signing is required.

1. In Xcode → Settings → Accounts, add your Apple ID and select your Personal Team.
2. Create a temporary native iOS app outside this repository with bundle identifier
   `com.fieldsales.tablet`. Select your iPad as its destination and enable automatic signing for
   your Personal Team. Build/run it once to have Xcode create a development certificate/profile
   for this bundle identifier and device.
3. Read the development identity with `security find-identity -v -p codesigning`. Use its exact name
   or hash, and the matching provisioning profile's UUID/name. Profiles normally live under
   `~/Library/Developer/Xcode/UserData/Provisioning Profiles/` (older Xcode versions use
   `~/Library/MobileDevice/Provisioning Profiles/`). The profile must cover this bundle ID and iPad.
4. Supply these values as local command arguments. Do not commit account/team/profile identifiers.
   Personal Team provisioning is temporary; renew it in Xcode when it expires. The native sample is
   only a provisioning aid; the helper installs the MAUI app over that bundle identifier.

```sh
./scripts/tablet-ipad.sh preflight --device IPAD_UDID --host MAC_LAN_IP
./scripts/tablet-ipad.sh build --device IPAD_UDID --host MAC_LAN_IP \
  --identity 'Apple Development: YOUR NAME (IDENTIFIER)' --profile PROFILE_UUID
./scripts/tablet-ipad.sh install --device IPAD_UDID
./scripts/tablet-ipad.sh launch --device IPAD_UDID
./scripts/tablet-ipad.sh logs --device IPAD_UDID
```

Keep the iPad unlocked during installation/launch. Approve device trust and local-network permission
prompts. A denied network permission can be restored in Settings → Privacy & Security → Local Network.
A locked device, missing profile or TLS trust failure must be resolved before sign-in/sync testing.

## Simulator development

```sh
./scripts/tablet-ipad.sh preflight --device SIMULATOR_UDID --simulator --host MAC_LAN_IP
./scripts/tablet-ipad.sh build --device SIMULATOR_UDID --simulator --host MAC_LAN_IP
./scripts/tablet-ipad.sh install --device SIMULATOR_UDID --simulator
./scripts/tablet-ipad.sh logs --device SIMULATOR_UDID --simulator
```

Simulator builds use `iossimulator-arm64` and separate output from physical `ios-arm64` builds.
The simulator Keychain entitlement is not applied to physical builds. For a compile-only check
before the backend is ready:

```sh
dotnet build src/FieldSales.Tablet/FieldSales.Tablet.csproj \
  -f net10.0-ios -c Debug -r iossimulator-arm64 -p:CodesignKey=- -m:1
```

## Storage, diagnostics and acceptance

iOS files live under `Library/Application Support/FieldSales` in the app sandbox. This root is
excluded from backup; new descendants inherit `CompleteUntilFirstUserAuthentication` protection.
Snapshot and Unsent schemas/durability remain unchanged. First launch of an installation clears
known Field Sales session/token entries if its local installation marker does not match Keychain.
It leaves saved work and unrelated Keychain entries alone. Normal relaunches retain the session.

Debug startup probes the bundled SQLite provider with temporary snapshot/Unsent stores, verifies
readback and physical-device file protection, logs the engine/WAL/FULL/free-space result, and removes
the temporary stores. Console output uses `FieldSalesTiming`. iOS startup timing begins at the first
managed timing mark; it excludes native startup and is not comparable to Android process-start timing.

Record each physical-device outcome in the results document:

- Successful sign-in and callback, cancelled browser sign-in, non-salesperson refusal and token refresh.
- Sign-out followed by a second rep's sign-in, confirming that neither rep sees the other's data/work.
- Initial sync; airplane-mode cold launch; product/location search; order saved before force-close
  and present after reopening; restore connectivity and upload successfully.
- Interrupted sync retains the previous complete snapshot and saved work; a retry succeeds.
- Background/resume and expiry still enforce the 24-hour window without extending it.
- Portrait/landscape, scrolling, keyboard, safe areas and local-network permission denial/recovery.

Never claim physical acceptance from simulator or desktop tests. Release performance and D4 remain
open until their separate device scenarios have been executed.
