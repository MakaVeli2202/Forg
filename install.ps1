<#
.SYNOPSIS
    Forge - Installer
    Usage: irm https://raw.githubusercontent.com/MakaVeli2202/Forg/main/install.ps1 -OutFile "$env:TEMP\forge_install.ps1"; & "$env:TEMP\forge_install.ps1"

.DESCRIPTION
    Clones, builds, and installs Forge to %LOCALAPPDATA%\Forge.
    Requires: .NET 8 SDK, Git, Administrator privileges.
#>

$ErrorActionPreference = 'Stop'
$repo = "https://github.com/MakaVeli2202/Forg.git"
$installDir = Join-Path $env:LOCALAPPDATA "Forge"
$startMenuDir = Join-Path $env:ProgramData "Microsoft\Windows\Start Menu\Programs"
$shortcutPath = Join-Path $startMenuDir "Forge.lnk"
$buildDir = Join-Path $env:TEMP "Forge_Build"

function Pause-Exit {
    Write-Host ""
    Write-Host "  Press Enter to exit..." -ForegroundColor DarkGray
    try { [Console]::ReadLine() } catch { Start-Sleep -Seconds 30 }
}

Write-Host ""
Write-Host "  ==========================================" -ForegroundColor DarkRed
Write-Host "  F O R G E" -ForegroundColor Red
Write-Host "  BUILD WINDOWS YOUR WAY" -ForegroundColor DarkGray
Write-Host "  ==========================================" -ForegroundColor DarkRed
Write-Host ""

# --- Check for .NET SDK ---
try { $dotnetVer = dotnet --version 2>&1 } catch { $dotnetVer = $null }
if (-not $dotnetVer -or $LASTEXITCODE -ne 0) {
    Write-Host "  ERROR: .NET SDK 8 not found." -ForegroundColor Red
    Write-Host "  Download: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Yellow
    Pause-Exit
    return
}

# --- Check for Git ---
try { $gitVer = git --version 2>&1 } catch { $gitVer = $null }
if (-not $gitVer -or $LASTEXITCODE -ne 0) {
    Write-Host "  ERROR: Git not found." -ForegroundColor Red
    Write-Host "  Download: https://git-scm.com/download/win" -ForegroundColor Yellow
    Pause-Exit
    return
}

Write-Host "  .NET SDK: $dotnetVer" -ForegroundColor Gray
Write-Host "  Git: $gitVer" -ForegroundColor Gray
Write-Host ""

try {
    $procs = Get-Process -Name "Forge" -ErrorAction SilentlyContinue
    if ($procs) {
        Write-Host "  Stopping running Forge instance..." -ForegroundColor Yellow
        $procs | Stop-Process -Force
        Start-Sleep -Seconds 1
    }

    if (Test-Path $buildDir) {
        Write-Host "  Cleaning old build cache..." -ForegroundColor Gray
        Remove-Item -Path $buildDir -Recurse -Force
    }

    Write-Host "  Cloning Forge..." -ForegroundColor Gray
    git clone --depth 1 $repo $buildDir 2>&1 | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkGray }
    if ($LASTEXITCODE -ne 0) { throw "Git clone failed." }

    Write-Host "  Building Forge (this may take a minute)..." -ForegroundColor Gray
    $publishDir = Join-Path $buildDir "publish"
    dotnet publish "$buildDir\Forge\Forge.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $publishDir 2>&1 | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkGray }
    if ($LASTEXITCODE -ne 0) { throw "Build failed. Make sure .NET 8 SDK is installed." }

    Write-Host "  Installing to $installDir ..." -ForegroundColor Gray
    $procs = Get-Process -Name "Forge" -ErrorAction SilentlyContinue
    if ($procs) { $procs | Stop-Process -Force; Start-Sleep -Seconds 1 }
    if (Test-Path $installDir) {
        Remove-Item -Path $installDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
    Copy-Item -Path "$publishDir\*" -Destination $installDir -Recurse -Force

    Write-Host "  Creating Start Menu shortcut..." -ForegroundColor Gray
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = Join-Path $installDir "Forge.exe"
    $shortcut.WorkingDirectory = $installDir
    $shortcut.Description = "Forge - Build Windows Your Way"
    $shortcut.Save()

    Write-Host "  Cleaning up build files..." -ForegroundColor Gray
    Remove-Item -Path $buildDir -Recurse -Force -ErrorAction SilentlyContinue

    Write-Host ""
    Write-Host "  ==========================================" -ForegroundColor Green
    Write-Host "  INSTALLED SUCCESSFULLY" -ForegroundColor Green
    Write-Host "  ==========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Location:  $installDir" -ForegroundColor White
    Write-Host "  Shortcut:  Forge (Start Menu)" -ForegroundColor White
    Write-Host "  Uninstall: irm https://raw.githubusercontent.com/MakaVeli2202/Forg/main/uninstall.ps1 -OutFile `"`$env:TEMP\forge_uninstall.ps1`"; & `"`$env:TEMP\forge_uninstall.ps1`"" -ForegroundColor White
    Write-Host ""

    $launch = Read-Host "  Launch Forge now? (Y/n)"
    if ($launch -ne 'n' -and $launch -ne 'N') {
        Write-Host "  Launching Forge..." -ForegroundColor Green
        Start-Process -FilePath (Join-Path $installDir "Forge.exe")
    }
}
catch {
    Write-Host ""
    Write-Host "  ERROR: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
}

Pause-Exit
