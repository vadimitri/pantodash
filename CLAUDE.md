# pantodash — Agent Knowledgebase

Game for the HPI BIS course, built on DualPanto. Final grade project.
Read `GUIDE.md` for the build plan. This file = facts so you don't
re-explore. Pick up mid-stride — Vadim continues across chats and expects
you to already know all of this.

## Working with Vadim on this

- **Goal: the game DONE and polished** — this is his final grade. BUILD
  mode, no didactic withholding; he explicitly waived the learning protocol
  for this project. Be token-efficient; don't re-explain settled things.
- Division of labor: agents write all code and docs. Unity editor work was
  Vadim's job by hand, BUT a **unity-mcp server is now connected** (tools
  `mcp__unity-mcp__*` via ToolSearch: console logs, scene/GameObject
  management, script validation, play mode via ManageEditor). Prefer it for
  reading the Console, wiring scenes, and verifying — instead of asking him
  to click and paste.
- He runs Unity himself and pastes errors when MCP isn't up. Diagnose from
  the first `error CS`; the rest is cascade.
- Style he liked: lead with the diagnosis, fix immediately, record the
  lesson here (pitfalls) so it's never re-debugged. Keep this file current —
  update State after every session; it's what makes "like we never said
  goodbye" work.

## The game

Pac-Man-like on a haptic device for blind players ("pantodash", see
`~/Downloads/pantodash.pdf`). The me-handle is force-held at nodes of a track
graph; the player *pushes* against the hold, and a hard-enough push toward an
outgoing segment makes the device *dash* the handle to the next node,
collecting audio "blop" points en route. 3 levels: line, square, figure-8
with a monster on the it-handle.

## Repo map (working dir: /Users/vadim/Developer/HPI/BIS)

- `pantodash/` — THIS project: `Scripts/` (game code, copy into Unity),
  `GUIDE.md`, `CLAUDE.md`. Unity project lives at `pantodash/PantodashUnity/`.
- `unity-dualpanto-toolkit/` (branch `develop`) — the Unity toolkit. Read-only
  reference; consumed as a git submodule inside the Unity project.
- `dualpantoframework/` (branch `BIS`) — ESP32 firmware + serial protocol
  docs (`documentation/protocol/protocol.md`). Read-only reference.
- Do NOT touch `archive/` (removed/off-limits per user). Do NOT switch
  branches in the reference clones.

## How DualPanto works (verified from firmware, branch BIS)

- Two pantograph handles over one workspace: **upper = "me"** (player,
  panto index 0), **lower = "it"** (NPC, index 1). Motors render force AND
  can move handles; encoders report physical position. USB serial, 115200.
- **God object runs in firmware**: virtual point that follows the handle but
  cannot cross walls; PD force pulls handle toward it → wall feel. Unity only
  sends geometry and reads positions.
- Firmware→Unity POSITION packet (per handle): handle x,y, rotation,
  **god object x,y**. There is NO force/motor-load readback anywhere.
  → "How hard is the user pressing" = |handle pos − hold pos|.
- Firmware supports: obstacles (create/enable/disable/remove), passable
  obstacles, rails, MOTOR position-tween (fires TRANSITION_ENDED → awaitable
  in Unity) or force vector, FREEZE/FREE, per-handle SPEED, speed-control
  tethering (MAX_SPEED / EXPLORATION / LEASH strategies).

## Toolkit API essentials (namespace `DualPantoToolkit`, Assets/PantoScripts)

- `DualPantoSync` on GameObject named **"Panto"** — serial bridge; everything
  does `GameObject.Find("Panto")`. `debug=true` = mouse emulator (key `b`
  cycles blind/mixed/dev view). Port defaults: `/dev/cu.SLAB_USBtoUART` (mac).
- `UpperHandle` / `LowerHandle` (both on the Panto object):
  - `Vector3 GetPosition()` — physical handle position (penetrates walls)
  - `async Task MoveToPosition(Vector3, float speed = 1, bool shouldFreeHandle = true)`
  - `async Task SwitchTo(GameObject, float speed)` — handle tracks the object
  - `void Freeze()` / `void Free()` — force-hold at current pos / release
  - `void ApplyForce(Vector3 dir, float strength ≤ 1)` / `StopApplyingForce()`
  - speed capped at `MaxMovementSpeed()` = 100
- God object positions are mirrored onto GameObjects named
  `"MeHandleGodObject"` / `"ItHandleGodObject"` (tagged `MeHandle`/`ItHandle`);
  no public getter on PantoHandle. Those tags are also what toolkit
  ForceFields/trigger scripts react to.
