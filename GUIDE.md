# pantodash — Build Guide

Step-by-step instructions to build and finish the game. Code is already
written in `Scripts/` — this guide covers everything code can't do: project
setup, Unity editor wiring, testing, and tuning. Background/API facts live in
`CLAUDE.md` (the knowledgebase).

**The game in one paragraph:** the me-handle is force-held at a node of a
track graph ("everything is a wall"). The player pushes against the hold;
pushing hard enough toward an outgoing track segment makes the device dash
the handle straight to the next bifurcation, collecting "blop" points on the
way, where it stops again. Level 3 adds a monster on the it-handle. Built for
blind play: all feedback is haptic + audio.

---

## Step 1 — Project setup (~30 min)

Base: **fresh Unity project + toolkit as submodule.** Do NOT copy another
project as a base (bis-rogue's game code is dungeon machinery we don't need).

1. Install Unity **6000.3.17f1** (or 6000.3.18f1) via Unity Hub.
2. Create a blank **3D** project named `PantodashUnity` **inside this
   `pantodash/` folder** (so agents working here see `CLAUDE.md`).
3. In the project root:
   ```sh
   cd PantodashUnity
   git init
   curl -o .gitignore https://raw.githubusercontent.com/github/gitignore/main/Unity.gitignore
   cd Assets
   git submodule add git@github.com:HassoPlattnerInstituteHCI/unity-dualpanto-toolkit
   git submodule update --init --recursive   # pulls SpeechIO inside the toolkit too
   ```
   If SpeechIO is separate (check for `Assets/unity-dualpanto-toolkit/Assets/SpeechIOForUnity` being empty),
   add it per the toolkit README:
   `git submodule add git@github.com:HassoPlattnerInstituteHCI/SpeechIOForUnity` in `Assets`.
   **SpeechIO is required** — `GameManager.cs` uses `SpeechIO.SpeechOut`.
4. Copy `pantodash/Scripts/*.cs` into `Assets/PantodashScripts/`.
5. Open the project. Drag the **Panto prefab** (`Assets → unity-dualpanto-toolkit
   → Assets → Resources`) into the scene. Delete the default Main Camera and
   Directional Light (the prefab brings its own).
6. On the Panto object's **DualPantoSync** component: leave **Debug = true**
   (emulator mode) for now.
7. Press Play. You should see two handle objects following the mouse. Press
   `b` to cycle blind/mixed/develop view. If this works, setup is done. Commit.

## Step 2 — Wire the core scene (~30 min)

Scene hierarchy to build (one scene, three level roots — no scene loading):

```
Panto (prefab)
GameManager        (GameManager component)
Player             (DashController component; empty GameObject)
Level1             (DashLevel component)  ← root, contains nodes + pickups
Level2             (DashLevel component)
Level3             (DashLevel component, contains Monster)
```

1. Empty GameObject `GameManager`, add `GameManager` component.
2. Empty GameObject `Player`, add `DashController`.
3. Empty GameObject `Level1`, add `DashLevel` component.

**Build Level 1 ("long ride" — a straight horizontal line):**

1. Under `Level1`, create two empty children `NodeL` and `NodeR`, add
   `TrackNode` to each. Place them at y = 0, inside the **Panto Working Area**
   shown by the prefab (e.g. left edge and right edge of the area, same z).
2. Link them: in `NodeL`'s TrackNode, add `NodeR` to `neighbors`, and vice
   versa. You should see a cyan gizmo line between them in the Scene view.
3. Add 3 small spheres under `Level1` (scale ~0.3), placed **on** the line,
   add `Pickup` to each.
4. `Level1` inspector: `startNode = NodeL`,
   `introText = "dash to the right and collect points on your way"`.
5. `GameManager` inspector: `levels = [Level1]`, assign `blopClip`
   (any short sound — see Step 5).

## Step 3 — Emulator test (do this before building more levels)

Press Play. Expected sequence:

1. After ~2 s the player parks at NodeL, intro is spoken.
2. Move the mouse away from NodeL horizontally: when the (emulated) handle is
   more than `pressThreshold` from the node, the handle dashes to NodeR,
   pickups "blop" as it passes them, then "hurray!" speech.

Checklist / known first-run issues:

- **Nothing dashes:** enable `logPress` on DashController and watch the
  console. If press never grows: `Freeze()` may lock the emulator handle to
  the node so the mouse can't displace it → untick `freezeAtNodes`
  (emulator only — retick for hardware).
- **Dashes instantly / repeatedly:** raise `pressThreshold`, raise `cooldown`.
- **No speech:** SpeechIO submodule missing or not initialized (Step 1.3).
- **Pickups not collected:** raise `pickupRadius`; make sure pickups sit on
  the line between the nodes.
