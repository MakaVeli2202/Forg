<#
.SYNOPSIS
    Forge - Uninstaller
    Usage: irm https://raw.githubusercontent.com/MakaVeli2202/Forg/main/uninstall.ps1 -OutFile "$env:TEMP\forge_uninstall.ps1"; & "$env:TEMP\forge_uninstall.ps1"
#>

$installDir = Join-Path $env:LOCALAPPDATA "Forge"
$startMenuDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
$shortcutPath = Join-Path $startMenuDir "Forge.lnk"
$buildCache = Join-Path $env:TEMP "Forge_Build"
$installCache = Join-Path $env:TEMP "Forge_Install"

function Show-Status {
    param([string]$Text, [string]$Color = "Gray")
    Write-Host "  " -NoNewline
    Write-Host $Text -ForegroundColor $Color
}

Write-Host ""
Write-Host "  ==========================================" -ForegroundColor DarkRed
Write-Host "  F O R G E" -ForegroundColor Red
Write-Host "  UNINSTALLER" -ForegroundColor DarkGray
Write-Host "  ==========================================" -ForegroundColor DarkRed
Write-Host ""

try {
    $procs = Get-Process -Name "Forge" -ErrorAction SilentlyContinue
    if ($procs) {
        Show-Status "Stopping Forge..." "Yellow"
        $procs | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    }

    if (Test-Path $installDir) {
        Show-Status "Removing Forge..."
        Remove-Item -Path $installDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    if (Test-Path $shortcutPath) {
        Remove-Item -Path $shortcutPath -Force -ErrorAction SilentlyContinue
    }

    if (Test-Path $buildCache) {
        Remove-Item -Path $buildCache -Recurse -Force -ErrorAction SilentlyContinue
    }

    if (Test-Path $installCache) {
        Remove-Item -Path $installCache -Recurse -Force -ErrorAction SilentlyContinue
    }

    $tempFiles = Get-Item -Path (Join-Path $env:TEMP "Forge_*") -ErrorAction SilentlyContinue
    foreach ($f in $tempFiles) {
        Remove-Item -Path $f.FullName -Recurse -Force -ErrorAction SilentlyContinue
    }

    Show-Status "Forge has been uninstalled." "Green"
}
catch {
    Show-Status "Something went wrong: $($_.Exception.Message)" "Red"
    Start-Sleep -Seconds 5
}
