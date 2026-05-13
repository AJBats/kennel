# Kennel

A Windows tool that gives any Unreal Engine game on Steam **one Play button, two modes**:

- **SteamVR running** → game launches with [UEVR](https://github.com/praydog/UEVR) injected via [chihuahua](https://github.com/keton/chihuahua)
- **SteamVR not running** → game launches normally (flat)

Steam doesn't know the difference. The same Library entry just does the right thing. No re-pasting launch options, no second shortcut, no leaving a game in "VR mode" when you wanted flat.

## Install

1. Download `Kennel-v1.0.0.zip` from the [Releases](https://github.com/AJBats/kennel/releases) page.
2. Extract anywhere you like (e.g. a folder on your desktop).
3. Double-click `Kennel.exe`.

No installer, no admin privileges, no .NET runtime to install - `.NET Framework 4.8` ships with Windows 10 and 11.

## First run

Kennel asks once where to put its data (Documents folder is the default; `%LOCALAPPDATA%` is the other choice). Then the main window opens with a red banner telling you chihuahua isn't installed yet - click **Install chihuahua** and it downloads everything it needs from GitHub (about 14 MB, SHA-256 verified for the UEVR portion).

## Adding a game

> **Quit Steam first.** Kennel writes to Steam's per-game launch options config, and Steam clobbers external writes on shutdown. Kennel detects Steam running and disables the Add button with a warning until you quit.

1. Click **Add game…**
2. Pick a game from your installed Steam library (filterable by name).
3. The game's main `.exe` is auto-detected - verify or **Browse…** for an override.
4. Adjust the VR start delay.
5. Optionally pick **Nightly** instead of **Release** for bleeding-edge UEVR builds.
6. Click OK. Kennel writes the launcher scripts and registers them with Steam.

Re-launch Steam, hit **Play** on the game. Flat mode if SteamVR isn't running; VR mode if it is.

## Manual injection mode

Some games won't work well with chihuahua's auto inject delay. For those, tick **Manual injection** in the Add/Edit dialog:

- Steam Play launches the game flat (no auto-inject).
- If you have UAC elevation popup enabled, you'll see it here as the launcher starts `UEVRInjector.exe`. (this step is skipped if `UEVRInjector.exe` is already running.)
- After you accept, the UEVR frontend opens alongside your running game.
- Pick the game's process in the frontend and inject manually.

**Known issue:** Manual mode only supports **Release** UEVR at this time.

## Keeping things updated

- **chihuahua updates:** click **Check for chihuahua update** in the main window. Kennel only downloads if a newer release exists. Your previous install is kept as `chihuahua-release.bak\` for rollback.
- **UEVR runtime DLLs:** chihuahua manages these itself. The first time you launch a Nightly-mode wrapper, chihuahua pulls down the latest UEVR nightly build; subsequent launches just verify it's current.

## Where data lives

If you picked Documents:

```
Documents\Kennel\
├── config.json          ← chihuahua tag + per-wrapper basename↔appid map
├── wrappers\            ← .bat + .vbs pairs per game
├── chihuahua-release\   ← chihuahua + UEVR 1.05 + UEVRInjector frontend
└── chihuahua-nightly\   ← chihuahua (UEVR DLLs bootstrap on first launch)
```

Plus a tiny pointer at `%LOCALAPPDATA%\Kennel\install.json` so Kennel can find that data folder on subsequent runs.

To uninstall: close Kennel, delete both folders. Steam's launch options stay (point at a now-missing `.vbs`); remove them manually in each game's Properties or just leave them - Steam falls back to launching the game directly when the wrapper is gone.

## Building from source

Requires the .NET SDK (10.x works; 8.x should too) on Windows.

```
dotnet build UevrLauncher\UevrLauncher.csproj
```

Output: `UevrLauncher\bin\Debug\net48\Kennel.exe`.

## Credits

Kennel is the launcher orchestration. The actual VR magic is praydog's UEVR; the injection is keton's chihuahua. Without either of those projects, Kennel wouldn't have anything to wrap.

## License

[The Unlicense](https://unlicense.org/) — public domain. Use it, fork it, ship it, sell it. See `UNLICENSE`.
