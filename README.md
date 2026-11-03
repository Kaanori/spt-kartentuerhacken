# KartentuerHacken (hack keycard door)

Every locked keycard door (Labs, Reserve, Kiba …) gets a "Hacken" (hack) entry in its menu.
After the hacking time the door opens, exactly as with the right card.

Settings: in game F12 → "Kartentuer hacken" (hack duration, distance, success chance).

Rebuild (e.g. after an SPT update):

    export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH
    dotnet build -c Release -p:Installieren=true

## How this mod was made (AI disclosure)

This mod was **programmed by Claude**, an AI assistant made by Anthropic.
**Kaan was the tester**: Kaan has no programming background, played with the mod
on SPT 4.1.6, reported what went wrong, and Claude fixed it. No human programmer
has reviewed the code. It is open source so anyone can check it — if you find a
bug, please open an [issue](https://github.com/Kaanori/spt-kartentuerhacken/issues).

## Requirements

- SPT 4.1.6 (EFT 0.16.9). BepInEx ships with SPT.

## Installation

1. Download `KartentuerHacken-1.0.0.zip` from the [Releases](https://github.com/Kaanori/spt-kartentuerhacken/releases) page.
2. Extract it **into your SPT game folder** (the folder that contains `EscapeFromTarkov.exe`).
   The ZIP already has the right layout; the mod ends up in `BepInEx/plugins/`.
   On Windows, make sure "Extract All" does not add an extra subfolder.
3. Restart the game.

## Uninstall

Delete the mod's files from `BepInEx/plugins/` (settings: `BepInEx/config/` for client mods) and restart the game.

## Building from source

Needs the .NET 10 SDK and an SPT 4.1.6 installation to build against.

```bash
./build.sh             # Linux: build only
./build.sh --install   # build and copy into the game folder
```
```powershell
.\build.ps1            # Windows
.\build.ps1 -Install
```

The game folder is set in `Directory.Build.props` (Windows default `C:/SPT`)
or via the environment variable `SPT_DIR`.

## Support

Bugs and questions: [GitHub issues](https://github.com/Kaanori/spt-kartentuerhacken/issues).

## License

MIT — see [LICENSE](LICENSE).
