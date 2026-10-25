# Notice

```
NeuralSwitch - live control of DLSS 5 Neural Rendering in DLSS games
Copyright (C) 2026 Razni-Ahamed and NeuralSwitch contributors

This program is free software: you can redistribute it and/or modify it under
the terms of the GNU General Public License as published by the Free Software
Foundation, either version 3 of the License, or (at your option) any later
version.

This program is distributed in the hope that it will be useful, but WITHOUT
ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with
this program (the LICENSE file). If not, see <https://www.gnu.org/licenses/>.
```

## What this covers

**NeuralSwitch's own work** is covered by the copyright above:

- the app (`src/NeuralSwitch`), the core library (`src/NeuralSwitch.Core`) and the command-line tool
  (`src/NeuralSwitch.Cli`);
- the tests, tools and scripts;
- the documentation;
- the engine patch in `engine/patches/`.

**The engine** that NeuralSwitch builds and ships (`OptiScaler.dll` and `nvngx.dll_dlssnr.dll` in a
release's `engine` folder) is a modified build of other people's work:

- [OptiScaler-DLSS5](https://github.com/Skynizz/optiscaler-dlss5) by Skynizz, built on Dagherbou's
  OptiScaler_DLSSNR;
- [OptiScaler](https://github.com/optiscaler/OptiScaler) by cdozdil and the OptiScaler team.

Both are licensed under the GNU General Public License, version 3. Their copyright stays with their
authors. NeuralSwitch's patch is offered under the same license, and the modified engine is
distributed under its terms. The engine also contains third-party components under their own licenses;
see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## Corresponding source for the engine binary

The engine in every NeuralSwitch release is built from:

1. OptiScaler-DLSS5 at the commit named in [`engine/UPSTREAM_COMMIT`](engine/UPSTREAM_COMMIT),
   including its git submodules: <https://github.com/Skynizz/optiscaler-dlss5>;
2. the patches in [`engine/patches/`](engine/patches/);
3. built with [`engine/build-engine.ps1`](engine/build-engine.ps1), which fetches exactly that commit,
   applies the patches and runs the build.

The NeuralSwitch source for a release is the repository at that release's git tag. Each release's
`engine/engine.json` records the commit and the patches it was built from.

## Not included, and not covered

**NVIDIA's DLSS 5 Neural Rendering model**, `nvngx_dlssnr.dll`, is NVIDIA's property. It is not part of
this repository or of any NeuralSwitch release, and nothing here downloads it. Whoever uses it is
bound by NVIDIA's terms for it.

## Trademarks

NVIDIA, GeForce, RTX and DLSS are trademarks or registered trademarks of NVIDIA Corporation. AMD,
FidelityFX and Anti-Lag are trademarks of Advanced Micro Devices, Inc. Intel and XeSS are trademarks of
Intel Corporation. Microsoft, Windows and DirectX are trademarks of Microsoft Corporation. All other
names are trademarks of their respective owners.

Using these names describes compatibility only. **NeuralSwitch is not affiliated with, endorsed by or
sponsored by any of these companies**, or by the authors of OptiScaler or OptiScaler-DLSS5.
