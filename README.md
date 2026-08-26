# Forge

**Build Windows Your Way**

A powerful Windows PC setup and optimization toolkit. Install apps, apply system tweaks, manage drivers, and customize Windows — all in one place.

## Quick Install

Run this single command in PowerShell (as Administrator):

```powershell
irm https://raw.githubusercontent.com/MakaVeli2202/Forg/main/install.ps1 | iex
```

This will download and launch the latest version of Forge automatically.

## Features

- **Bulk App Install** — Install, update, and remove applications in one click
- **Privacy & Performance** — Debloat Windows, disable telemetry, boost speed
- **System Tweaks** — 50+ tweaks organized by category with one-click apply/undo
- **Driver Management** — Scan and fix drivers with Device Manager integration
- **Windows Updates** — Manage system updates from one place
- **Custom Windows ISO** — Create your perfect Windows installation media
- **Ultimate Performance** — Enable the hidden power plan for maximum gaming performance

## Recommended Gaming Tweaks

For the best gaming experience, apply these tweaks in the Tweaks section:

| Tweak | Why |
|-------|-----|
| Game Mode | Prioritizes system resources for games |
| Ultimate Performance Profile | Unlocks maximum CPU/GPU power |
| Hibernation - Disable | Frees RAM and disk space |
| Background Apps - Disable | Stops Store apps from consuming resources |
| Delivery Optimization - Disable | Stops Windows from using your upload bandwidth |
| Services - Set to Manual | Reduces background svchost processes |
| Visual Effects - Best Performance | Snappier UI, less GPU overhead |

## Requirements

- Windows 10/11
- Administrator privileges
- PowerShell 5.1+

## Building from Source

```bash
git clone https://github.com/MakaVeli2202/Forg.git
cd Forg
dotnet build
```

## License

MIT
