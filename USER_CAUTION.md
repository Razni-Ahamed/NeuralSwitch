# User caution: read this before using NeuralSwitch

> **In short:** NeuralSwitch is an unofficial, experimental hobby project. It puts files into your
> game folders and loads code into running games, using a driver feature that NVIDIA has not
> documented. **You use it entirely at your own risk.** It comes with **no warranty of any kind**. The
> authors and contributors are **not responsible** for what you do with it, or for anything that
> happens to your games, saves, accounts, PC or data as a result.
>
> If you don't accept that, don't download, install or use NeuralSwitch.

## What NeuralSwitch actually does on your PC

Know this before you decide to use it:

- **It adds files to game folders.** When you **Prepare** a game, it copies a modified build of
  OptiScaler (a third-party mod) beside the game's executable, under a name such as `winmm.dll`. It
  also copies NVIDIA's model file and a settings file there. Windows then loads that DLL into the game
  every time the game starts.
- **It changes how games render.** Inside the game, the engine intercepts the game's DLSS calls and
  runs an extra neural network pass over every frame.
- **It relies on undocumented behaviour.** NVIDIA hasn't published a way to use DLSS 5 Neural
  Rendering in games that weren't built for it. The engine drives it directly. That can stop working,
  or start behaving differently, after any driver or game update.
- **It talks to running games.** The app sends commands to the engine inside the game through a local
  named pipe. Only your own Windows account on your own PC can reach it.

## Risks you accept by using it

### Bans and anti-cheat

- **Never use NeuralSwitch in online, multiplayer or competitive games**, or in the online modes of
  any game. Anti-cheat systems and publishers treat code loaded into a game as cheating, even when it
  only changes visuals, and can **permanently ban your account**.
- NeuralSwitch refuses to prepare games when it finds known anti-cheat software (EasyAntiCheat,
  BattlEye, EA AntiCheat and others). **That detection is not complete.** Some games use anti-cheat
  that NeuralSwitch can't see, or switch it on later in an update. A single-player game with online
  features (for example Red Dead Redemption 2 and Red Dead Online) can still flag a modified install.
- **A ban is your responsibility.** The authors can't help reverse one and accept no liability for it.

### Game terms and licenses

- Many games' end-user license agreements and terms of service forbid modifying the game or loading
  third-party code into it. **It is up to you to check and follow the terms of every game you use
  NeuralSwitch with.**
- The same goes for NVIDIA's license for its drivers and for `nvngx_dlssnr.dll`.

### Stability and your data

- **Things can go wrong.** Games can crash, freeze, show visual glitches or perform worse. The
  graphics driver can reset ("display driver stopped responding"). The PC can become unstable while a
  game runs.
- **Back up your saves** before using NeuralSwitch with a game you care about.
- **Game and driver updates can break it.** An update can conflict with the engine or replace its
  files. "Verify game files" in a launcher usually leaves extra DLLs where they are. Use **Remove** in
  NeuralSwitch to take the engine out properly.

### NVIDIA's model file

- `nvngx_dlssnr.dll` belongs to NVIDIA. **NeuralSwitch doesn't include it, distribute it or download
  it.** How you get a copy, and whether you're allowed to use it, is your responsibility.
- **Modified and fake copies circulate online, and some carry malware.** NeuralSwitch checks that the
  file is signed by NVIDIA and warns you before using one that isn't. Using an unsigned file anyway is
  your decision and your risk.
- Never turn off your antivirus to make a file work.

### Health, heat and hardware

- **Photosensitivity.** Neural Rendering changes lighting and contrast, and it can flicker, especially
  in fast motion. If you're sensitive to flashing or flickering images, or feel any discomfort, stop
  immediately.
- **Heat.** It adds GPU load. Laptops in particular run hotter and louder. Keep vents clear, and stop
  if temperatures concern you.

### Downloads

- **Only download NeuralSwitch from its official repository**,
  <https://github.com/Razni-Ahamed/NeuralSwitch>, or build it yourself from the source. Copies from
  anywhere else may have been tampered with.
- Releases are not code-signed. Windows SmartScreen and some antivirus programs may warn about the app
  or about the engine DLL, because it loads into other programs.

## No affiliation

NeuralSwitch is **not affiliated with, endorsed by or supported by NVIDIA**, AMD, Intel, Microsoft, any
game developer or publisher, or the authors of OptiScaler and OptiScaler-DLSS5. Don't contact them for
support with NeuralSwitch. Names like DLSS are used only to describe compatibility; see
[NOTICE.md](NOTICE.md).

## No warranty and no liability

NeuralSwitch is licensed under the GNU General Public License, version 3 ([LICENSE](LICENSE)).
Sections 15 and 16 of that license apply in full. In plain terms:

- **No warranty.** NEURALSWITCH IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
  INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
  NON-INFRINGEMENT. THE ENTIRE RISK AS TO ITS QUALITY AND PERFORMANCE IS WITH YOU.
- **No liability.** TO THE FULLEST EXTENT PERMITTED BY APPLICABLE LAW, IN NO EVENT WILL THE AUTHORS,
  CONTRIBUTORS OR COPYRIGHT HOLDERS BE LIABLE TO YOU FOR ANY DAMAGES, INCLUDING ANY GENERAL, SPECIAL,
  INCIDENTAL OR CONSEQUENTIAL DAMAGES ARISING OUT OF THE USE OR INABILITY TO USE NEURALSWITCH. THIS
  INCLUDES, BUT IS NOT LIMITED TO, LOSS OF DATA OR SAVES, ACCOUNT SUSPENSIONS OR BANS, HARDWARE OR
  SOFTWARE FAILURES, AND LOSSES CAUSED BY THIRD-PARTY FILES. THIS APPLIES EVEN IF THEY HAVE BEEN
  ADVISED OF THE POSSIBILITY OF SUCH DAMAGES.

Some places don't allow certain warranties or liabilities to be excluded. There, these exclusions apply
as far as the law allows.

## Your responsibilities

By downloading, installing or using NeuralSwitch, you confirm that:

1. you have read and understood this caution, and you use NeuralSwitch at your own risk;
2. you are solely responsible for how you use it and for the consequences;
3. you will follow the laws that apply to you and the terms of the games, platforms and software you
   use it with;
4. you won't use it to cheat or to gain an unfair advantage over other players;
5. you won't redistribute NVIDIA's files, or anyone else's, in breach of their licenses.

## Reporting problems

Report bugs, crashes and security issues through the repository's
[Issues](https://github.com/Razni-Ahamed/NeuralSwitch/issues). Include `OptiScaler.log` from the game
folder when the problem happens in a game. Support is voluntary and offered without any guarantee.
