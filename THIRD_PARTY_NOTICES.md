# Third-party notices

NeuralSwitch includes or builds on the components below. Each one remains under its own license, and
each license's full text is in the project it links to. The engine section reflects the source tree of
OptiScaler-DLSS5 at the commit in [`engine/UPSTREAM_COMMIT`](engine/UPSTREAM_COMMIT).

## The engine (`engine/` in a release)

| Component | Used for | License |
|---|---|---|
| [OptiScaler](https://github.com/optiscaler/OptiScaler), cdozdil and the OptiScaler team | The engine itself: hooks, upscaler bridging, the in-game menu | GPL-3.0 |
| [OptiScaler-DLSS5](https://github.com/Skynizz/optiscaler-dlss5), Skynizz (on Dagherbou's OptiScaler_DLSSNR) | DLSS 5 Neural Rendering on the game's frames | GPL-3.0 |
| [RenoDX](https://github.com/clshortfuse/renodx) DLSS 5 add-on, clshortfuse | The colour composition the engine reimplements | MIT (attribution in `engine/Licenses/RenoDX_ATTRIBUTION.txt`) |
| [AMD FidelityFX SDK](https://github.com/GPUOpen-LibrariesAndSDKs/FidelityFX-SDK) and FSR 2/3 headers | FSR upscaling and frame generation | MIT (`engine/Licenses/FidelityFX_v2_LICENSE.md`) |
| AMD Anti-Lag 2 SDK | Latency reduction on AMD GPUs | MIT |
| [Intel XeSS SDK](https://github.com/intel/xess) | XeSS upscaling | Intel Simplified Software License (`engine/Licenses/XeSS_LICENSE.txt`) |
| [DirectX Agility SDK](https://devblogs.microsoft.com/directx/directx12agility/) and D3DX headers | Direct3D | Microsoft license terms (`engine/Licenses/DirectX_LICENSE.txt`) |
| [Microsoft Detours](https://github.com/microsoft/Detours) | Function hooking | MIT |
| [NVIDIA NVAPI](https://github.com/NVIDIA/nvapi) | GPU queries and Reflex | MIT |
| [NVIDIA Streamline](https://github.com/NVIDIAGameWorks/Streamline) headers | Talking to games that use Streamline | MIT |
| NVIDIA NGX / DLSS SDK headers | Talking to the game's DLSS | NVIDIA proprietary license (`LicenseRef-NvidiaProprietary`), as shipped in OptiScaler |
| [Vulkan Headers](https://github.com/KhronosGroup/Vulkan-Headers), The Khronos Group | Vulkan | Apache-2.0 or MIT |
| [Dear ImGui](https://github.com/ocornut/imgui), Omar Cornut | The in-game menu | MIT |
| [FreeType](https://gitlab.freedesktop.org/freetype/freetype) | Font rendering in the menu | FreeType License (FTL) |
| [spdlog](https://github.com/gabime/spdlog), Gabi Melman | Logging | MIT |
| [nlohmann/json](https://github.com/nlohmann/json), Niels Lohmann | JSON | MIT |
| [simpleini](https://github.com/brofield/simpleini), Brodie Thiesfield | Reading `OptiScaler.ini` | MIT |
| [unordered_dense](https://github.com/martinus/unordered_dense), Martin Leitner-Ankerl | Hash maps | MIT |
| [magic_enum](https://github.com/Neargye/magic_enum), Daniil Goncharov | Enum names | MIT |
| [flag-set-cpp](https://github.com/mrts/flag-set-cpp) | Flag sets | MIT |
| [LatencyFleX](https://github.com/ishitatsuyuki/LatencyFleX), Tatsuyuki Ishi | Latency reduction | Apache-2.0 |
| SHA-1 implementation (`sha1.hpp`) | Hashing | Public domain |

The release's `engine` folder includes `LICENSE-OptiScaler.txt` and the `Licenses` folder the engine's
own build produces.

## The app and the command-line tool

| Component | Used for | License |
|---|---|---|
| [.NET](https://github.com/dotnet/runtime) and [WPF](https://github.com/dotnet/wpf), Microsoft and the .NET Foundation | Runtime and user interface. Releases are self-contained, so the .NET runtime is bundled. | MIT |

## Tests and build only (not shipped)

| Component | Used for | License |
|---|---|---|
| [xUnit.net](https://github.com/xunit/xunit) | Unit tests | Apache-2.0 |
| [Microsoft.NET.Test.Sdk](https://github.com/microsoft/vstest) | Test runner | MIT |

## Not included

NVIDIA's DLSS 5 Neural Rendering model, `nvngx_dlssnr.dll`, is NVIDIA's and is not part of
NeuralSwitch. See [NOTICE.md](NOTICE.md).

If you find a component missing here or a license given wrongly, please open an issue.
