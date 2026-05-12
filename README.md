# Kennel

A Windows tool that manages [UEVR](https://github.com/praydog/UEVR) launcher wrappers for Steam games.

One Steam library entry, two play modes:

- **SteamVR running** → launches the game with UEVR injected (via [chihuahua](https://github.com/Raicuparta/rai-pal))
- **SteamVR not running** → launches the game normally (flat)

Kennel writes the launcher scripts, registers them in Steam's per-game launch options for you, and keeps the chihuahua injector up to date.

## Status

Early development. Not yet ready for end users.

## Requirements

- Windows 10 / 11
- Steam
- [SteamVR](https://store.steampowered.com/app/250820) for VR mode

No .NET install required — Kennel targets .NET Framework 4.8 which ships with Windows 10 and 11.

## Building from source

```
dotnet build UevrLauncher/UevrLauncher.csproj
```

Output: `UevrLauncher/bin/Debug/net48/Kennel.exe`

## License

TBD.
