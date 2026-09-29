param (
    [string]$OutputDir = "artifacts/migrations",
    [string]$RuntimeIdentifier = ""
)

$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$identityProjectPath = Join-Path $repositoryRoot "FieldSales.Identity/src/FieldSales.Identity/FieldSales.Identity.csproj"
$staffWebProjectPath = Join-Path $repositoryRoot "FieldSales.Web/FieldSales.Web.csproj"
$catalogueProjectPath = Join-Path $repositoryRoot "FieldSales.Api/FieldSales.Api.csproj"
$resolvedOutputDir = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputDir))

New-Item -ItemType Directory -Path $resolvedOutputDir -Force | Out-Null

function Invoke-DotNet {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

Invoke-DotNet -Arguments @("tool", "restore")

$bundles = @(
    @{ Context = "ApplicationDbContext"; Name = "identity"; Project = $identityProjectPath },
    @{ Context = "ConfigurationDbContext"; Name = "configuration"; Project = $identityProjectPath },
    @{ Context = "PersistedGrantDbContext"; Name = "operational"; Project = $identityProjectPath },
    @{ Context = "StaffWebDbContext"; Name = "staff-web"; Project = $staffWebProjectPath },
    @{ Context = "CatalogueDbContext"; Name = "catalogue"; Project = $catalogueProjectPath }
)

foreach ($bundle in $bundles) {
    $artifactName = if ($IsWindows) { "$($bundle.Name).exe" } else { $bundle.Name }
    $artifactPath = Join-Path $resolvedOutputDir $artifactName
    $arguments = @(
        "ef", "migrations", "bundle",
        "--project", $bundle.Project,
        "--startup-project", $bundle.Project,
        "--context", $bundle.Context,
        "--configuration", "Release",
        "--output", $artifactPath,
        "--force"
    )

    if (-not [string]::IsNullOrWhiteSpace($RuntimeIdentifier)) {
        $arguments += @("--runtime", $RuntimeIdentifier)
    }

    Write-Host "Building $($bundle.Context) migration bundle: $artifactPath"
    Invoke-DotNet -Arguments $arguments

    if (-not (Test-Path -LiteralPath $artifactPath -PathType Leaf)) {
        throw "Expected migration bundle was not created: $artifactPath"
    }
}

Write-Host "All $($bundles.Count) migration bundles were created in $resolvedOutputDir."
