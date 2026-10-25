# NeuralSwitch

Turn DLSS 5 Neural Rendering on and off, and switch its style, **while you play**. It works in games
that already support DLSS but have no DLSS 5 of their own.

![NeuralSwitch connected to a running game](docs/screenshot.png)

NeuralSwitch has two parts:

* **An engine inside the game.** This is [OptiScaler-DLSS5](https://github.com/Skynizz/optiscaler-dlss5),
  which runs NVIDIA's Neural Rendering model on the frames the game's own DLSS produces. NeuralSwitch
  builds it with one small patch: a local pipe the app can talk to.
* **An app outside the game.** It finds your DLSS games, puts the engine into a game's folder (and
  takes it out again cleanly), and switches Neural Rendering live: **Default**, **Natural** or
  **Cinematic**, plus intensity. Hotkeys work while the game has focus.

> **Unofficial.** NeuralSwitch isn't made by or affiliated with NVIDIA. It drives an undocumented
> driver feature, which a driver update can change or break. DLSS is a trademark of NVIDIA
> Corporation.

## What you need

* **An NVIDIA RTX GPU.** NVIDIA's model runs natively on RTX 50 series cards.
* **NVIDIA driver 616.56 or newer.**
* **NVIDIA's model, `nvngx_dlssnr.dll`.** It isn't included and NeuralSwitch never downloads it. It
  comes with NVIDIA drivers and games that ship DLSS 5. The app looks in the driver store, NVIDIA's
  model folder and your game folders, and you can point it at a copy with **Locate model…**.
  NeuralSwitch checks that the file is **signed by NVIDIA**. Modified copies circulate online, and
  the app warns you before using one.
* **A game that supports DLSS**, with DLSS turned on in its graphics settings.
* **Single-player only.** Games with EasyAntiCheat, BattlEye or similar are detected and refused.
  Loading any mod into them can get an account banned.

## Using it

1. Download the latest release and unzip it anywhere. Or build it yourself (see **Building** below).
2. Run **NeuralSwitch.exe**. It lists the DLSS games in your library folders (`C:\Games` by default;
   use **+ Add folder** for others).
3. Click **Prepare** on a game. This adds four files beside the game's executable and changes nothing
   else. The game starts exactly as before, with Neural Rendering off.
4. Start the game and turn DLSS on in its graphics settings. The game shows up under **Now playing**.
5. Flip the switch or pick a style. In game:

   | Hotkey | Does |
   |---|---|
   | **Ctrl+Alt+N** | Neural Rendering on/off |
   | **Ctrl+Alt+B** | Next style (Default → Natural → Cinematic) |

   Both keys can be changed in `%APPDATA%\NeuralSwitch\settings.json` (`ToggleHotkey`, `StyleHotkey`).

**Remove** takes the engine out again. The folder ends up exactly as it was, because every installed
file is recorded in `neuralswitch.installed.json` beside the game.

### From the command line

`neuralswitch-cli.exe` does the same as the app:

```
neuralswitch-cli games                    DLSS games in your library folders
neuralswitch-cli install "C:\Games\Marvel's Spider-Man 2"
neuralswitch-cli status
neuralswitch-cli on
neuralswitch-cli style natural
neuralswitch-cli intensity 0.8
neuralswitch-cli uninstall "C:\Games\Marvel's Spider-Man 2"
```

## How it works

```
 NeuralSwitch.exe ──── \\.\pipe\neuralswitch-<pid> ────┐
 (or the CLI)          "set enabled=true style=natural" │
                                                        ▼
                                  ┌─ game process ──────────────────────────────┐
                                  │  winmm.dll = OptiScaler-DLSS5 + this patch  │
                                  │    queued request → applied on the next     │
                                  │    frame, on the render thread              │
                                  │  game's DLSS → nvngx_dlssnr.dll (NVIDIA)    │
                                  └─────────────────────────────────────────────┘
```

The engine loads as a proxy DLL beside the game's executable (`winmm.dll`, or the next free name). On
the game's first frame it opens a pipe that only this PC and this user can reach. The app finds running
games by their pipes. Changes are applied by the game's own render thread, exactly like the engine's
in-game hotkey, so a click in the app is the same as a key press in the game. The protocol is in
[docs/PROTOCOL.md](docs/PROTOCOL.md).

## Building

```bash
git clone https://github.com/Razni-Ahamed/NeuralSwitch.git
cd NeuralSwitch
dotnet build NeuralSwitch.sln
dotnet test NeuralSwitch.sln
powershell -ExecutionPolicy Bypass -File engine\build-engine.ps1
powershell -ExecutionPolicy Bypass -File scripts\package.ps1
```

* **The app** needs the .NET 8 SDK.
* **The engine** needs git and Visual Studio 2022 or 2026 with the C++ workload. See
  [engine/README.md](engine/README.md).
* **The release zip** lands in `artifacts/`.

GitHub Actions builds and tests every push. A `v*` tag also builds the engine and publishes the zip as
a release.

| Folder | What's in it |
|---|---|
| `src/NeuralSwitch` | The app (WPF) |
| `src/NeuralSwitch.Core` | Game scanning, installing, finding the model, the pipe client, a simulated game |
| `src/NeuralSwitch.Cli` | `neuralswitch-cli` |
| `tests/NeuralSwitch.Tests` | Unit tests, including the pipe protocol against the simulated game |
| `engine/` | The pinned engine commit, the patch and its build script |
| `tools/engine-smoke-test` | Loads the real built engine into a tiny Direct3D 12 program and drives it |

## Credits

* [OptiScaler](https://github.com/optiscaler/OptiScaler): cdozdil and the OptiScaler team.
* [OptiScaler-DLSS5](https://github.com/Skynizz/optiscaler-dlss5) by Skynizz, built on Dagherbou's
  OptiScaler_DLSSNR. It does the actual Neural Rendering.
* The colour composition in the engine comes from [RenoDX](https://github.com/clshortfuse/renodx) by
  clshortfuse.

## License

GPL-3.0, the same as OptiScaler. See [LICENSE](LICENSE).
