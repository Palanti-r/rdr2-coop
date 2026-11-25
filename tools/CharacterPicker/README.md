# RDR2 Co-op Settings for Windows

Use `RDR2CoopCharacterPicker.exe` to set up a guest or host connection, edit co-op settings, and change your guest character. The app remembers both connection profiles when you switch between them.

## Build and install

You need an existing RDR2 Co-op installation, this fork's updated mod DLL, and .NET Framework 4.8 on the Windows computer where you play. Build the DLL for your installed loader and follow the copy instructions in the [mod build guide](../../BUILDING.md#build-and-install-only-the-mod-dll).

With the .NET 9 SDK installed, run this from the repository root:

```powershell
dotnet build tools/CharacterPicker/RDR2CoopCharacterPicker.csproj -c Release
```

Keep `RDR2CoopCharacterPicker.exe` and `RDR2CoopCharacterPicker.exe.config` together. Both are in `tools/CharacterPicker/bin/Release/net48/` after the build.

Place the EXE next to `RDR2.exe` so the app can find `scripts/RDR2Coop.ini` automatically. If you open the app from another folder, click **Browse** and select the INI inside your game installation.

## Join or host a room

Open **Connection** and choose a mode.

| Mode | What to enter |
| --- | --- |
| **Guest** | Your player name and your friend's server address, port, and room password. Leave the address blank to join from the in-game co-op menu. |
| **Host** | Your player name, room name, port, and room password. You can enable UPnP to request port forwarding and enable the online listing so friends can find your room. |

Click **Save settings** and restart RDR2. The selected mode becomes active. Both profiles are kept in the INI, including their separate room passwords, so keep your installed INI private.

The other tabs cover general settings, visuals, gameplay, convenience, networking, key bindings, games, and advanced options. Select a row to read its description. These settings apply in either mode and also take effect after a restart.

## Play as Sadie Adler

1. Open **Character** in Guest mode and choose **Sadie Adler** or another preset.
2. Click **Apply character**.
3. Return to RDR2 in Story Mode. Join your friend's room and stand on foot in free roam. If the host's story world is loaded, wait for the mod to return you to your own story world before it swaps your model.

You can choose another guest character during play. The app saves the choice in `RDR2Coop.character` beside the INI. To return to your original character, choose **Story character (reload save)**, click **Apply character**, restart the game, and load your original save.

The host controls their normal story character. Use **Appear As** under **General** to change the model other players see. For a guest, leaving **Appear As** blank lets friends see the character you've selected.

## Troubleshooting

- If the app opens with empty settings, use **Browse** and check that the path points to the game's INI. The repository's sample INI is a separate file.
- If a character choice appears saved but the model stays the same, confirm that the updated mod DLL is installed and that you are a guest on foot outside a mission or cutscene.
- If you prefer to select a guest character manually, remove `RDR2Coop.character` and use the `GuestCharacter` setting in the [mod configuration](../../RDR2Coop.ini).
