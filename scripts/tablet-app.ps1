[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('DevCertificate', 'Build', 'Install', 'Reverse', 'Launch', 'ForceClose')][string]$Action,
    [string]$DeviceSerial,
    [string]$AndroidSdk = 'C:/Program Files (x86)/Android/android-sdk',
    [ValidateSet('Release', 'Debug')][string]$Configuration = 'Release',
    # Development builds trust this PC's ASP.NET development certificate for localhost (P8); omit for production.
    [switch]$DevTrust,
    # The installed workload manifests copied for WI-032 (see docs/tablet-offline-device-spike-results.md).
    [string]$ManifestRoot = '.artifacts/wi032/installed-manifests',
    [string]$Apk,
    [switch]$Cold
)
$ErrorActionPreference = 'Stop'
$tabletRepo = Split-Path -Parent $PSScriptRoot
$tabletPackage = 'com.fieldsales.tablet'
$tabletOutput = Join-Path $tabletRepo '.artifacts/wi032/tablet-app'
$tabletCertificate = Join-Path $tabletOutput 'fieldsales_dev_cert.der'
$tabletAdb = Join-Path $AndroidSdk 'platform-tools/adb.exe'

function Invoke-TabletAdb {
    param([string[]]$Arguments)
    & $tabletAdb -s $DeviceSerial @Arguments
    if ($LASTEXITCODE -ne 0) { throw "adb command failed (exit $LASTEXITCODE)." }
}
function Assert-TabletDevice {
    if (-not (Test-Path -LiteralPath $tabletAdb -PathType Leaf)) { throw "adb not found: $tabletAdb" }
    if ([string]::IsNullOrWhiteSpace($DeviceSerial)) {
        & $tabletAdb devices -l
        throw 'Supply -DeviceSerial for the tablet you identified and authorized. No device is selected automatically.'
    }
    if ((Invoke-TabletAdb -Arguments @('get-state')).Trim() -ne 'device') { throw 'Selected device is offline or has not authorized debugging.' }
}

Push-Location $tabletRepo
try {
    switch ($Action) {
        'DevCertificate' {
            # Public certificate only. The same file is installed on the tablet so its browser trusts the sign-in page.
            $certificate = Get-ChildItem Cert:\CurrentUser\My |
                Where-Object { $_.Extensions | Where-Object { $_.Oid.Value -eq '1.3.6.1.4.1.311.84.1.1' } } |
                Where-Object { $_.NotAfter -gt (Get-Date) } | Sort-Object NotAfter -Descending | Select-Object -First 1
            if (-not $certificate) { throw 'No valid ASP.NET development certificate. Run: dotnet dev-certs https --trust' }
            New-Item -ItemType Directory -Path $tabletOutput -Force | Out-Null
            [IO.File]::WriteAllBytes($tabletCertificate, $certificate.Export([Security.Cryptography.X509Certificates.X509ContentType]::Cert))
            Write-Output "Exported $($certificate.Subject) ($($certificate.Thumbprint)) to $tabletCertificate"
        }
        'Build' {
            $properties = @("-p:AndroidSdkDirectory=$AndroidSdk")
            if ($DevTrust) {
                if (-not (Test-Path -LiteralPath $tabletCertificate)) { throw 'Run -Action DevCertificate first.' }
                $properties += "-p:FieldSalesDevCertificate=$tabletCertificate"
            }
            $oldTemp = $env:TEMP; $oldTmp = $env:TMP
            $oldManifests = $env:DOTNETSDK_WORKLOAD_MANIFEST_ROOTS; $oldIgnore = $env:DOTNETSDK_WORKLOAD_MANIFEST_IGNORE_DEFAULT_ROOTS
            $buildTemp = Join-Path $tabletRepo '.artifacts/wi032/build-temp'
            New-Item -ItemType Directory -Path $buildTemp -Force | Out-Null
            $env:TEMP = $buildTemp; $env:TMP = $buildTemp
            if (Test-Path -LiteralPath $ManifestRoot) {
                $env:DOTNETSDK_WORKLOAD_MANIFEST_ROOTS = (Resolve-Path -LiteralPath $ManifestRoot).Path
                $env:DOTNETSDK_WORKLOAD_MANIFEST_IGNORE_DEFAULT_ROOTS = '1'
            }
            try {
                & dotnet publish src/FieldSales.Tablet/FieldSales.Tablet.csproj -f net10.0-android -c $Configuration -m:1 @properties -o (Join-Path $tabletOutput 'apk')
                if ($LASTEXITCODE -ne 0) { throw 'Android build failed.' }
            } finally {
                $env:TEMP = $oldTemp; $env:TMP = $oldTmp
                $env:DOTNETSDK_WORKLOAD_MANIFEST_ROOTS = $oldManifests; $env:DOTNETSDK_WORKLOAD_MANIFEST_IGNORE_DEFAULT_ROOTS = $oldIgnore
            }
            Get-ChildItem -LiteralPath (Join-Path $tabletOutput 'apk') -Filter '*-Signed.apk' | ForEach-Object { Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256 }
        }
        'Install' {
            Assert-TabletDevice
            if (-not $Apk) { $Apk = (Get-ChildItem -LiteralPath (Join-Path $tabletOutput 'apk') -Filter '*-Signed.apk' | Select-Object -First 1).FullName }
            if (-not $Apk -or -not (Test-Path -LiteralPath $Apk -PathType Leaf)) { throw 'Build the APK first, or supply -Apk.' }
            Invoke-TabletAdb -Arguments @('install', '-r', (Resolve-Path -LiteralPath $Apk).Path)
        }
        # The tablet reaches this PC's identity host (7201) and API (7204) over USB.
        'Reverse' { Assert-TabletDevice; foreach ($port in 7201, 7204) { Invoke-TabletAdb -Arguments @('reverse', "tcp:$port", "tcp:$port") } }
        'Launch' {
            Assert-TabletDevice
            if ($Cold) { Invoke-TabletAdb -Arguments @('shell', 'am', 'force-stop', $tabletPackage) }
            $component = (Invoke-TabletAdb -Arguments @('shell', 'cmd', 'package', 'resolve-activity', '--brief', $tabletPackage) | Select-Object -Last 1).Trim()
            if (-not $component.StartsWith($tabletPackage + '/')) { throw 'Could not resolve the tablet app launcher.' }
            Invoke-TabletAdb -Arguments @('shell', 'am', 'start', '-W', '-n', $component)
        }
        'ForceClose' { Assert-TabletDevice; Invoke-TabletAdb -Arguments @('shell', 'am', 'force-stop', $tabletPackage) }
    }
} finally { Pop-Location }
