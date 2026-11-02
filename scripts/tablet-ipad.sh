#!/bin/bash
# Debug-only iPad development. See docs/tablet-ipad-development.md.
set -euo pipefail
task_repo="$(cd "$(dirname "$0")/.." && pwd)"
task_project="$task_repo/src/FieldSales.Tablet/FieldSales.Tablet.csproj"
task_host_project="$task_repo/src/FieldSales.AppHost/FieldSales.AppHost.csproj"
task_output="$task_repo/.artifacts/ipad"
task_action="${1:-help}"
if [[ $# -gt 0 ]]; then shift; fi
task_device="" task_host="" task_identity="" task_profile="" task_simulator=false

fail() { echo "$*" >&2; exit 1; }
usage() {
    cat <<'HELP'
Usage: scripts/tablet-ipad.sh ACTION [options]
Actions:
  certificates --host LAN_IPV4     Create trusted local HTTPS certificates and save AppHost user secrets (requires mkcert).
  backend                         Start AppHost with the Development-only LAN profile saved by certificates.
  preflight --device UDID [--simulator] [--host LAN_IPV4]
  build --device UDID --host LAN_IPV4 [--simulator | --identity NAME_OR_HASH --profile UUID_OR_NAME]
  install --device UDID [--simulator]
  launch --device UDID [--simulator]
  logs --device UDID [--simulator]  Relaunch with console attached; Ctrl-C stops capture.

Builds are always Debug. Device signing options are required for physical builds.
The simulator and device have separate output directories. No device is selected automatically.
HELP
}
while [[ $# -gt 0 ]]; do
    case "$1" in
        --simulator) task_simulator=true; shift ;;
        --device|--host|--identity|--profile)
            [[ $# -ge 2 && -n "$2" ]] || fail "Missing value for $1"
            case "$1" in
                --device) task_device="$2" ;;
                --host) task_host="$2" ;;
                --identity) task_identity="$2" ;;
                --profile) task_profile="$2" ;;
            esac
            shift 2 ;;
        *) fail "Unknown option: $1" ;;
    esac
done
cd "$task_repo"
task_rid=ios-arm64
if $task_simulator; then task_rid=iossimulator-arm64; fi
task_app="$task_output/$task_rid/FieldSales.Tablet.app"

require_host() {
    [[ -n "$task_host" ]] || fail "Supply --host with this Mac's LAN IPv4 address."
    awk -v ip="$task_host" 'BEGIN {
        if (split(ip,a,".") != 4) exit 1;
        for (i=1;i<=4;i++) if (a[i] !~ /^[0-9]+$/ || a[i]>255) exit 1;
        if (a[1]==0 || a[1]==127 || a[1]>=224) exit 1;
    }' || fail "--host must be a LAN IPv4 address without a scheme, port or path."
}
require_device() { [[ -n "$task_device" ]] || fail "Supply --device explicitly (use xcrun devicectl list devices or xcrun simctl list devices available)."; }
check_backend() {
    require_host
    # curl and the app both use normal certificate/hostname validation. No --insecure.
    curl --fail --silent --show-error --connect-timeout 5 --max-time 10 \
        "https://$task_host:7201/.well-known/openid-configuration" -o /dev/null
    # The protected session endpoint should answer 401 when no credentials are supplied.
    local task_status
    task_status="$(curl --silent --show-error --connect-timeout 5 --max-time 10 \
        -o /dev/null -w '%{http_code}' "https://$task_host:7204/staff/session")"
    [[ "$task_status" == 401 ]] || fail "Expected API /staff/session to return 401, received $task_status."
}
case "$task_action" in
    help|--help|-h) usage ;;
    certificates)
        require_host
        command -v mkcert >/dev/null || fail "Install mkcert first: brew install mkcert"
        umask 077
        task_certs="$task_output/certificates"
        mkdir -p "$task_certs"
        mkcert -cert-file "$task_certs/server.crt" -key-file "$task_certs/server.key" \
            "$task_host" localhost 127.0.0.1 ::1
        # Trust for the current macOS user; mkcert -install uses the system keychain and requires sudo.
        security add-trusted-cert -r trustRoot -k "$HOME/Library/Keychains/login.keychain-db" "$(mkcert -CAROOT)/rootCA.pem"
        task_cert_password="$(openssl rand -hex 32)"
        export FIELDSALES_CERT_EXPORT_PASSWORD="$task_cert_password"
        openssl pkcs12 -export -out "$task_certs/server.pfx" -inkey "$task_certs/server.key" \
            -in "$task_certs/server.crt" -passout env:FIELDSALES_CERT_EXPORT_PASSWORD
        python3 - "$task_certs/server.pfx" "$task_host" <<'PY' | dotnet user-secrets set --project "$task_host_project" >/dev/null
