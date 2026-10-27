[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('UseInstalledPacks', 'Build', 'Host', 'Preflight', 'Install', 'Launch', 'ForceClose', 'Reverse', 'PullReports')][string]$Action,
    [string]$DeviceSerial,
    [string]$AndroidSdk = 'C:/Program Files (x86)/Android/android-sdk',
    [string]$Fixtures = '.artifacts/wi032/final-profiles',
    [string]$Bind = 'http://127.0.0.1:7206',
    [string]$Apk,
    [string]$ReportDirectory = '.artifacts/wi032/samsung-reports',
    [string]$ManifestRoot,
    [switch]$Cold
)
$ErrorActionPreference = 'Stop'
$spikeRepo = Split-Path -Parent $PSScriptRoot
$spikePackage = 'com.fieldsales.tabletspike'
$spikeOutput = Join-Path $spikeRepo '.artifacts/wi032/1b-android'
$spikeAdb = Join-Path $AndroidSdk 'platform-tools/adb.exe'

function Invoke-SpikeAdb {
    param([string[]]$Arguments)
    & $spikeAdb -s $DeviceSerial @Arguments
    if ($LASTEXITCODE -ne 0) { throw "adb command failed (exit $LASTEXITCODE)." }
}
function Assert-SpikeDevice {
    if (-not (Test-Path -LiteralPath $spikeAdb -PathType Leaf)) { throw "adb not found: $spikeAdb" }
    if ([string]::IsNullOrWhiteSpace($DeviceSerial)) {
        & $spikeAdb devices -l
        throw 'Supply -DeviceSerial for the Samsung you identified and authorized. No device is selected automatically.'
    }
    $spikeDeviceState = Invoke-SpikeAdb -Arguments @('get-state')
    if ($spikeDeviceState.Trim() -ne 'device') { throw 'Selected device is offline or has not authorized debugging.' }
    $spikeApi = Invoke-SpikeAdb -Arguments @('shell', 'getprop', 'ro.build.version.sdk')
    if ([int]$spikeApi -lt 24) { throw 'MAUI Hybrid requires Android API 24 or newer. Review this device; do not lower the app floor.' }
}
Push-Location $spikeRepo
try {
    switch ($Action) {
        'UseInstalledPacks' {
            # Workstation recovery: copy unchanged manifests matching its original installed packs.
            # This action neither installs workloads nor writes to the machine's dotnet directories.
            $spikeDotnetRoot = Split-Path -Parent (Get-Command dotnet).Source
            $spikeManifestBase = Join-Path $spikeDotnetRoot 'sdk-manifests/10.0.100'
            $spikeSdkVersion = (& dotnet --version).Trim()
            $spikeSdkParts = $spikeSdkVersion.Split('.')
            $spikeFeatureBand = "$($spikeSdkParts[0]).$($spikeSdkParts[1]).$([math]::Floor([int]$spikeSdkParts[2] / 100) * 100)"
            if (-not $ManifestRoot) { $ManifestRoot = Join-Path $spikeRepo '.artifacts/wi032/installed-manifests' }
            $spikeOriginalVersions = [ordered]@{
                'microsoft.net.sdk.android' = '36.1.69'; 'microsoft.net.sdk.maui' = '10.0.20'
                'microsoft.net.sdk.ios' = '26.5.10301'; 'microsoft.net.sdk.maccatalyst' = '26.5.10301'
                'microsoft.net.sdk.macos' = '26.5.10301'; 'microsoft.net.sdk.tvos' = '26.5.10301'
            }
            foreach ($spikeKind in @('current', 'net6', 'net7', 'net8', 'net9')) {
                $spikeOriginalVersions["microsoft.net.workload.emscripten.$spikeKind"] = '10.0.112'
                $spikeOriginalVersions["microsoft.net.workload.mono.toolchain.$spikeKind"] = '10.0.112'
            }
            foreach ($spikeEntry in $spikeOriginalVersions.GetEnumerator()) {
                $spikeSource = Join-Path $spikeManifestBase "$($spikeEntry.Key)/$($spikeEntry.Value)"
                if (-not (Test-Path -LiteralPath "$spikeSource/WorkloadManifest.json")) { throw "Original installed manifest unavailable: $spikeSource" }
                $spikeDestination = Join-Path $ManifestRoot "$spikeFeatureBand/$($spikeEntry.Key)/$($spikeEntry.Value)"
                New-Item -ItemType Directory -Path $spikeDestination -Force | Out-Null
                Get-ChildItem -LiteralPath $spikeSource -File | Copy-Item -Destination $spikeDestination -Force
            }
            $spikeOriginalVersions | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ManifestRoot 'versions.json')
            Write-Output "Copied original manifests for SDK $spikeSdkVersion. Build with -ManifestRoot '$ManifestRoot'."
        }
        'Build' {
            New-Item -ItemType Directory -Path $spikeOutput -Force | Out-Null
            $spikeOldTemp = $env:TEMP; $spikeOldTmp = $env:TMP
            $spikeOldManifests = $env:DOTNETSDK_WORKLOAD_MANIFEST_ROOTS
            $spikeOldIgnoreManifests = $env:DOTNETSDK_WORKLOAD_MANIFEST_IGNORE_DEFAULT_ROOTS
            $spikeBuildTemp = Join-Path $spikeRepo '.artifacts/wi032/build-temp'
            New-Item -ItemType Directory -Path $spikeBuildTemp -Force | Out-Null
            $env:TEMP = $spikeBuildTemp; $env:TMP = $spikeBuildTemp
            if ($ManifestRoot) {
                $env:DOTNETSDK_WORKLOAD_MANIFEST_ROOTS = (Resolve-Path -LiteralPath $ManifestRoot).Path
                $env:DOTNETSDK_WORKLOAD_MANIFEST_IGNORE_DEFAULT_ROOTS = '1'
            }
            Push-Location (Join-Path $spikeRepo 'tools/FieldSales.TabletSpike')
            try {
                & dotnet publish FieldSales.TabletSpike.csproj -f net10.0-android -c Release -m:1 "-p:AndroidSdkDirectory=$AndroidSdk" -o $spikeOutput
                if ($LASTEXITCODE -ne 0) { throw 'Android Release build failed.' }
            } finally {
                Pop-Location
                $env:TEMP = $spikeOldTemp; $env:TMP = $spikeOldTmp
                $env:DOTNETSDK_WORKLOAD_MANIFEST_ROOTS = $spikeOldManifests
                $env:DOTNETSDK_WORKLOAD_MANIFEST_IGNORE_DEFAULT_ROOTS = $spikeOldIgnoreManifests
            }
            Get-ChildItem -LiteralPath $spikeOutput -Filter '*-Signed.apk' | ForEach-Object { Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256 }
        }
        'Host' {
            $spikeFixtures = (Resolve-Path -LiteralPath $Fixtures).Path
            # Foreground host is intentional; Ctrl+C stops it. No firewall or production settings are changed.
            & dotnet run --project tools/FieldSales.TabletSpike.Host --no-launch-profile -- --fixtures $spikeFixtures --urls $Bind
            if ($LASTEXITCODE -ne 0) { throw 'Synthetic fixture host exited with an error.' }
        }
        'Preflight' {
            Assert-SpikeDevice
            $spikePreflight = [ordered]@{ package = $spikePackage; capturedUtc = [DateTimeOffset]::UtcNow.ToString('O') }
            foreach ($spikeProperty in @('ro.product.manufacturer', 'ro.product.model', 'ro.build.version.release', 'ro.build.version.sdk', 'ro.product.cpu.abilist', 'ro.kernel.qemu')) {
                $spikePreflight[$spikeProperty] = (Invoke-SpikeAdb -Arguments @('shell', 'getprop', $spikeProperty)) -join "`n"
            }
            $spikePreflight['webView'] = (Invoke-SpikeAdb -Arguments @('shell', 'dumpsys', 'webviewupdate')) -join "`n"
            New-Item -ItemType Directory -Path $ReportDirectory -Force | Out-Null
            $spikePreflight | ConvertTo-Json -Depth 5 | Tee-Object -FilePath (Join-Path $ReportDirectory 'preflight.json')
        }
        'Install' {
            Assert-SpikeDevice
            if (-not $Apk) { $Apk = (Get-ChildItem -LiteralPath $spikeOutput -Filter '*-Signed.apk' | Select-Object -First 1).FullName }
            if (-not $Apk -or -not (Test-Path -LiteralPath $Apk -PathType Leaf)) { throw 'Build the Release APK first, or supply -Apk.' }
            $spikeAapt = Get-ChildItem -LiteralPath (Join-Path $AndroidSdk 'build-tools') -Filter aapt.exe -Recurse | Sort-Object FullName -Descending | Select-Object -First 1
            if (-not $spikeAapt) { throw 'Android build-tools aapt is required to verify the package before installation.' }
            $spikeBadging = & $spikeAapt.FullName dump badging $Apk
            if ($LASTEXITCODE -ne 0 -or -not ($spikeBadging -match "^package: name='$([regex]::Escape($spikePackage))'")) { throw 'APK package verification failed.' }
            Invoke-SpikeAdb -Arguments @('install', '-r', (Resolve-Path -LiteralPath $Apk).Path)
        }
        'Launch' {
            Assert-SpikeDevice
            if ($Cold) { Invoke-SpikeAdb -Arguments @('shell', 'am', 'force-stop', $spikePackage) }
            $spikeComponent = (Invoke-SpikeAdb -Arguments @('shell', 'cmd', 'package', 'resolve-activity', '--brief', $spikePackage) | Select-Object -Last 1).Trim()
            if (-not $spikeComponent.StartsWith($spikePackage + '/')) { throw 'Could not resolve the spike package launcher.' }
            New-Item -ItemType Directory -Path $ReportDirectory -Force | Out-Null
            $spikeLaunch = Invoke-SpikeAdb -Arguments @('shell', 'am', 'start', '-W', '-n', $spikeComponent)
            "coldProcess=$Cold; OS cache uncontrolled; Android am start clock only" | Set-Content -LiteralPath (Join-Path $ReportDirectory 'last-launch.txt')
            $spikeLaunch | Tee-Object -FilePath (Join-Path $ReportDirectory 'last-launch.txt') -Append
        }
        'ForceClose' { Assert-SpikeDevice; Invoke-SpikeAdb -Arguments @('shell', 'am', 'force-stop', $spikePackage) }
        'Reverse' { Assert-SpikeDevice; Invoke-SpikeAdb -Arguments @('reverse', 'tcp:7206', 'tcp:7206') }
        'PullReports' {
            Assert-SpikeDevice
            New-Item -ItemType Directory -Path $ReportDirectory -Force | Out-Null
            # Tap Export in the app first. Private databases/other app packages are never copied.
            Invoke-SpikeAdb -Arguments @('pull', "/sdcard/Android/data/$spikePackage/files/reports", (Resolve-Path -LiteralPath $ReportDirectory).Path)
        }
    }
} finally { Pop-Location }
