<#
.SYNOPSIS
Runs one test tier. See README § Testing and docs/test-speed-review-2026-11-01.md.

.DESCRIPTION
Fast       Inner loop: FieldSales.Fast.slnx (unit, tablet components, Tablet.Core). No Docker.
WorkItem   Before committing: Fast, then every Docker-free integration class in the API, Admin and Web
           projects at default parallelism. -Area adds the SQL Server classes for those areas.
SqlServer  Every class tagged Requires=SqlServer, including migration and adoption tests.
All        What CI runs: Fast, then every integration project with the CI filter and settings.

SQL Server runs use sqlserver.runsettings and run one project at a time, as CI does. A plain
`dotnet test FieldSales.slnx` starts too many SQL Server containers at once.

.EXAMPLE
./scripts/test.ps1
./scripts/test.ps1 -Tier WorkItem -Area Coverage,TabletSync
./scripts/test.ps1 -Tier All -ResultsDirectory TestResults
#>
param (
    [ValidateSet("Fast", "WorkItem", "SqlServer", "All")]
    [string]$Tier = "Fast",
    # Catalogue, Directory, Coverage, TabletSync or Identity; several may be given, comma-separated.
    [string[]]$Area = @(),
    [switch]$NoBuild,
    [string]$ResultsDirectory = ""
)

$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$runSettings = Join-Path $repositoryRoot "sqlserver.runsettings"
# The Web project has three SQL collections (Catalogue, Directory, Coverage) and runs them at once.
$webRunSettings = Join-Path $repositoryRoot "sqlserver-web.runsettings"

function Get-TestProject([string]$Name) {
    Join-Path $repositoryRoot "tests/Integration/$Name/$Name.IntegrationTests.csproj"
}

$api = Get-TestProject "FieldSales.Api"
$admin = Get-TestProject "FieldSales.Identity.Admin"
$web = Get-TestProject "FieldSales.Web"
$demoData = Get-TestProject "FieldSales.DemoData"
# Tablet.Core and Tablet.Components are not listed: FieldSales.Fast.slnx already runs all of their tests.
$integrationProjects = @(
    $api, $admin, $web, $demoData,
    (Get-TestProject "FieldSales.SnapshotSpike"),
    (Get-TestProject "FieldSales.TabletSpike")
)
# The projects holding each area's SQL Server classes.
$areaProjects = @{
    Catalogue = @($api, $web); Directory = @($api, $web); Coverage = @($api, $web)
    TabletSync = @($api); Identity = @($admin)
}

# `pwsh -File` passes "A,B" as one string, so split here rather than relying on PowerShell's array parsing.
$Area = @($Area | ForEach-Object { $_ -split "," } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
$Area = @(foreach ($name in $Area) {
    $canonical = $areaProjects.Keys | Where-Object { $_ -ieq $name }
    if (-not $canonical) { throw "Unknown area '$name'. Choose from: $($areaProjects.Keys -join ', ')." }
    $canonical
})
if ($Area.Count -gt 0 -and $Tier -ne "WorkItem") {
    throw "-Area applies only to -Tier WorkItem."
}

$steps = [System.Collections.Generic.List[object]]::new()
$failed = $false

function Invoke-TestStep {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Target,
        [string]$Filter = "",
        [switch]$SqlServer,
        [switch]$Build
    )

    $arguments = @("test", $Target)
    if (-not $Build) { $arguments += @("--no-build", "--no-restore") }
    if ($Filter) { $arguments += @("--filter", $Filter) }
    # The thread cap protects SQL Server memory; Docker-free runs use xUnit's default parallelism.
    if ($SqlServer) {
        $settings = if ($Target -eq $web) { $webRunSettings } else { $runSettings }
        $arguments += @("-m:1", "--settings", $settings, "--blame-hang-timeout", "2m")
    }
    if ($ResultsDirectory) {
        $arguments += @("--logger", "trx", "--results-directory", (Join-Path $ResultsDirectory ($Name -replace '[^\w.-]', '-')))
    }

    Write-Host "`n=== $Name" -ForegroundColor Cyan
    Write-Host "dotnet $($arguments -join ' ')"
    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    & dotnet @arguments
    $exitCode = $LASTEXITCODE
    $timer.Stop()
    $script:steps.Add([pscustomobject]@{
        Step = $Name; Seconds = [math]::Round($timer.Elapsed.TotalSeconds, 1)
        Result = if ($exitCode -eq 0) { "passed" } else { "FAILED ($exitCode)" }
    })
    if ($exitCode -ne 0) { $script:failed = $true }
}

function Assert-Docker {
    & docker info --format "{{.ServerVersion}}" *> $null
    if ($LASTEXITCODE -ne 0) {
        throw "Docker is not running. Tier '$Tier' needs SQL Server containers; start Docker Desktop, or run -Tier Fast."
    }
}

function Get-ProjectLabel([string]$Project) {
    [System.IO.Path]::GetFileNameWithoutExtension($Project) -replace '^FieldSales\.', '' -replace '\.IntegrationTests$', ''
}

$needsDocker = $Tier -in @("SqlServer", "All") -or $Area.Count -gt 0
if ($needsDocker) { Assert-Docker }

Push-Location $repositoryRoot
try {
    if ($Tier -eq "Fast") {
        Invoke-TestStep -Name "Fast" -Target "FieldSales.Fast.slnx" -Build:(-not $NoBuild)
    }
    else {
        if (-not $NoBuild) {
            Write-Host "`n=== Build FieldSales.slnx" -ForegroundColor Cyan
            & dotnet build FieldSales.slnx
            if ($LASTEXITCODE -ne 0) { throw "dotnet build FieldSales.slnx failed with exit code $LASTEXITCODE." }
        }

        if ($Tier -in @("WorkItem", "All")) {
            Invoke-TestStep -Name "Fast" -Target "FieldSales.Fast.slnx"
        }

        if ($Tier -eq "WorkItem") {
            foreach ($project in @($api, $admin, $web)) {
                Invoke-TestStep -Name "$(Get-ProjectLabel $project) Docker-free" -Target $project `
                    -Filter "Category!=Unit&Requires!=SqlServer"
            }
            if ($Area.Count -gt 0) {
                $areaFilter = ($Area | ForEach-Object { "Area=$_" }) -join "|"
                $projects = $Area | ForEach-Object { $areaProjects[$_] } | Select-Object -Unique
                foreach ($project in $projects) {
                    Invoke-TestStep -Name "$(Get-ProjectLabel $project) SQL ($($Area -join ', '))" -Target $project `
                        -Filter "Requires=SqlServer&($areaFilter)" -SqlServer
                }
            }
        }

        if ($Tier -eq "SqlServer") {
            foreach ($project in @($api, $admin, $web, $demoData)) {
                Invoke-TestStep -Name "$(Get-ProjectLabel $project) SQL" -Target $project -Filter "Requires=SqlServer" -SqlServer
            }
        }

        if ($Tier -eq "All") {
            foreach ($project in $integrationProjects) {
                Invoke-TestStep -Name "$(Get-ProjectLabel $project)" -Target $project -Filter "Category!=Unit" -SqlServer
            }
        }
    }
}
finally {
    Pop-Location
}

Write-Host "`n=== Summary (tier $Tier$(if ($Area.Count) { ', areas ' + ($Area -join ', ') }))" -ForegroundColor Cyan
$steps | Format-Table -AutoSize | Out-Host
$total = ($steps | Measure-Object -Property Seconds -Sum).Sum
Write-Host "Test time: $([math]::Round($total, 1)) s"
if ($failed) { exit 1 }
