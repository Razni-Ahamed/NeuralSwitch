# The remote-control protocol

The patched engine listens on a named pipe inside each game it is loaded into. NeuralSwitch (and
anything else you write) uses it to switch DLSS 5 Neural Rendering while the game runs.

## The pipe

```
\\.\pipe\neuralswitch-<process id of the game>
```

* **Local only.** It is created with `PIPE_REJECT_REMOTE_CLIENTS`. With the default named-pipe
  security, only the user running the game (and administrators) can write to it.
* **One client at a time.** The engine serves one connection, then waits for the next. Hold a
  connection only for as long as one request takes.
* **Finding games.** List `\\.\pipe\` and look for names that start with `neuralswitch-`. The number
  after the dash is the game's process id.
* **Off switch.** `[DlssNr] RemoteControl=false` in the game's `OptiScaler.ini` stops the pipe from
  being created at all.

The pipe starts on the game's first rendered frame, not at launch, so expect it to appear a few
seconds after the game window does.

## Requests

Each request is one line of UTF-8 text ending in `\n`. Each answer is one line of JSON ending in `\n`.
A connection can carry several requests in a row.

| Request | What it does |
|---|---|
| `hello` / `get` | Returns the state. Changes nothing. |
| `set enabled=<true\|false>` | Turns Neural Rendering on or off. Also accepts `1`/`0` and `on`/`off`. |
| `set style=<default\|natural\|cinematic>` | Changes the model's style (or `0`, `1`, `2`). The model rebuilds, which takes a moment. |
| `set intensity=<0..2>` | Changes the model's intensity. Always use a dot as the decimal separator. |
| `toggle` | Flips on/off. |
| `retry` | Clears the engine's failure latch so the model is tried again without restarting the game. |

You can combine settings in one `set`: `set enabled=true style=natural`. The engine checks every value
before applying any of them, so one bad value means nothing changes.

Turning Neural Rendering on from the pipe also resets the in-game comparison key (F6) from "off" back
to your settings. Otherwise "on" could quietly mean "not running".

### How changes are applied

The engine never touches settings from the pipe's thread. It queues the request, and the game's render
thread applies it on the next frame, the same place the in-game hotkey takes effect. The answer waits
up to 500 ms for that frame. When the game isn't drawing (paused, minimised, loading), the answer comes
back with `"applied": false`, and the change lands as soon as the game draws again.

## Answers

```json
{"ok":true,"applied":true,"protocol":1,"pid":4242,"exe":"Spider-Man2.exe","rendering":true,
 "enabled":true,"style":1,"styleName":"natural","running":true,"failure":"","mode":"your settings",
 "compare":"yours","intensity":0.800,"fps":58.3,"nrMs":2.41}
```

| Field | Meaning |
|---|---|
| `ok` | `false` means the request was refused; `error` says why. |
| `applied` | Only present for changes. Whether the game applied the change before answering. |
| `protocol` | `1`. Raised only for changes that break existing clients. |
| `rendering` | The game drew a frame in the last second. |
| `enabled` | The setting: Neural Rendering on or off. |
| `style`, `styleName` | `0` default, `1` natural, `2` cinematic. |
| `intensity` | 0 to 2. |
| `running` | The model is loaded and working on the game's frames. Can be `false` while `enabled` is `true`: the game hasn't rendered with DLSS yet, or it failed. |
| `failure` | Why the model is not running, after it tried and failed. Empty otherwise. |
| `mode` | What the engine is running right now, as its on-screen line says it. |
| `compare` | The comparison key's mode: `yours`, `vanilla` or `off`. |
| `fps` | Frames the game renders per second, frame generation excluded. Counted from DLSS calls, so `0` in a game that hasn't called DLSS. |
| `nrMs` | What the Neural Rendering pass costs on the GPU per frame, in milliseconds. |

Errors look like this:

```json
{"ok":false,"error":"style takes default, natural or cinematic (or 0, 1, 2)"}
```

## Trying it without a game

`neuralswitch-cli simulate` serves the same protocol from a pretend game, so you can build a client
against it. `tools/engine-smoke-test` loads the real engine into a tiny Direct3D 12 program and
drives it end to end.
