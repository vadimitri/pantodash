# pantodash

Pac-Man-like game for blind players on the [DualPanto](https://github.com/HassoPlattnerInstituteHCI/unity-dualpanto-toolkit)
haptic device. HPI BIS course project.

The handle is force-held at nodes of a track graph; push hard enough toward an
outgoing segment and the device dashes you to the next node, collecting audio
"blop" points on the way. Three levels: line, square, figure-8 with a monster.

## Setup

Clone **with submodules** — the toolkit and SpeechIO are nested submodules and
Unity will not compile without them:

```sh
git clone --recursive https://github.com/vadimitri/pantodash
# already cloned without --recursive?
git submodule update --init --recursive
```

Open `pantoDashUnity/` in Unity 6000.3. Then, in Project Settings → Player, set
**Active Input Handling = Both** (the toolkit uses the legacy `Input` API; the URP
template defaults to new-input-only and throws every frame otherwise).

No device? Tick `debug` on the `Panto` GameObject for the mouse emulator:
hold left mouse to drag the me-handle, right mouse for the it-handle, `b` cycles
the view.

## Layout

- `pantoDashUnity/` — the Unity project.
- `pantoDashUnity/Assets/PantoDashScripts/` — the game code Unity actually compiles.
- `Scripts/` — editing copy of the same files, kept in sync by hand.
- `GUIDE.md` — build plan. `BUGS.md` — toolkit bugs found (reported for the bounty).
  `CLAUDE.md` — accumulated project facts.
