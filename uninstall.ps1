<#
.SYNOPSIS
    Forge - Uninstaller
    Usage: irm https://raw.githubusercontent.com/MakaVeli2202/Forg/main/uninstall.ps1 -OutFile "$env:TEMP\forge_uninstall.ps1"; & "$env:TEMP\forge_uninstall.ps1"

.DESCRIPTION
    Completely removes Forge and all associated files.
#>

$ErrorActionPreference = 'Stop'
$installDir = Join-Path $env:LOCALAPPDATA "Forge"
$startMenuDir = Join-Path $env:ProgramData "Microsoft\Windows\Start Menu\Programs"
$shortcutPath = Join-Path $startMenuDir "Forge.lnk"
$buildCache = Join-Path $env:TEMP "Forge_Build"
$installCache = Join-Path $env:TEMP "Forge_Install"

function Pause-Exit {
    Write-Host ""
    Write-Host "  Press Enter to exit..." -ForegroundColor DarkGray
    try { [Console]::ReadLine() } catch { Start-Sleep -Seconds 30 }
}

Write-Host ""
Write-Host "  ==========================================" -ForegroundColor DarkRed
Write-Host "  F O R G E" -ForegroundColor Red
Write-Host "  UNINSTALLER" -ForegroundColor DarkGray
Write-Host "  ==========================================" -ForegroundColor DarkRed
Write-Host ""

try {
    $exists = Test-Path $installDir
    if (-not $exists) {
        Write-Host "  Forge is not installed." -ForegroundColor Yellow
        Write-Host ""
    }

    $procs = Get-Process -Name "Forge" -ErrorAction SilentlyContinue
    if ($procs) {
        Write-Host "  Stopping Forge..." -ForegroundColor Yellow
        $procs | Stop-Process -Force
        Start-Sleep -Seconds 1
    }

    if ($exists) {
        Write-Host "  Removing $installDir ..." -ForegroundColor Gray
        Remove-Item -Path $installDir -Recurse -Force
        Write-Host "  Removed install directory." -ForegroundColor Green
    }

    if (Test-Path $shortcutPath) {
        Write-Host "  Removing Start Menu shortcut..." -ForegroundColor Gray
        Remove-Item -Path $shortcutPath -Force
        Write-Host "  Removed shortcut." -ForegroundColor Green
    }

    if (Test-Path $buildCache) {
        Write-Host "  Removing build cache ($buildCache)..." -ForegroundColor Gray
        Remove-Item -Path $buildCache -Recurse -Force
        Write-Host "  Removed build cache." -ForegroundColor Green
    }

    if (Test-Path $installCache) {
        Write-Host "  Removing download cache ($installCache)..." -ForegroundColor Gray
        Remove-Item -Path $installCache -Recurse -Force
        Write-Host "  Removed download cache." -ForegroundColor Green
    }

    $tempForge = Join-Path $env:TEMP "Forge_*"
    $tempFiles = Get-Item -Path $tempForge -ErrorAction SilentlyContinue
    foreach ($f in $tempFiles) {
        if ($f.Name -ne "Forge_Build") {
            Write-Host "  Removing $($f.FullName)..." -ForegroundColor Gray
            Remove-Item -Path $f.FullName -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    Write-Host ""
    Write-Host "  ==========================================" -ForegroundColor Green
    Write-Host "  UNINSTALLED SUCCESSFULLY" -ForegroundColor Green
    Write-Host "  ==========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "  All files, shortcuts, and caches removed." -ForegroundColor White
    Write-Host ""
}
catch {
    Write-Host ""
    Write-Host "  ERROR: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "  You may need to manually delete: $installDir" -ForegroundColor Yellow
    Write-Host ""
}

Pause-Exit
