# Building RDR2 Co-op from source

If you already have RDR2 Co-op installed and want to try guest character selection, build just the mod DLL. If you need the Windows loader and bundled runtime too, use the [full Linux build](#build-the-full-game-files-on-linux).

## Get the source

Install Git and the [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) on your build computer. The SDK includes the compiler. You can build the mod DLL on Windows, macOS, or Linux and copy it to the computer where you play.

Clone this repository with a GitHub account that has access, or open a terminal in your existing clone. Run the build commands from the repository root, where `RDR2Coop.ini` and `build.sh` are located.

```sh
git clone https://github.com/Palanti-r/rdr2-coop.git
cd rdr2-coop
dotnet --list-sdks
```

Check that the SDK list includes a `9.0.x` entry.

## Build and install only the mod DLL

Use the target that matches the RDR2 Co-op loader installed in the folder containing `RDR2.exe`.

### Standard loader (.NET 9)

Use this target if the game folder contains `RDR2Coop.asi` and the `RDR2Coop/` folder.

```sh
dotnet build src/RDR2Coop.csproj -c Release -p:TargetFrameworks=net9.0
```

Close the game and back up the installed DLL. Copy `src/bin/Release/net9.0/RDR2Coop.dll` to `<game folder>/RDR2Coop/RDR2Coop.dll`. This replacement assumes the loader, API, and runtime came from the release corresponding to this source snapshot.

### Legacy ScriptHookRDR2DotNet V2 loader (.NET Framework 4.8)

Download [ScriptHookRDR2DotNet V2 2.2.0](https://github.com/Halen84/ScriptHookRDR2DotNet-V2/releases/tag/2.2.0). Extract `ScriptHookRDRNetAPI.dll` from the archive to `lib/ScriptHookRDRNetAPI.dll` in the repository. Create `lib/` if needed. The SDK restores the .NET Framework 4.8 reference assemblies from NuGet.

```sh
dotnet build src/RDR2Coop.csproj -c Release -p:TargetFrameworks=net48
```

Close the game and back up the installed DLL. Copy `src/bin/Release/net48/RDR2Coop.dll` to `<game folder>/scripts/RDR2Coop.dll`. This setup uses ScriptHookRDR2, ScriptHookRDR2DotNet V2 2.2.0, and .NET Framework 4.8 at runtime. Keep only one RDR2 Co-op loader setup active. Set `UpdateCheck=No` under `[Coop]` if you want to keep using the legacy loader, since the upstream update can migrate it to the standard loader.

If restore reports `NU1301` or `NU1900`, check that your build computer can reach `https://api.nuget.org/v3/index.json` and retry. If the compiler cannot find `ScriptHookRDRNetAPI`, check the filename and location under `lib/`.

## Select a guest character

In the game's existing `scripts/RDR2Coop.ini`, edit the `[Coop]` section and keep your server and password settings. For Sadie Adler, use:

```ini
GuestCharacter=CS_MRSADLER
AppearAs=
```

For another character, copy a model name from the commented presets in the repository's [RDR2Coop.ini](RDR2Coop.ini). Keep a single active `GuestCharacter` line. Leaving `AppearAs` empty lets your friend see the model you control.

1. Start RDR2 in Story Mode from a separate free-roam test save.
2. Have your friend host, then join their room as a guest and stand on foot in free roam.
3. Check for the `Guest character: CS_MRSADLER` subtitle. Confirm you can control the character and your friend sees the same model.

The mod attempts the switch once per game launch when you're eligible. With this setting enabled, you stay in your own story world while connected. To go back to your story character, clear `GuestCharacter`, restart the game, and load your original save.

## Build the full game files on Linux

The release build expects the .NET 9 SDK, Mono with the Roslyn `csc` compiler and .NET 4.8 reference assemblies at `/usr/lib/mono/4.8-api`, Zig, Python 3, Bash, `curl`, and `bsdtar`. Use the Zig toolchain specified in [host/build.sh](host/build.sh) or set `ZIG` to the path of a compatible executable. The toolchain cross-compiles the loader for Windows x64. The script fetches the ScriptHookRDR2DotNet build reference if needed, and NuGet supplies the runtime and compatibility packages.

```sh
ZIG=/absolute/path/to/zig ./build.sh
```

The script recreates `dist/` and runs the automated tests. For a fresh standard-loader installation, follow the `INSTALL` section in [README-PLAYERS.txt](README-PLAYERS.txt) using `dist/RDR2Coop.asi`, `dist/RDR2Coop/`, and `dist/scripts/RDR2Coop.ini` as the build outputs. Keep your existing INI when updating. For an existing legacy installation, use `dist/shdn/scripts/RDR2Coop.dll` as the replacement DLL.

### Game files and release artifacts

| File in the download | Source | Built by |
|---|---|---|
| `RDR2Coop.asi` (the loader: hosts .NET 9 inside the game, replaces ScriptHookRDR2DotNet's native part) | `host/RDR2Coop.cpp` | `host/build.sh` (zig, cross-compiled; `host/` says the version) |
| `RDR2Coop/RDR2Coop.Core.dll` (script scheduling, natives, memory, keys, log) | `api/Core` (port of ScriptHookRDR2DotNet V2's core) | `api/Core/RDR2Coop.Core.csproj` |
| `RDR2Coop/RDR2Coop.Api.dll` (the RDR2, RDR2.Native, RDR2.Math, RDR2.UI API) | `api/Api` (port of ScriptHookRDR2DotNet V2's scripting_v3) | `api/Api/RDR2Coop.Api.csproj` |
| `RDR2Coop/RDR2Coop.dll` (the mod, .NET 9) | `src/Net`, `src/Mod` | `src/RDR2Coop.csproj` (target `net9.0`) |
| `RDR2Coop/runtime/` (the .NET 9 runtime, laid out as a dotnet install) | Microsoft's runtime pack (MIT; its licence files are in there) | `host/make-runtime.sh` |
| `shdn/scripts/RDR2Coop.dll` (the mod for ScriptHookRDR2DotNet, .NET Framework 4.8; enRage hosts) | `src/Net`, `src/Mod` | `build.sh` (mono csc) / `src/RDR2Coop.csproj` (target `net48`) |
| `RDR2CoopSetup.exe` (Windows installer) | `tools/WinSetup` | `tools/WinSetup/RDR2CoopSetup.csproj` |
| `setup.py`, `server/relay.py`, `server/setup-server.sh` (installer and server downloads) | as is (Python / shell, not compiled) | - |

`api/` is a port of ScriptHookRDR2DotNet V2 by crosire and Halen84. Its zlib licence and attribution are in `api/LICENSE` and `api/NOTICE` and ship as `RDR2Coop/NOTICE.txt`. Both game installations require ScriptHookRDR2. [ScriptHookRDR2 by Alexander Blade](https://www.dev-c.com/rdr2/scripthookrdr2/)

## Release

`./build.sh` produces the game files. The upstream release workflow also packages the installer, relay, and optional enRage patch. It requires the private enRage build and upstream signing key described in [tools/make-release.sh](tools/make-release.sh).

`tools/make-release.sh` builds the download zips in `release/upload`, and for the
in-game update (src/Mod/Updater.cs) signs the two mod builds with the release key
(`~/.config/rdr2-coop/release-signing.pem`, or `RELEASE_KEY=`) and writes `latest.txt`.
Put `RDR2Coop.dll` + `RDR2Coop.dll.sig` (the ScriptHookRDR2DotNet build; the name
must not change, every older install updates from it), `RDR2Coop.net9.dll` +
`RDR2Coop.net9.dll.sig` (the main build) and `latest.txt` on every GitHub release
with the zips: players' mods find the newest version through
`releases/latest/download/latest.txt`. The one-press update replaces only the mod
DLL; a release that changes the loader, Core, Api or the runtime says "download the
zip / run the installer again" in its CHANGELOG line. Keep a backup of the key:
without it no release can be signed, and players would have to update by hand to a
mod built with a new key.

`SKIP_BUILD=1 tools/make-release.sh` packs whatever `dist/` holds (to test the zips).

## Tests

From the repository root on Linux or macOS, use these commands for the network, script host, relay, and room agent tests. The network tests call `python3` to start a local relay.

```sh
dotnet run --project tests/NetTests.csproj -c Release
dotnet run --project api/CoreTests/CoreTests.csproj -c Release
(cd server && python3 -m unittest test_relay)
(cd server/rooms && python3 -m unittest test_rooms_agent)
```

The full build runs the network tests under Mono and .NET 9, plus the script host and relay tests. Use the in-game steps above to verify the character swap itself.
