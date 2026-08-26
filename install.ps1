<#
.SYNOPSIS
    Forge - Installer
    Usage: irm https://raw.githubusercontent.com/MakaVeli2202/Forg/main/install.ps1 -OutFile "$env:TEMP\forge_install.ps1"; & "$env:TEMP\forge_install.ps1"
#>

$repo = "https://github.com/MakaVeli2202/Forg.git"
$installDir = Join-Path $env:LOCALAPPDATA "Forge"
$startMenuDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
$shortcutPath = Join-Path $startMenuDir "Forge.lnk"
$buildDir = Join-Path $env:TEMP "Forge_Build"

function Find-Exe {
    param([string]$Name, [string[]]$KnownPaths)
    $found = Get-Command $Name -ErrorAction SilentlyContinue
    if ($found) { return $found.Source }
    foreach ($p in $KnownPaths) {
        if (Test-Path $p) { return $p }
    }
    return $null
}

function Show-Status {
    param([string]$Text, [string]$Color = "Gray")
    Write-Host "  " -NoNewline
    Write-Host $Text -ForegroundColor $Color
}

Write-Host ""
Write-Host "  ==========================================" -ForegroundColor DarkRed
Write-Host "  F O R G E" -ForegroundColor Red
Write-Host "  BUILD WINDOWS YOUR WAY" -ForegroundColor DarkGray
Write-Host "  ==========================================" -ForegroundColor DarkRed
Write-Host ""

$dotnetExe = Find-Exe -Name "dotnet" -KnownPaths @(
    "C:\Program Files\dotnet\dotnet.exe",
    "C:\Program Files (x86)\dotnet\dotnet.exe",
    "$env:LOCALAPPDATA\Programs\dotnet\dotnet.exe"
)

$gitExe = Find-Exe -Name "git" -KnownPaths @(
    "C:\Program Files\Git\cmd\git.exe",
    "C:\Program Files (x86)\Git\cmd\git.exe",
    "$env:LOCALAPPDATA\Programs\Git\cmd\git.exe"
)

if (-not $dotnetExe -or -not $gitExe) {
    Show-Status "Forge requires .NET 8 SDK and Git to install." "Red"
    Show-Status ".NET SDK: https://dotnet.microsoft.com/download/dotnet/8.0" "Yellow"
    Show-Status "Git:      https://git-scm.com/download/win" "Yellow"
    Start-Sleep -Seconds 10
    return
}

$gitDir = Split-Path (Split-Path $gitExe)
$env:PATH = "$gitDir\cmd;$gitDir\bin;$gitDir\usr\bin;$gitDir\mingw64\bin;$env:PATH"

try {
    $procs = Get-Process -Name "Forge" -ErrorAction SilentlyContinue
    if ($procs) {
        Show-Status "Stopping running Forge..." "Yellow"
        $procs | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    }

    if (Test-Path $buildDir) {
        Remove-Item -Path $buildDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    Show-Status "Downloading Forge..."
    $ErrorActionPreference = 'Continue'
    & $gitExe clone --depth 1 $repo $buildDir 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Download failed." }

    Show-Status "Building Forge..."
    $publishDir = Join-Path $buildDir "publish"
    & $dotnetExe publish "$buildDir\Forge\Forge.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $publishDir 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Build failed." }
    $ErrorActionPreference = 'Stop'

    Show-Status "Installing..."
    $procs = Get-Process -Name "Forge" -ErrorAction SilentlyContinue
    if ($procs) { $procs | Stop-Process -Force -ErrorAction SilentlyContinue; Start-Sleep -Seconds 2 }
    if (Test-Path $installDir) {
        Remove-Item -Path $installDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
    Copy-Item -Path "$publishDir\*" -Destination $installDir -Recurse -Force

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = Join-Path $installDir "Forge.exe"
    $shortcut.WorkingDirectory = $installDir
    $shortcut.Description = "Forge - Build Windows Your Way"
    $shortcut.Save()

    Remove-Item -Path $buildDir -Recurse -Force -ErrorAction SilentlyContinue

    Show-Status "Installed!" "Green"
    Write-Host ""
    Start-Process -FilePath (Join-Path $installDir "Forge.exe")
}
catch {
    Write-Host ""
    Show-Status "Something went wrong: $($_.Exception.Message)" "Red"
    Start-Sleep -Seconds 10
}