import json, os, sys
json.dump({
    "Parameters:tablet-lan-certificate-password": os.environ["FIELDSALES_CERT_EXPORT_PASSWORD"],
    "TabletLan:CertificatePath": sys.argv[1],
    "TabletLan:Host": sys.argv[2]
}, sys.stdout)
PY
        unset FIELDSALES_CERT_EXPORT_PASSWORD task_cert_password
        cp "$(mkcert -CAROOT)/rootCA.pem" "$task_certs/rootCA.crt"
        echo "AppHost configured. Install the public CA on the iPad and enable full trust: $task_certs/rootCA.crt"
        echo "Keep the CA private key on this Mac. Restart the backend and rebuild the app if the LAN address changes."
        ;;
    backend)
        TabletLan__Enabled=true dotnet run --project "$task_host_project" --launch-profile https
        ;;
    preflight)
        require_device
        dotnet --version
        xcodebuild -version
        if $task_simulator; then
            xcrun simctl list devices available
        else
            xcrun devicectl device info details --device "$task_device" --timeout 15
            security find-identity -v -p codesigning
        fi
        if [[ -n "$task_host" ]]; then check_backend; fi
        ;;
    build)
        require_device
        check_backend
        task_signing=()
        if $task_simulator; then
            # Local ad-hoc signing validates native libraries/Keychain entitlements; no Apple account/profile.
            task_signing+=('-p:CodesignKey=-')
        else
            [[ -n "$task_identity" && -n "$task_profile" ]] || fail "Physical Debug builds require --identity and --profile from your Xcode development setup."
            task_signing+=("-p:CodesignKey=$task_identity" "-p:CodesignProvision=$task_profile")
        fi
        dotnet build "$task_project" -f net10.0-ios -c Debug -r "$task_rid" -m:1 \
            "-p:FieldSalesApiBaseUrl=https://$task_host:7204/" \
            "-p:FieldSalesIdentityBaseUrl=https://$task_host:7201/" \
            "-p:OutputPath=$task_output/$task_rid/" \
            -p:AppendTargetFrameworkToOutputPath=false -p:AppendRuntimeIdentifierToOutputPath=false \
            "${task_signing[@]}"
        [[ -d "$task_app" ]] || fail "Build did not produce $task_app"
        echo "Built Debug app: $task_app"
        ;;
    install)
        require_device
        [[ -d "$task_app" ]] || fail "Build the selected device/simulator app first."
        if $task_simulator; then
            xcrun simctl bootstatus "$task_device" -b
            xcrun simctl install "$task_device" "$task_app"
        else
            xcrun devicectl device install app --device "$task_device" "$task_app"
        fi
        ;;
    launch|logs)
        require_device
        if $task_simulator; then
            if [[ "$task_action" == logs ]]; then
                xcrun simctl launch --terminate-running-process --console "$task_device" com.fieldsales.tablet
            else
                xcrun simctl launch --terminate-running-process "$task_device" com.fieldsales.tablet
            fi
        else
            if [[ "$task_action" == logs ]]; then
                xcrun devicectl device process launch --device "$task_device" --terminate-existing --console com.fieldsales.tablet
            else
                xcrun devicectl device process launch --device "$task_device" --terminate-existing com.fieldsales.tablet
            fi
        fi
        ;;
    *) usage; fail "Unknown action: $task_action" ;;
esac
