# NeuralSwitch user guide

This guide walks through setting up NeuralSwitch, using it while you play, and fixing problems.

> **Before anything else, read [USER_CAUTION.md](../USER_CAUTION.md).** NeuralSwitch loads a
> third-party mod into your games. Never use it in online or multiplayer games. You use it at your own
> risk.

## Contents

1. [Before you start](#1-before-you-start)
2. [Install NeuralSwitch](#2-install-neuralswitch)
3. [A tour of the window](#3-a-tour-of-the-window)
4. [Get NVIDIA's model](#4-get-nvidias-model)
5. [Add your game folders](#5-add-your-game-folders)
6. [Prepare a game](#6-prepare-a-game)
7. [Play with Neural Rendering](#7-play-with-neural-rendering)
8. [Hotkeys](#8-hotkeys)
9. [The engine's own in-game menu](#9-the-engines-own-in-game-menu)
10. [What the messages mean](#10-what-the-messages-mean)
11. [Remove it from a game, or uninstall NeuralSwitch](#11-remove-it-from-a-game-or-uninstall-neuralswitch)
12. [Troubleshooting](#12-troubleshooting)
13. [The command-line tool](#13-the-command-line-tool)
14. [The settings file](#14-the-settings-file)
15. [Questions](#15-questions)

---

## 1. Before you start

Check every item:

- [ ] Windows 10 or 11, 64-bit.
- [ ] An **NVIDIA RTX 50 series** GPU. NVIDIA's model refuses to run on older cards.
- [ ] **NVIDIA driver 616.56 or newer**. Check in the NVIDIA app, or run `nvidia-smi` in a terminal.
- [ ] **NVIDIA's model file, `nvngx_dlssnr.dll`**. See [section 4](#4-get-nvidias-model).
- [ ] A **single-player** game that supports **DLSS** upscaling.
- [ ] A backup of your saves for any game you care about.

## 2. Install NeuralSwitch

1. Open the [Releases page](https://github.com/Razni-Ahamed/NeuralSwitch/releases) and download
   `NeuralSwitch-<version>-win-x64.zip`. **Only download from this repository.**
2. Unzip it into a folder of your own, for example `C:\Tools\NeuralSwitch`. Avoid `Program Files`, which
   needs administrator rights.
3. Check that the folder has `NeuralSwitch.exe`, `neuralswitch-cli.exe` and an **`engine`** folder.
   The app needs the `engine` folder beside it.
4. Run `NeuralSwitch.exe`.

You don't need to install .NET: releases include everything they need.

If **Windows SmartScreen** says "Windows protected your PC": releases aren't code-signed, so Windows
doesn't know the publisher. If you downloaded the zip from the official repository, choose **More
info → Run anyway**. If you're not sure where the file came from, don't run it.

## 3. A tour of the window

![The NeuralSwitch window](screenshot.png)

**Now playing** (top) controls the game that's running right now:

- **The game's name** and a status line ([section 10](#10-what-the-messages-mean) explains each one).
- **The switch** (ON / OFF) turns Neural Rendering on or off.
- **Style** picks Default, Natural or Cinematic. Picking a style also turns Neural Rendering on.
- **Intensity** goes from 0 to 2 (1 is normal) and controls how strongly the model's look is applied.
- **Hotkeys** shows the keys that work while the game has focus.
- **Try again** appears when the model failed to start.

These controls are greyed out until a prepared game is running.

**Games** (middle) shows the games in your library folders:

- The folder chips (for example `C:\Games ✕`) are the folders NeuralSwitch looks in. **+ Add folder**
  adds one, and **✕** stops looking in that one.
- Each game shows its name, its DLSS version, its executable and its state, with a **Prepare** or
  **Remove** button.
- **Rescan** looks again, for example after you install a new game.

**The bottom lines** show:

- whether NVIDIA's model was found, plus **Locate model…**;
- whether the engine is ready;
- the result of the last thing you did (in green).

## 4. Get NVIDIA's model

Neural Rendering is done by NVIDIA's model, `nvngx_dlssnr.dll`. **NeuralSwitch doesn't include it and
never downloads it.**

**Where it comes from.** NVIDIA ships it in newer drivers and with games that support DLSS 5. Once
you have a driver or a game that includes it, NeuralSwitch can usually find it on its own.

**Where NeuralSwitch looks:**

- your NVIDIA driver's folder in `C:\Windows\System32\DriverStore\FileRepository`;
- NVIDIA's model download folder, `C:\ProgramData\NVIDIA\NGX\models`;
- `%LOCALAPPDATA%\RHI\DLSS-NR`;
- the folders of the games in your library.

**If it isn't found**, the bottom line says *"NVIDIA's model (nvngx_dlssnr.dll) is not on this PC yet."*
If you have a copy somewhere else, click **Locate model…** and choose it. NeuralSwitch remembers your
choice.

**The signature check.** NeuralSwitch only uses the file without asking if Windows confirms it is
validly **signed by NVIDIA Corporation**. The bottom line then reads *"NVIDIA model 310.x · signed by
NVIDIA"*. If it says *"is NOT signed by NVIDIA. It may have been modified."*, the file isn't NVIDIA's
original. Modified copies circulate online, and some contain malware. NeuralSwitch asks before using
such a file, and the safe answer is **No**.

## 5. Add your game folders

NeuralSwitch expects a **library folder**: a folder where **each subfolder is one game**. For
example:

```
C:\Games\                       ← add this folder
├── Marvel's Spider-Man 2\       ← one game
├── Red Dead Redemption 2\       ← one game
└── AC Black Flag Resynced\      ← one game
```

`C:\Games` is added automatically if it exists. For Steam, add `...\steamapps\common`. For other
launchers, add the folder that holds their game folders.

A game shows up only if it ships DLSS: an `nvngx_dlss.dll` somewhere in its folder. Games without
DLSS can't use NeuralSwitch.

## 6. Prepare a game

1. **Close the game** if it's running.
2. Click **Prepare** next to it.
3. The bottom line confirms: *"<Game> is ready (the engine loads as winmm.dll). Start the game and turn
   DLSS on in its settings."*

**What changed:** four files were added beside the game's executable (the engine, a small forwarder,
NVIDIA's model and `OptiScaler.ini`), plus a record file, `neuralswitch.installed.json`. Nothing of the
game's was changed, moved or overwritten. The game still starts **with Neural Rendering off**: until
you switch it on, it looks and plays as before.

**Game states:**

| State | Meaning |
|---|---|
| **Not prepared** | Nothing installed. **Prepare** is available. |
| **Ready** | Prepared. Start the game to use it. |
| **Playing** | Prepared, running, and connected to NeuralSwitch. |
| **Anti-cheat** | Known anti-cheat was found. NeuralSwitch won't prepare this game; the reason is shown under its name. |
| **Other OptiScaler install** | OptiScaler is already in this game from somewhere else. Remove that first; NeuralSwitch won't touch it. |

## 7. Play with Neural Rendering

1. **Start the game** however you normally do (Steam, its launcher, its executable).
2. **Turn DLSS on** in the game's graphics settings: DLSS Super Resolution, any quality mode. Neural
   Rendering works on the frames DLSS produces, so **without DLSS on, nothing happens**. Some games
   need a restart after changing this.
3. A few seconds after the game starts drawing, it appears under **Now playing**. Its row in **Games**
   says **Playing**.
4. **Flip the switch to ON**, or press **Ctrl+Alt+N** in the game. The engine shows a small message in
   the corner of the game, *"DLSS Neural Rendering: On"*.
5. **Pick a style:**
   - **Natural** is the best place to start: more realistic light and materials while keeping the
     game's own colours.
   - **Default** is the strongest. It can look striking or overdone depending on the game.
   - **Cinematic** is softer and more film-like.

   Changing the style rebuilds the model, so expect a brief moment before the new look appears.
6. **Adjust Intensity** to taste. Lower is subtler; above 1 pushes the effect further.

**Performance.** The status line shows *"Running · 72 fps rendered · 3.1 ms per frame"*. The ms figure
is what Neural Rendering costs the GPU each frame. Turning Neural Rendering off removes that cost.
Changing Intensity changes the look, not the cost.

## 8. Hotkeys

These work **while the game has focus**, so you don't need to switch windows:

| Hotkey | Does |
|---|---|
| **Ctrl+Alt+N** | Neural Rendering on/off |
| **Ctrl+Alt+B** | Next style: Default → Natural → Cinematic → Default |

NeuralSwitch must be running (it can be minimised) for its hotkeys to work.

**If a hotkey shows "(taken)"**, another program already uses that combination, and the bottom line
names it. Pick other letters in the [settings file](#14-the-settings-file) (`ToggleHotkey`,
`StyleHotkey`) and restart NeuralSwitch.

**If the game runs as administrator,** run NeuralSwitch as administrator too. Windows blocks a normal
program from sending commands to an elevated one, so neither the buttons nor the hotkeys would reach
the game.

## 9. The engine's own in-game menu

The engine (OptiScaler-DLSS5) has its own menu inside the game. NeuralSwitch doesn't need it, but it's
there for fine-tuning:

| Key in game | Does |
|---|---|
| **Insert** | Opens the engine's menu. Neural Rendering's settings are on the **DLSS 5** tab. |
| **F6** | Cycles what Neural Rendering runs as, for comparison: your settings → as the engine ships it → off. |

Settings you change in that menu apply to that game. They're kept if you save them there, and the app
shows the result. If you used **F6** to switch it off, the app says so. Turning Neural Rendering on
from NeuralSwitch also resets F6 back to your settings.

## 10. What the messages mean

**Status line under Now playing**

| Message | Meaning and what to do |
|---|---|
| *Prepare a game below, start it, and it appears here.* | No prepared game is running. |
| *Paused, minimised or loading.* | The game hasn't drawn a frame in the last second. Changes apply as soon as it does. |
| *Neural Rendering is off.* | It's switched off. Flip the switch or press Ctrl+Alt+N. |
| *Starting… The model loads the next time the game renders with DLSS. Make sure DLSS is on in the game's graphics settings.* | It's switched on but hasn't run yet. Usually DLSS is off in the game; see [Troubleshooting](#12-troubleshooting). |
| *Running · N fps rendered · N ms per frame* | Working. N fps is the game's real frame rate (without frame generation). |
| *Could not start: …* | The model failed to start; the reason follows. Click **Try again** after fixing it. |
| *Switched off by the in-game comparison key (F6). Press it again in game.* | Someone pressed F6 in the game. Press F6 until it's back to your settings, or switch Neural Rendering on from the app. |

**Messages after an action (bottom line)**

| Message | Meaning |
|---|---|
| *Close <game> first. Its files are in use while it runs.* | Prepare and Remove only work while the game is closed. |
| *<game> uses EasyAntiCheat … so NeuralSwitch will not install here.* | Anti-cheat found; the game is refused, on purpose. |
| *Another OptiScaler install is already in this game. Remove it first.* | Another tool put OptiScaler there. Uninstall it with that tool. |
| *<file> is already in the game folder and is not ours. Not overwriting it.* | A file NeuralSwitch needs to add already exists and belongs to something else. Nothing was changed. |
| *Every name the engine can load under is taken in this game.* | The game already uses all the names the engine can load under. |
| *Windows did not allow changes in <folder>. Run NeuralSwitch as administrator for this folder.* | The game is in a protected folder such as Program Files. |
| *NVIDIA's model nvngx_dlssnr.dll is needed first.* | See [section 4](#4-get-nvidias-model). |
| *The engine was not found beside the app.* | The `engine` folder is missing from beside `NeuralSwitch.exe`. Unzip the release again, keeping its folders. |
| *The game did not answer. It may have closed.* / *The game took too long to answer.* | The game closed, crashed or was busy (for example loading). Try again. |
| *The game is not rendering right now; the change applies on its next frame.* | The game is paused or minimised. Nothing is wrong. |

## 11. Remove it from a game, or uninstall NeuralSwitch

**Remove from one game:** close the game, then click **Remove**. Every file NeuralSwitch added is
deleted, along with the engine's log (`OptiScaler.log`) and its capture folders. The folder ends up
exactly as it was before **Prepare**. If a copy of NVIDIA's model was already in the folder before you
prepared, it stays.

**Uninstall NeuralSwitch completely:**

1. Click **Remove** on **every** prepared game first. Deleting the app doesn't remove the engine from
   your games.
2. Delete the NeuralSwitch folder.
3. Delete `%APPDATA%\NeuralSwitch` (your settings).

Nothing else is left behind: no registry entries, services or startup items.

## 12. Troubleshooting

**My game isn't in the list.**
- Is its folder inside one of your library folders, one level down? See [section 5](#5-add-your-game-folders).
- Does it support DLSS? Only games with `nvngx_dlss.dll` are listed.
- Click **Rescan** after installing or moving games.

**The game runs, but "Now playing" stays empty.**
- Wait a few seconds after the game starts drawing; the connection opens on the first frame.
- Check the game says **Ready** in the list, and that you started the same copy you prepared.
- Look in the game's folder for `OptiScaler.log`:
  - **No log at all** means the engine didn't load in this game. Please open an issue with the game's
    name.
  - **The log exists** but has no line containing `remote control listening`: please open an issue and
    attach the log.
- If the game runs as administrator, run NeuralSwitch as administrator too.

**It says "Starting…" and never "Running".**
- Turn **DLSS** on in the game's graphics settings. Not FSR, not XeSS, not TAA. Restart the game if it
  asks.
- Some games only call DLSS once you're actually in the game world, not in menus.

**It says "Could not start: …".**
- Read the reason. Most often NVIDIA's model is missing, isn't NVIDIA's original, or your GPU or driver
  isn't supported (RTX 50 series, driver 616.56 or newer).
- After fixing it, click **Try again**. No restart is needed.

**The picture looks oversaturated, too contrasty, or "AI-looking".**
- Switch to **Natural** and lower **Intensity** to around 0.6–0.8.

**The game got slower.**
- Neural Rendering costs GPU time, shown as "ms per frame". Use a faster DLSS mode in the game
  (Balanced or Performance) to win it back, or switch Neural Rendering off when you don't want it.

**The game crashes or behaves strangely after a game update.**
- Close it, click **Remove**, then **Prepare** again. If it still crashes, keep it removed and open an
  issue.

**My antivirus flagged `winmm.dll` (or `version.dll`) in a game folder.**
- That's the engine. It loads into the game, which is the kind of behaviour antivirus software looks
  for. If you downloaded NeuralSwitch from the official repository, it's the file NeuralSwitch put
  there. Whether to allow it is your decision; never turn off your antivirus entirely.

**The hotkeys don't work.**
- See [section 8](#8-hotkeys): the key may be taken, or the game may run as administrator.

**Reporting a problem.** Open an [issue](https://github.com/Razni-Ahamed/NeuralSwitch/issues) with:
- the game's name and version;
- your GPU and driver version;
- what you did and what happened;
- `OptiScaler.log` from the game's folder.

## 13. The command-line tool

`neuralswitch-cli.exe` sits beside the app and can do everything it can, which is useful for scripts,
stream decks and macro keys. Run it from a terminal in the NeuralSwitch folder.

**While a game runs**

| Command | Does |
|---|---|
| `neuralswitch-cli status` | Shows each running game's state |
| `neuralswitch-cli on` / `off` / `toggle` | Switches Neural Rendering |
| `neuralswitch-cli style natural` | `default`, `natural` or `cinematic` |
| `neuralswitch-cli intensity 0.8` | 0 to 2, with a dot as the decimal separator |
| `neuralswitch-cli retry` | Tries the model again after a failure |

Add `--pid <process id>` when more than one prepared game is running.

**Setting up**

| Command | Does |
|---|---|
| `neuralswitch-cli games` | Lists DLSS games in your library folders, or in folders you give it |
| `neuralswitch-cli model` | Shows where NVIDIA's model was found and whether it's signed |
| `neuralswitch-cli install "<game folder>"` | Prepares a game. Options: `--model <dll>`, `--style <style>`, `--on` (start with Neural Rendering on), `--engine <folder>` |
| `neuralswitch-cli uninstall "<game folder>"` | Removes it again |

**Testing**

| Command | Does |
|---|---|
| `neuralswitch-cli simulate` | Runs a pretend game that the app connects to, for trying the controls without a game. Press Enter to stop. |

Example:

```
neuralswitch-cli install "C:\Games\Marvel's Spider-Man 2" --style natural
neuralswitch-cli on
neuralswitch-cli intensity 0.75
```

## 14. The settings file

NeuralSwitch keeps its settings in `%APPDATA%\NeuralSwitch\settings.json`. The app creates the file
the first time you add or remove a folder or locate the model. If it doesn't exist yet, create it with
the content below. Close NeuralSwitch before editing it by hand.

```json
{
  "LibraryFolders": [ "C:\\Games", "D:\\SteamLibrary\\steamapps\\common" ],
  "ModelPath": null,
  "EngineDirectory": null,
  "DefaultStyle": "Natural",
  "ToggleHotkey": "N",
  "StyleHotkey": "B"
}
```

| Setting | Meaning |
|---|---|
| `LibraryFolders` | Folders whose subfolders are games. |
| `ModelPath` | NVIDIA's model chosen with **Locate model…**. `null` searches automatically. |
| `EngineDirectory` | A different engine folder. `null` uses the `engine` folder beside the app. |
| `DefaultStyle` | The style a newly prepared game starts with: `Default`, `Natural` or `Cinematic`. |
| `ToggleHotkey` | The letter in Ctrl+Alt+*letter* for on/off. |
| `StyleHotkey` | The letter in Ctrl+Alt+*letter* for next style. |

## 15. Questions

**Can I use it in online games?** No. See [USER_CAUTION.md](../USER_CAUTION.md). You can be banned.

**Does it work on AMD, Intel or older NVIDIA GPUs?** No. NVIDIA's model needs an RTX 50 series GPU.

**Does it change my game files?** No. It only adds files beside them, and **Remove** takes those away
again.

**Does NeuralSwitch need to stay open?** Only for switching. The game runs fine without it; Neural
Rendering just stays as it was last set.

**Can I use it together with ReShade?** Usually, yes. ReShade normally loads as `dxgi.dll`, and the
engine uses a different name. **Don't** run another DLSS 5 mod in the same game at the same time (for
example RenoDX's DLSS 5 add-on): both would run the model.

**Can I control two games at once?** The app connects to the first prepared game it finds running. The
command-line tool takes `--pid` to choose.

**Does it send any data anywhere?** No. NeuralSwitch makes no network connections. Its pipe to the game
is local to your PC. Games it prepares have the engine's own online update check turned off.

**Is this made by NVIDIA?** No. It's an unofficial community project, not affiliated with NVIDIA.