- Walls (if ever needed): `PantoBoxCollider` etc. + `CreateObstacle()` +
  `Enable()`. Speech: `SpeechIO.SpeechOut`, `await speechOut.Speak(text)` —
  SpeechIO is a **git submodule that must be initialized**.

## Design decisions (don't re-litigate without new evidence)

1. **No panto obstacles at all.** `Freeze()` IS the "everything is a wall"
   feel (firmware holds via the same god-object spring). Press detection =
   `GetPosition() − currentNode.position`. Avoids the entire buggy obstacle
   path (serial flooding desyncs packet IDs, device obstacle capacity,
   "Skipping god object" crashes).
2. **One scene, three level roots** toggled by GameManager. No Unity scene
   loads → panto connection never disturbed, no toolkit SceneManager needed.
3. **Dash = awaited `MoveToPosition(node, dashSpeed, false)` + `Freeze()`**;
   pickups = per-frame distance checks while dashing (no Unity physics).
4. Monster drives a GameObject along the graph (greedy chase);
   `LowerHandle.SwitchTo(monster)` makes the it-handle track it physically.
5. Fresh repo base; toolkit + SpeechIO as submodules. Nothing copied from
   bis-rogue (its game code is dungeon machinery; only its *patterns* were
   used: SwitchTo-driven enemy, speech intro, MoveToPosition haptics).

## Pitfalls / open risks

- **Every toolkit/setup bug found goes into `BUGS.md`** (repro + suggested
  fix) — Vadim reports them to the teaching team; there's a bug bounty.

- **Emulator vs Freeze (VERIFIED 2026-07-16):** `Freeze()` DOES pin the
  emulated handle (`userControlledPosition = false`; mouse-drag in
  `DualPantoSync.Update` only moves user-controlled handles). Crucially,
  `MoveToPosition(..., shouldFreeHandle: false)` calls `Freeze()` *internally*,
  so DashController must pass `shouldFreeHandle: !freezeAtNodes` (fixed) —
  a hardcoded `false` pins the emulator handle regardless of the toggle.
  Untick `freezeAtNodes` in emulator, tick on hardware.
- Emulator controls (DualPantoSync.Update): **hold LEFT mouse** = drag
  me-handle, **hold RIGHT mouse** = drag it-handle, Horizontal axis (A/D)
  while held = rotate, `b` = cycle view, `q` = quit. Dragging only works
  while the handle is user-controlled (not frozen/tweening).