- Win with 0 pickups is instant — every level needs ≥ 1 pickup.

Commit when Level 1 plays end-to-end in the emulator.

## Step 4 — Levels 2 and 3 (~45 min)

**Level 2 ("loopy loop" — square):** root `Level2` + `DashLevel` component.
4 corner nodes, each linked to its 2 adjacent corners (both directions!).
3 pickups on the edges (per the PDF: top edge, and two on the left edge).
`introText = "dash around the square and get all points!"`.
Add `Level2` to GameManager's `levels` array (after Level1).

**Level 3 ("figure 8"):** 6 nodes — a 2×3 grid of corners:

```
A---B---C
|   |   |
D---E---F
```

Neighbors: A↔B, B↔C, A↔D, B↔E, C↔F, D↔E, E↔F. B and E are the real
bifurcations (3 exits). 3+ pickups spread over the outer edges.
`introText = "outrun the monster in the middle of the map and collect all cookies"`.

**Monster:** under `Level3`, create a small cube `Monster`, add
`MonsterController`: `startNode = E` (middle-bottom), tune `speed` low
(slower than a dash!), `killRadius ≈ 0.8`. Add an **AudioSource** to it:
looping growl/hum clip, Play On Awake, **Spatial Blend = 1** so the player
hears where it is.

Disable `Level1/2/3` roots' checkbox? Not needed — GameManager deactivates
all of them on Start and activates them one by one.

Test the whole 3-level flow in the emulator. Death should speak "you have
been killed" and restart level 3 with the monster back at E.

## Step 5 — Audio polish (~30 min)

- `blop`: any short percussive click/pop (freesound.org, or record one).
  Import into `Assets/Audio/`, assign to GameManager.
- Monster loop: low hum/growl.
- Optional juice, in order of value:
  1. Rising blop pitch per point collected in one dash (combo feel).
  2. A short whoosh at dash start (`AudioSource.PlayClipAtPoint` in `DashTo`).
  3. Distinct "thunk" on arrival at a node.

## Step 6 — Hardware bring-up (~1 h, needs the device)

1. Turn the panto on (switch on the back), calibrate: linkages closed,
   handles pointing right, press the reset button next to USB, wait 3 s.
2. DualPantoSync: **Debug = false**. Default macOS port is
   `/dev/cu.SLAB_USBtoUART`; if not found, `ls /dev/cu.*` with/without the
   device plugged in and put the new one into *Overwrite Default Port*.
   (Driver: Silicon Labs CP210x — see toolkit README §3.)
3. First run: macOS may block `libserial.dylib` → allow it in Settings →
   Privacy & Security, restart Unity.
4. Retick `freezeAtNodes` if you unticked it for the emulator.
5. Play Level 1. The handle should pull itself to NodeL and hold.

## Step 7 — Tuning (the actual assignment quality lives here)

Tune in this order, on hardware, with `logPress` on:

| Parameter | Start | Symptom too low | Symptom too high |
|---|---|---|---|
| `pressThreshold` | 1.0 | accidental dashes while resting | fighting the motors; firmware logs "Skipping god object" |
| `cooldown` | 0.4 | residual push chains dashes | game feels laggy |
| `maxAngle` | 45° | — | wrong-direction dashes at bifurcations |
| `dashSpeed` | 30 | dash feels like a slow escort | overshoot/oscillation at nodes, scary jerk |
| monster `speed` | 3 | trivial level 3 | unwinnable level 3 |

Watch `press.magnitude` in the console while pressing comfortably hard —
set `pressThreshold` to ~60 % of that. Then blind-test: eyes closed (or `b`
for blind view), can you complete level 2 by feel + audio alone? That's the
bar the graders will use.

## Step 8 — Troubleshooting (device)

- Handles don't move / "Revision id not matching" → reset button, power cycle.
- Unity freezes on Play → power-cycle panto, press Play again (toolkit
  resets the device over serial first; a stuck firmware misses the handshake).
- "Skipping god object" → user pressing too hard / threshold too high.
- Desync after many commands → we send almost nothing (no obstacles), so
  this shouldn't occur; if it does, power cycle and recalibrate.

## Definition of done

- [ ] 3 levels playable start-to-finish on hardware
- [ ] All feedback works with eyes closed (speech intro, blops, win/death)
- [ ] Monster perceivable via it-handle + spatial audio before it kills you
- [ ] No crash across ≥ 3 consecutive full playthroughs
- [ ] Repo committed incl. submodules; runs from fresh clone via
      `git submodule update --init --recursive`
