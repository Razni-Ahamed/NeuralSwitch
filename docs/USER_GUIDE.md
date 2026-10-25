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

**The first time it starts**, NeuralSwitch shows **Before you start**: a summary of the
[user caution](../USER_CAUTION.md). Tick **I have read and accept the user caution…** and click
**Continue**. Nothing else works until you do, and **Quit** closes the app instead.

**NeuralSwitch keeps running in the notification area** (the icons by the clock) when you close its
window, so its hotkeys keep working while you play. Click the icon to open it again. Right-click it to
switch Neural Rendering, pick a style or **Quit**. You can turn this off in **Settings**. Starting
NeuralSwitch again while it's already running just brings its window back.

## 3. A tour of the window

![The NeuralSwitch window](screenshot.png)

The **sidebar** on the left has the four pages, and below them a box that always shows whether a game
is connected, what Neural Rendering is doing there, and your on/off hotkey.

**Now playing** controls the game that's running right now:

- **The game**, with its icon, its executable, and a **status pill** (Running, Off, Waiting for DLSS,
  Couldn't start…). The line under it says what's happening in words;
  [section 10](#10-what-the-messages-mean) explains each one.
- **The switch** turns Neural Rendering on or off.
- **Style** has three cards: Default, Natural and Cinematic. The current one has a tick. Picking a style
  also turns Neural Rendering on.
- **Intensity** goes from 0 to 2 (1 is normal) and controls how strongly the model's look is applied.
  **Reset** puts it back to 1.00.
- **Frames rendered**, **Neural Rendering cost** and your **hotkeys**, in three boxes at the bottom.
- **Try again** appears when the model failed to start. **Go to Games** appears when nothing is
  connected.
- When more than one prepared game is running, buttons at the top right choose which one to control.

These controls are greyed out until a prepared game is running.

**Games** lists the games in your library folders:

- The folder chips (for example `C:\Games ✕`) are the folders NeuralSwitch looks in. **Add folder** adds
  one, and **✕** stops looking in that one. **Rescan** looks again.
- A yellow banner appears when something is missing before games can be prepared (NVIDIA's model, or
  the engine), with **Locate model…**.
- Each game shows its icon, name, DLSS version, executable and state, with a button to open its folder
  and a **Prepare** or **Remove** button.

**Settings** has NVIDIA's model, your game folders, the hotkeys, the style new games start with,
whether to keep running in the notification area, and where the engine and your settings are.

**About** has the version, links to this guide, the user caution, the license and the project page,
and the credits.

**Notifications** appear at the bottom right after you do something: green when it worked, yellow or
red when it didn't. Good news goes away on its own; problems stay until you close them with **✕**.

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

**If it isn't found**, **Settings → NVIDIA's model** says *"NVIDIA's model isn't on this PC yet"*, and
the Games page shows a yellow banner. If you have a copy somewhere else, click **Locate model…** and
choose it. NeuralSwitch remembers your choice. **Search automatically** forgets it again.

**The signature check.** NeuralSwitch only uses the file without asking if Windows confirms it is
validly **signed by NVIDIA Corporation**. Settings then shows *"NVIDIA model 310.x"* with a green tick
and *"Signed by NVIDIA"*. If it says *"is not signed by NVIDIA"*, the file isn't NVIDIA's original.
Modified copies circulate online, and some contain malware. When you prepare a game with such a file,
NeuralSwitch asks first. The safe answer is **Cancel**; **Use it anyway** is your decision and your
risk.

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
2. On the **Games** page, click **Prepare** next to it.
3. A green notification confirms: *"<Game> is ready (the engine loads as winmm.dll). Start the game and
   turn DLSS on in its settings."* The game's state changes to **Ready**.

**What changed:** four files were added beside the game's executable (the engine, a small forwarder,
NVIDIA's model and `OptiScaler.ini`), plus a record file, `neuralswitch.installed.json`. Nothing of the
game's was changed, moved or overwritten. The game still starts **with Neural Rendering off**: until
you switch it on, it looks and plays as before.

**Game states:**

| State | Meaning |
|---|---|
| **Not prepared** | Nothing installed. **Prepare** is available. |
| **Ready** | Prepared. Start the game to use it. |
| **Playing** | Running and connected to NeuralSwitch. |
| **Anti-cheat** | Known anti-cheat was found. NeuralSwitch won't prepare this game; the reason is shown under its name. |
| **Other OptiScaler install** | OptiScaler is already in this game from somewhere else. Remove that first; NeuralSwitch won't touch it. |

## 7. Play with Neural Rendering

1. **Start the game** however you normally do (Steam, its launcher, its executable).
2. **Turn DLSS on** in the game's graphics settings: DLSS Super Resolution, any quality mode. Neural
   Rendering works on the frames DLSS produces, so **without DLSS on, nothing happens**. Some games
   need a restart after changing this.
3. A few seconds after the game starts drawing, it appears under **Now playing**. The sidebar says
   **Connected**, and its row in **Games** says **Playing**.
4. **Flip the switch**, or press **Ctrl+Alt+N** in the game. The engine shows a small notice in the
   corner of the game, *"DLSS Neural Rendering — On"*, and the status pill turns green: **Running**.
5. **Pick a style:**
   - **Natural** is the best place to start: more realistic light and materials while keeping the
     game's own colours.
   - **Default** is the strongest. It can look striking or overdone depending on the game.
   - **Cinematic** is softer and more film-like.

   Changing the style rebuilds the model, so expect a brief moment before the new look appears.
6. **Adjust Intensity** to taste. Lower is subtler; above 1 pushes the effect further.

**Performance.** **Frames rendered** is the game's real frame rate (without frame generation).
**Neural Rendering cost** is what it costs the GPU each frame, in milliseconds. Turning Neural Rendering
off removes that cost. Changing Intensity changes the look, not the cost.

## 8. Hotkeys

These work **while the game has focus**, so you don't need to switch windows:

| Hotkey | Does |
|---|---|
| **Ctrl+Alt+N** | Neural Rendering on/off |
| **Ctrl+Alt+B** | Next style: Default → Natural → Cinematic → Default |

NeuralSwitch must be running for its hotkeys to work. Running in the notification area is enough.

**To change them**, go to **Settings → Hotkeys**, type another letter or digit in either box, and click
**Apply**. They take effect straight away.

**If another program already uses a combination**, a yellow notification says so, and **Settings →
Hotkeys** shows which one with a warning sign. Pick another letter there.

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

**Status on Now playing** (the pill, and the line under it)

| Pill | Line under it | What to do |
|---|---|---|
| **Not connected** | *Prepare a game on the Games page, start it with DLSS on, and it appears here.* | Start a prepared game. |
| **Paused** | *The game is paused, minimised or loading. Changes apply as soon as it draws again.* | Nothing; go back to the game. |
| **Off** | *Neural Rendering is off. Turn it on with the switch, or press Ctrl+Alt+N in the game.* | Turn it on. |
| **Waiting for DLSS** | *It's on, and starts the next time the game renders with DLSS. Make sure DLSS is turned on in the game's graphics settings.* | Usually DLSS is off in the game; see [Troubleshooting](#12-troubleshooting). |
| **Running** | *Neural Rendering is working on every frame the game renders.* | Enjoy. |
| **Couldn't start** | The reason the model failed. | Fix the reason, then click **Try again**. |
| **Paused by F6** | *The in-game comparison key (F6) switched it off. Press F6 in the game, or turn it on here.* | Press F6 in the game, or turn it on in the app. |

**Notifications** (bottom right, after an action)

| Message | Meaning |
|---|---|
| *Close <game> first. Its files are in use while it runs.* | Prepare and Remove only work while the game is closed. |
| *<game> uses EasyAntiCheat … so NeuralSwitch will not install here.* | Anti-cheat found; the game is refused, on purpose. |
| *Another OptiScaler install is already in this game. Remove it first.* | Another tool put OptiScaler there. Uninstall it with that tool. |
| *<file> is already in the game folder and is not ours. Not overwriting it.* | A file NeuralSwitch needs to add already exists and belongs to something else. Nothing was changed. |
| *Every name the engine can load under is taken in this game.* | The game already uses all the names the engine can load under. |
| *Windows did not allow changes in <folder>. Run NeuralSwitch as administrator for this folder.* | The game is in a protected folder such as Program Files. |
| *Preparing needs NVIDIA's model, nvngx_dlssnr.dll.* | See [section 4](#4-get-nvidias-model). |
| *The engine folder is missing from beside NeuralSwitch.exe.* | Unzip the release again, keeping its folders. |
| *The game did not answer. It may have closed.* / *The game took too long to answer.* | The game closed, crashed or was busy (for example loading). Try again. |
| *The game isn't rendering right now; the change applies on its next frame.* | The game is paused or minimised. Nothing is wrong. |
| *Another program already uses Ctrl+Alt+…* | See [section 8](#8-hotkeys). |
| *Couldn't save settings: …* | Windows refused to write `%APPDATA%\NeuralSwitch\settings.json`. Check the folder isn't read-only. |

## 11. Remove it from a game, or uninstall NeuralSwitch

**Remove from one game:** close the game, then click **Remove** on the Games page. Every file
NeuralSwitch added is deleted, along with the engine's log (`OptiScaler.log`) and its capture folders.
The folder ends up exactly as it was before **Prepare**. If a copy of NVIDIA's model was already in the
folder before you prepared, it stays.

**Uninstall NeuralSwitch completely:**

1. Click **Remove** on **every** prepared game first. Deleting the app doesn't remove the engine from
   your games.
2. Quit NeuralSwitch: right-click its notification-area icon and choose **Quit**.
3. Delete the NeuralSwitch folder.
4. Delete `%APPDATA%\NeuralSwitch` (your settings).

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

**It says "Waiting for DLSS" and never "Running".**
- Turn **DLSS** on in the game's graphics settings. Not FSR, not XeSS, not TAA. Restart the game if it
  asks.
- Some games only call DLSS once you're actually in the game world, not in menus.

**It says "Couldn't start".**
- Read the reason under the status. Most often NVIDIA's model is missing, isn't NVIDIA's original, or your GPU or driver
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
- See [section 8](#8-hotkeys): the key may be taken (Settings shows which), or the game may run as administrator.

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
| `neuralswitch-cli install "<game folder>" --without-model` | Prepares the engine alone, to check a game before you have NVIDIA's model. The game connects and takes commands; switching Neural Rendering on then reports the model as missing. |
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

Everything here can be changed on the **Settings** page; you never need to edit this file. For
reference, NeuralSwitch keeps its settings in `%APPDATA%\NeuralSwitch\settings.json` (**Settings →
Open settings folder**). If you do edit it by hand, quit NeuralSwitch from its tray icon first.

```json
{
  "LibraryFolders": [ "C:\\Games", "D:\\SteamLibrary\\steamapps\\common" ],
  "ModelPath": null,
  "EngineDirectory": null,
  "DefaultStyle": "Natural",
  "ToggleHotkey": "N",
  "StyleHotkey": "B",
  "AcceptedCaution": 1,
  "CloseToTray": true,
  "TrayHintShown": true
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
| `AcceptedCaution` | Which version of the user caution you accepted on first run. `0` shows **Before you start** again. |
| `CloseToTray` | `true` keeps NeuralSwitch in the notification area when its window is closed. |
| `TrayHintShown` | Whether the "still running" tip has been shown once. |

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
