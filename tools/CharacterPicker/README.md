# RDR2 Co-op Settings for Windows

Use `RDR2CoopCharacterPicker.exe` to set up a guest or host connection, edit co-op settings, and change your guest character. The app remembers both connection profiles when you switch between them.

## Download and install

Open the [Build Windows settings app workflow](https://github.com/Palanti-r/rdr2-coop/actions/workflows/build-settings.yml), choose a successful run, and download **RDR2-Coop-Settings.zip** under **Artifacts**. The workflow runs on pushes to `master`. You can also start it with **Run workflow**.

Extract the ZIP outside the game folder, open `RDR2-Coop/`, and follow `README-SETTINGS.txt`. The package includes the Windows installer, native loader, bundled .NET runtime, compatibility libraries, standalone server, optional enRage patch, and the settings EXE. Run `RDR2CoopSetup.exe` to install the mod, then open the settings EXE. The installer fetches ScriptHookRDR2 if needed. The settings EXE uses .NET Framework 4.8.

## Build the ZIP locally

Install the .NET 9 SDK and Python 3. Run this from the repository root:

```sh
python3 tools/CharacterPicker/build-package.py
```

On Windows, use `py` in place of `python3` if needed. The builder verifies the upstream Windows ZIP against its pinned SHA-256 and uses the included ScriptHookRDR2DotNet API to compile the legacy DLL. It replaces the two mod DLLs and their INIs, adds the settings app, and verifies every file in `dist/RDR2-Coop-Settings.zip`. All other files from the official package are preserved byte-for-byte. [Matching upstream release](https://github.com/BigCheeseThe3st/rdr2-coop/releases/tag/v0.9.0-beta.8.5)

Keep `RDR2CoopCharacterPicker.exe` and `RDR2CoopCharacterPicker.exe.config` together. Place them next to `RDR2.exe` to find the installed INI automatically. If you open the app from another folder, select the game directory under **Diagnostics** or click **Browse** and select the INI inside your game installation.

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

## Use the emote wheel

Press **F10 twice** in Story Mode and choose **Session → Emote wheel**. Move the mouse or right stick, or press the arrow keys, to highlight a gesture. The bar shows your selection and the available controls. Click or press **Enter** to play it, or press **Esc** to close the wheel.

For a direct shortcut, set **Key bindings → Emote Menu**, click **Save settings**, and restart RDR2. Hold the shortcut, choose a gesture, and release it to play. Emotes sync with your partners in a connected session.

## Troubleshooting

If the co-op menu gives no response, open **Diagnostics** and choose the folder containing `RDR2.exe`. This works even when the INI is missing.

1. Click **Check files and logs**. **File checks** shows missing or empty loader files, ScriptHook files, and bundled runtime libraries. It also detects an older legacy install that can take precedence over the standard loader.
2. Click **Enable debug logging**, then restart RDR2 and load Story Mode. Try the menu shortcut again. The mod records whether it received the key and opened the menu in `scripts/RDR2Coop.log`.
3. Return to the picker, click **Check files and logs**, and open **Recent logs**. You can use **Copy report** to copy the checks and recent log lines for troubleshooting.

- If the app opens with empty settings, use **Browse** and check that the path points to the game's INI. The repository's sample INI is a separate file.
- If a character choice appears saved but the model stays the same, confirm that the updated mod DLL is installed and that you are a guest on foot outside a mission or cutscene.
- If you prefer to select a guest character manually, remove `RDR2Coop.character` and use the `GuestCharacter` setting in the [mod configuration](../../RDR2Coop.ini).
