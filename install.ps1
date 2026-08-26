<#
.SYNOPSIS
    Forge - One-command installer
    Usage: irm https://raw.githubusercontent.com/MakaVeli2202/Forg/main/install.ps1 | iex

.DESCRIPTION
    Downloads and runs the latest Forge release from GitHub.
    Requires Administrator privileges.
#>

$ErrorActionPreference = 'Stop'

$repo = "MakaVeli2202/Forg"
$apiUrl = "https://api.github.com/repos/$repo/releases/latest"

Write-Host ""
Write-Host "  ==========================================" -ForegroundColor DarkRed
Write-Host "  F O R G E" -ForegroundColor Red
Write-Host "  BUILD WINDOWS YOUR WAY" -ForegroundColor DarkGray
Write-Host "  ==========================================" -ForegroundColor DarkRed
Write-Host ""
Write-Host "  Checking for latest release..." -ForegroundColor Gray

try
{
    $release = Invoke-RestMethod -Uri $apiUrl -UseBasicParsing
}
catch
{
    Write-Host "  Failed to check for updates." -ForegroundColor Red
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor DarkGray
    exit 1
}

$asset = $release.assets | Where-Object {
    $_.name -match '\.exe$'
} | Select-Object -First 1

if (-not $asset)
{
    Write-Host "  No executable found in latest release." -ForegroundColor Red
    Write-Host "  Visit: https://github.com/$repo/releases" -ForegroundColor Gray
    exit 1
}

Write-Host "  Latest version: $($release.tag_name)" -ForegroundColor White
Write-Host "  Downloading: $($asset.name)..." -ForegroundColor Gray

$tempDir = Join-Path $env:TEMP "Forge_Install"
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

$exePath = Join-Path $tempDir $asset.name

try
{
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $exePath -UseBasicParsing
}
catch
{
    Write-Host "  Download failed." -ForegroundColor Red
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor DarkGray
    Write-Host "  Manual download: https://github.com/$repo/releases" -ForegroundColor Gray
    exit 1
}

Write-Host "  Launching Forge..." -ForegroundColor Green
Write-Host ""

Start-Process -FilePath $exePath