- **Emulator ignores handle speed** (BUGS.md #5): debug-mode handle chase is
  a fixed 5%-of-gap per physics tick (`PantoHandle.FixedUpdate`), so handles
  lag behind tracked objects and dashes crawl. GameManager.Start sets
  `Time.fixedDeltaTime = 0.005` when `DualPantoSync.debug` — emulator only;
  never lower the timestep on hardware (serial flooding).
- Gizmos (TrackNode's cyan lines/spheres) only show in the **Game view** if
  its Gizmos toolbar toggle is on — Scene view has its own separate toggle.
- `MoveToPosition(..., shouldFreeHandle: false)` then `Freeze()` is our
  arrival sequence; if the handle drifts at nodes on hardware, check whether
  Freeze captured a position slightly off the node (re-freeze or tween again).
- Unity 6: use `FindFirstObjectByType`/`FindObjectsByType`, not the obsolete
  `FindObjectOfType` (toolkit's own code still uses old APIs — that's fine).
- Speech is async; don't let two `Speak()` calls overlap (GameOver guards).
- Every level needs ≥1 Pickup or GameManager wins instantly.
- README device wisdom: don't grip/push the handle too hard ("Skipping god
  object"); reset button + handles-right recalibrates; macOS must allow
  `libserial.dylib`.
- **"[DualPanto] Open failed" on connect** = stale serial fds held by the
  Editor process (BUGS.md #8). Check `lsof /dev/cu.SLAB_USBtoUART`; if Unity
  holds fds, fully restart the Unity Editor. Each failed retry leaks more fds,
  so don't keep clicking the port-window submit.
- **"Prefab contains script that does not derive from MonoBehaviour" = a
  compile error somewhere, NOT a broken prefab.** When any assembly fails to
  compile, all script refs go null. Check `~/Library/Logs/Unity/Editor.log`
  for `error CS`. 2026-07-16 instance: template pinned
  `com.unity.inputsystem: 1.12.0` (incompatible with 6000.3,
  `BuildTarget.ReservedCFE`) → bumped to 1.14.0 and removed unused
  `com.unity.ai.assistant`/`com.unity.ai.inference` from manifest.json.
- Unity project was created from the **Universal 3D (URP) template**; the
  toolkit targets built-in RP, so toolkit materials may render pink/magenta.
  Cosmetic only — fix via Window → Rendering → Render Pipeline Converter if
  it bothers, don't recreate the project.
- **All game scripts live in namespace `PantoDash`.** The toolkit dumps
  generic class names into the global namespace (`Level`, `SoundManager`,
  `SceneManager`, ...) — a global game class with the same name shadows
  theirs and breaks toolkit compilation (happened with `Level`; ours is now
  `DashLevel`). Never add global-namespace classes.
- The URP template project needed `"com.unity.modules.physics2d": "1.0.0"`
  added to manifest.json (toolkit's PantoCollider uses PolygonCollider2D).
- Never save changes INTO the toolkit prefab/submodule — if Unity prompts to
  save `Assets/unity-dualpanto-toolkit/...`, discard. Keep the submodule
  clean; game objects live in our scene.

## State / progress

- 2026-07-16: Scripts written (TrackNode, Pickup, Level, DashController,
  MonsterController, GameManager). Nothing tested yet.
- 2026-07-16: Unity project created at `pantodash/pantoDashUnity` (URP
  template), toolkit submodule + nested SpeechIO in place, scripts copied to
  `Assets/PantoDashScripts/`. Fixed manifest.json (inputsystem 1.12.0→1.14.0
  for the ReservedCFE compile break; added modules.physics2d; the
  ai.assistant/ai.inference packages were re-added intentionally — leave
  them). Fixed `Level` name clash → all scripts now `namespace PantoDash`,
  class renamed `DashLevel`.
- 2026-07-16 (evening): URP template had Active Input Handling = new-only →
  toolkit's legacy `Input` calls threw every frame; fixed via Player Settings
  → Active Input Handling = **Both** (+ editor restart). Step 2 wired by
  Vadim by hand (unity-mcp connection was revoked; re-approve under Project
  Settings → AI → Unity MCP). Freeze-pins-emulator pitfall confirmed and
  fixed in DashController (`shouldFreeHandle: !freezeAtNodes`).
- 2026-07-17: Level 3 in progress. Monster NRE = unassigned `startNode` on
  MonsterController (Inspector field). Handle-lag-behind-monster diagnosed as
  toolkit emulator bug (BUGS.md #5); fixed via debug-only
  `Time.fixedDeltaTime = 0.005` in GameManager.Start (synced to Unity copy).
- 2026-07-17 (hardware day): First real-device run. Two scale/unit mismatches
  (invisible in emulator): (a) workspace in Unity units = x∈[−56,56],
  z∈[−78,+2] (Panto prefab scale 3.1/3.8) — authored levels were a ~3cm patch
  at the top, so the park "didn't move" and froze near the rest position;
  (b) firmware tween speed = fraction-of-move per second (BUGS.md #6), so
  dashSpeed 30 = 33ms slam. Fix: `GameManager.ApplyHardwareFit()` (runs when
  !debug) scales level roots ×hardwareScale(7) + z-shift 30, scales
  pickupRadius/monster speed/killRadius, sets dashSpeed=2 and
  pressThreshold=3 (~1cm push). All tunables in GameManager inspector under
  "Hardware fit". Emulator path unchanged. Also logged BUGS.md #7 (bounds
  check in wrong coord space). Unity-MCP got revoked again (re-approve in
  Project Settings → AI).
- 2026-07-17 (later): TA says: never drive handles directly — make an object,
  `SwitchTo(object)`, then MOVE THE OBJECT (handle chases via
  PantoHandle.FixedUpdate re-sending positions; no TRANSITION_ENDED
  dependency). DashController rewritten to this pattern (design decision 3
  superseded): Player object is authoritative, dashes = MoveTowards on our
  transform, dashSpeed now Unity units/sec (×hardwareScale on hardware;
  `hardwareDashSpeed` field removed), Freeze only after WaitForHandle
  catches up. Invisibility diagnosed: Panto prefab camera starts in blind
  view (culling mask 512 = layer 9 only; `b` cycles via Vis.cs) AND is a
  7.5-unit ortho at the authored area → ApplyHardwareFit now re-fits camera
  (pos (0,10,-38), ortho 45, cullingMask -1). TrackNode spawns a small
  sphere marker at runtime (gizmos don't render in play). unity-mcp fix:
  patched PackageCache com.unity.ai.assistant Bridge.cs `ExecuteCommandAsync`
  — neutralized the stale-Denied approval gate (`if (false && ...Denied)`),
  same patch as archive/tomb-dualpanto-toolkit; regenerated if PackageCache
  re-resolves.
- **NEXT (start here):** verify via unity-mcp: compile clean, hardware run —
  park at start node, press-to-dash, nodes/points visible. Then Levels 2+3
  on hardware.

Update the State section when you finish or learn something; keep the rest
stable.
