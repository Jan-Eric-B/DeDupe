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

    # --- Publisher ---
    $publisher        = 'CN=4EB8B421-4EE9-470A-8A8D-54E0C4F44E0C'
    $certFriendlyName = 'DeDupe SideLoad'

    # --- Check dotnet ---
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet CLI not found on PATH.'
    }

    # --- Locate SignTool.exe ---
    $signTool = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin\10.*\x64\signtool.exe' -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending |
        Select-Object -First 1

    if (-not $signTool) {
        throw 'SignTool.exe not found.'
    }
    Write-Host "SignTool: $($signTool.FullName)"

    # --- Parse version from Package.appxmanifest ---
    [xml]$xml = Get-Content $manifest
    $version = $xml.Package.Identity.Version

    if ([string]::IsNullOrWhiteSpace($version)) {
        throw "Could not parse Version from $manifest"
    }

    Write-Host ''
    Write-Host '============================================================'
    Write-Host "  DeDupe - GitHub Release Build (Signed)"
    Write-Host "  Version:   $version"
    Write-Host "  Platforms: $($platforms -join ', ')"
    Write-Host '============================================================'
    Write-Host ''

    # --- Certificate ---
    Write-Host '[CERT] Looking for existing signing certificate...'

    $cert = Get-ChildItem Cert:\CurrentUser\My |
        Where-Object {
            $_.Subject -eq $publisher -and
            $_.FriendlyName -eq $certFriendlyName -and
            $_.NotAfter -gt (Get-Date)
        } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1

    if ($cert) {
        Write-Host "  Found existing cert (thumbprint: $($cert.Thumbprint), expires: $($cert.NotAfter.ToString('yyyy-MM-dd')))"
    }
    else {
        Write-Host '  No valid certificate found. Creating a new self-signed certificate...'

        $cert = New-SelfSignedCertificate `
            -Type Custom `
            -Subject $publisher `
            -FriendlyName $certFriendlyName `
            -KeyUsage DigitalSignature `
            -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}') `
            -CertStoreLocation 'Cert:\CurrentUser\My' `
            -NotAfter (Get-Date).AddYears(5)

        Write-Host "  Created new cert (thumbprint: $($cert.Thumbprint), expires: $($cert.NotAfter.ToString('yyyy-MM-dd')))"
    }

    $thumbprint = $cert.Thumbprint

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
            -p:UapAppxPackageBuildMode=SideloadOnly `
            -p:AppxPackageDir=".\AppPackages\" `
            -p:GenerateAppxPackageOnBuild=true `
            -p:AppxPackageSigningEnabled=false `
            -p:AppxSymbolPackageEnabled=false

        if ($LASTEXITCODE -ne 0) {
            throw "Build failed for $platform"
        }
        Write-Host "[OK] $platform build succeeded."
    }

    Write-Host ''
    Write-Host '[COLLECT] Gathering MSIX files...'

    $ghDir = Join-Path $pkgDir 'GitHub'
    New-Item -ItemType Directory -Path $ghDir -Force | Out-Null

    # --- Collect MSIX files ---
    $allMsixFiles = Get-ChildItem -Path $pkgDir -Filter '*.msix' -Recurse |
        Where-Object { $_.DirectoryName -ne (Resolve-Path $ghDir).Path }

    if ($allMsixFiles.Count -eq 0) {
        throw 'No .msix files found after build.'
    }

    # --- Separate packages ---
    $dedupePackages     = $allMsixFiles | Where-Object { $_.Name -like 'DeDupe_*' }
    $dependencyPackages = $allMsixFiles | Where-Object { $_.Name -notlike 'DeDupe_*' }

    # --- Copy and sign packages ---
    foreach ($file in $dedupePackages) {
        $dest = Join-Path $ghDir $file.Name
        Copy-Item $file.FullName -Destination $dest

        Write-Host "[SIGN] $($file.Name) ..."
        & $signTool.FullName sign /fd SHA256 /sha1 $thumbprint /td SHA256 $dest

        if ($LASTEXITCODE -ne 0) {
            throw "Signing failed for $($file.Name)"
        }
    }

    # --- Copy dependency packages ---
    foreach ($file in ($dependencyPackages | Sort-Object Name -Unique)) {
        $dest = Join-Path $ghDir $file.Name
        if (-not (Test-Path $dest)) {
            Write-Host "  [DEP] $($file.Name)"
            Copy-Item $file.FullName -Destination $dest
        }
    }

    # --- Export public certificate ---
    $cerPath = Join-Path $ghDir 'DeDupe.cer'
    Export-Certificate -Cert $cert -FilePath $cerPath | Out-Null
    Write-Host '[CERT] Exported public certificate to DeDupe.cer'

    Write-Host ''
    Write-Host '============================================================'
    Write-Host '  GitHub release assets ready in:'
    Write-Host "    $ghDir\"
    Write-Host ''
    Write-Host '  Files:'
    Get-ChildItem "$ghDir\*" | ForEach-Object { Write-Host "    $($_.Name)" }
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