Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Navigate to root
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot

try {
    $project   = 'src\DeDupe\DeDupe.csproj'
    $pkgDir    = 'src\DeDupe\AppPackages'
    $manifest  = 'src\DeDupe\Package.appxmanifest'
    $platforms = @('x64', 'x86', 'ARM64')

    # --- Check dotnet ---
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet CLI not found on PATH.'
    }

    # --- Locate MakeAppx.exe ---
    $makeAppx = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin\10.*\x64\MakeAppx.exe' -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending |
        Select-Object -First 1

    if (-not $makeAppx) {
        throw 'MakeAppx.exe not found.'
    }
    Write-Host "MakeAppx: $($makeAppx.FullName)"

    # --- Parse version from Package.appxmanifest ---
    [xml]$xml = Get-Content $manifest
    $version = $xml.Package.Identity.Version

    if ([string]::IsNullOrWhiteSpace($version)) {
        throw "Could not parse Version from $manifest"
    }

    Write-Host ''
    Write-Host '============================================================'
    Write-Host "  DeDupe - Microsoft Store Build"
    Write-Host "  Version:   $version"
    Write-Host "  Platforms: $($platforms -join ', ')"
    Write-Host '============================================================'
    Write-Host ''

    # --- Clean previous output ---
    if (Test-Path $pkgDir) {
        Write-Host 'Cleaning previous packages...'
        Remove-Item $pkgDir -Recurse -Force
    }

    # --- Build each platform ---
    foreach ($platform in $platforms) {
        Write-Host ''
        Write-Host "[BUILD] $platform ..."

        dotnet build $project -c Release -p:Platform=$platform `
            -p:AppxBundle=Never `
            -p:UapAppxPackageBuildMode=StoreUpload `
            -p:AppxPackageDir=".\AppPackages\" `
            -p:GenerateAppxPackageOnBuild=true `
            -p:AppxPackageSigningEnabled=false `
            -p:GenerateTemporaryStoreCertificate=false `
            -p:AppxSymbolPackageEnabled=false

        if ($LASTEXITCODE -ne 0) {
            throw "Build failed for $platform"
        }
        Write-Host "[OK] $platform build succeeded."
    }

    Write-Host ''
    Write-Host '[BUNDLE] Creating .msixbundle ...'

    $bundleDir = Join-Path $pkgDir 'Bundle'
    $bundleOut = Join-Path $pkgDir "DeDupe_$version.msixbundle"

    New-Item -ItemType Directory -Path $bundleDir -Force | Out-Null

    # --- Collect MSIX files ---
    $msixFiles = Get-ChildItem -Path $pkgDir -Filter '*.msix' -Recurse |
        Where-Object { $_.DirectoryName -ne (Resolve-Path $bundleDir).Path } |
        Where-Object { $_.Name -like 'DeDupe_*' }

    if ($msixFiles.Count -eq 0) {
        throw 'No .msix files found after build.'
    }

    foreach ($file in $msixFiles) {
        Write-Host "  Copying $($file.Name)"
        Copy-Item $file.FullName -Destination $bundleDir
    }

    # --- Create bundle ---
    Write-Host "  Using: $($makeAppx.FullName)"
    & $makeAppx.FullName bundle /d $bundleDir /p $bundleOut /o

    if ($LASTEXITCODE -ne 0) {
        throw 'Bundle creation failed.'
    }

    # --- Clean up temp bundle dir ---
    Remove-Item $bundleDir -Recurse -Force

    Write-Host ''
    Write-Host '============================================================'
    Write-Host '  Store package ready:'
    Write-Host "    $bundleOut"
    Write-Host '============================================================'
}
catch {
    Write-Host ''
    Write-Host "[ERROR] $_" -ForegroundColor Red
    exit 1
}
finally {
    Pop-Location
}