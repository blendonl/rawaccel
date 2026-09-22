# Raw Accel

Raw Accel is a Windows 10 & Windows 11 x86-64 driver which allows for the acceleration of mouse input in the raw input stream. It started as a replacement for [InterAccel](https://github.com/KovaaK/InterAccel) and has been extended with more acceleration types, charts, and other features.

## Anti-Cheat Friendly

[Releases](https://github.com/a1xd/rawaccel/releases/latest) of the Raw Accel driver are signed and run in system space. Raw Accel only modifies mouse input by a constant set of formulas, and adds a one-second delay when changing settings in order to mitigate its abuse.

## Install

In PowerShell:

```powershell
irm https://raw.githubusercontent.com/blendonl/rawaccel/master/install.ps1 | iex
```

That downloads the latest release into `%LOCALAPPDATA%\Programs\rawaccel` and adds Raw Accel to the Start menu and to Settings > Apps. The .NET 10 and Visual C++ runtimes are installed with winget if they are missing. If the driver is not installed, or differs from the one in the release, Windows asks for administrator rights to install it; restart afterwards. Run it again to upgrade: `settings.json` is kept, and a Raw Accel running from that folder is closed for the upgrade and opened again.

To pin a version or install somewhere else:

```powershell
& ([scriptblock]::Create((irm https://raw.githubusercontent.com/blendonl/rawaccel/master/install.ps1))) -Version 1.7.0 -InstallDir C:\Tools\rawaccel
```

To install a package you built or downloaded from a CI run, pass it with `-Package`:

```powershell
.\install.ps1 -Package .\dist\rawaccel-1.7.0-win64.zip
```

Uninstall it from Settings > Apps > Installed apps, or run `uninstall.ps1` in the install folder. That removes the driver (restart afterwards), the Start menu shortcut and the install folder, including `settings.json`.

## Releases

Every push builds, tests and packages `rawaccel-<version>-win64.zip` on a Windows runner; the zip is attached to the run.

Merging to master releases automatically. `tools/next-version.sh` reads the [conventional commits](https://www.conventionalcommits.org) since the last `v*` tag and bumps that version:

| Commits since the last release | Bump |
| --- | --- |
| `feat!:`, `fix(scope)!:` or a `BREAKING CHANGE:` footer | major |
| `feat:` | minor |
| `fix:` or `perf:` | patch |
| only `docs:`, `ci:`, `chore:`, `build:`, `refactor:`, `test:` … | none, nothing is released |

CI writes the new version into `common/rawaccel-version.h`, builds the package with it, commits `chore(release): <version>` to master, and publishes the `v<version>` release on that commit. A version counts as released only when it has a GitHub release; a `v*` tag without one is moved to the release commit. Don't push `v*` tags by hand. With squash merges the PR title is the commit that counts, so give it a conventional prefix. To pick a version yourself, set `RA_VER_*` in `common/rawaccel-version.h`; a version higher than the computed one wins.

The driver is not rebuilt. The package ships the Microsoft-signed driver, installer and uninstaller from `signed/x64`, and that driver reports 1.7.0, so the version must stay at 1.7.0 or above.

## Getting Help

For an overview of everything Raw Accel has to offer, please see the [guide](doc/Guide.md). For questions, see the [FAQ](doc/FAQ.md) first.

## Development

Development of Raw Accel is ongoing. See "User Interface 2.0" below for work on next release. Bug reports and pull requests are always welcome.  Join [our Discord server](https://discord.gg/7pQh8zH) if you want to stay updated with releases or say hello.

### User Interface 2.0
The next release of Raw Accel is planned to have a new User Interface. Work for this is ongoing at https://github.com/RawAccelOfficial/rawaccel/tree/userinterface. Check out the ReadMe in that branch if interested in contributing.

## External Websites
Raw Accel has moved from its old home at https://github.com/a1xd/rawaccel to https://github.com/RawAccelOfficial/rawaccel. (If you are unsure, you can check that the original link redirects to the new one.)  
The latest version of Raw Accel is always hosted [here on github](https://github.com/RawAccelOfficial/rawaccel). There is no other site or mirror where you can be sure get official versions of Raw Accel.   
Raw Accel is not affiliated with any external websites, such as rawaccel.net or rawaccel.com. If Raw Accel ever were to be affiliated with an external site, it would be mentioned here on github first.

## Credits
simon - Driver & Acceleration Logic  
\_m00se\_ - GUI, Gain features, Acceleration types  
Sidiouth  - Primary tester and sounding board  
TauntyArmordillo - Originator of the ideas behind Synchronous and Natural curve types
Kovaak - Brought us together
