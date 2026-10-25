# The engine

NeuralSwitch doesn't render anything itself. The work is done inside the game by
[OptiScaler-DLSS5](https://github.com/Skynizz/optiscaler-dlss5), a fork of
[OptiScaler](https://github.com/optiscaler/OptiScaler) that runs DLSS 5 Neural Rendering on the frames a
game's own DLSS produces. This folder builds that engine with one patch added: a named pipe the
NeuralSwitch app talks to while the game runs.

| File | What it is |
|---|---|
| `UPSTREAM_COMMIT` | The OptiScaler-DLSS5 commit the patch is made against. Builds always use exactly this commit. |
| `patches/0001-neuralswitch-remote-control.patch` | The remote-control pipe: `OptiScaler/dlssnr/DlssNr_Remote.{h,cpp}`, a call from the menu's per-frame shortcut handling, and the `[DlssNr] RemoteControl` setting. |
| `build-engine.ps1` | Clones the pinned commit with its submodules into `upstream/`, applies the patches, builds, and stages the result in `dist/`. |

## Building

You need git and Visual Studio 2022 or 2026 with the **Desktop development with C++** workload. The
free Build Tools are enough:

```bash
winget install --id Microsoft.VisualStudio.2022.BuildTools --override "--passive --wait --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended"
```

Then:

```bash
powershell -ExecutionPolicy Bypass -File engine\build-engine.ps1
```

The first run downloads the engine and its submodules (several hundred MB) and takes a few minutes.
`dist/` then holds `OptiScaler.dll`, the forwarder `nvngx.dll_dlssnr.dll`, the shipped `OptiScaler.ini`
and `engine.json` (which commit and patches went in). The script refuses to finish if the built DLL
doesn't contain the remote-control code.

## Changing the patch

Edit the files in `upstream/`, then regenerate the patch from there:

```bash
cd engine/upstream
git add -N OptiScaler/dlssnr/DlssNr_Remote.cpp OptiScaler/dlssnr/DlssNr_Remote.h
git -c core.autocrlf=false diff --binary > ../patches/0001-neuralswitch-remote-control.patch
```

To move to a newer OptiScaler-DLSS5, put the new commit in `UPSTREAM_COMMIT`, run the build, and fix
whatever no longer applies.

## Testing it without a game

```bash
powershell -ExecutionPolicy Bypass -File tools\engine-smoke-test\run-smoke-test.ps1
```

This compiles a small Direct3D 12 program, puts the built engine beside it as `winmm.dll` (the way the
installer puts it into a game), and drives the pipe with the CLI: on, style, intensity, toggle, a
refused value, and a check that it still answers afterwards.

## What is not here

NVIDIA's model, `nvngx_dlssnr.dll`. It belongs to NVIDIA, isn't redistributable, and is never
downloaded by anything in this repository. The app finds it where NVIDIA puts it, or you point it at a
copy.
