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

- `pantodash/` — THIS project, and since 2026-07-17 **its own git repo**, public
  at `github.com/vadimitri/pantodash` (branch `main`). It is nested inside the
  private `BIS` repo but independent of it — commit and push from
  `pantodash/`, never from `BIS`. Contains: `Scripts/` (game code, copy into Unity),
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
- **Coordinate mapping (verified in DualPantoSync.PantoToUnity): `unity =
  device_mm/10 × PantoRoot.localScale + PantoRoot.position`. NEVER scale or
  move the Panto root** — it silently redefines the whole workspace. At the
  prefab default (scale 1, pos 0): **1 Unity unit = 1 cm physical**, workspace
  = x∈[−18,18], z∈[−20.5,+0.5] (pantoBounds "ember": 360×210mm centered
  (0,−100)). Emulator and hardware use the SAME coordinates.
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
3. **Me-handle is command-silent (rewritten 2026-07-19 per archived/HANDLING.md).**
   PARKED = Freeze + no handledGameObject → zero serial traffic; press measured
   from settled `holdPos`. DASH = snap Player transform to target, ONE
   fire-and-forget `SwitchTo(gameObject, dashSpeed)` (inTransition staying true
   deliberately gates OFF PantoHandle.FixedUpdate's 50 Hz re-send = BUGS.md
   #11 flood), poll `GetPosition()` for arrival (never depends on
   TRANSITION_ENDED), then Free+TweeningEnded+Freeze+settle. `dashSpeed` is
   the firmware 0x92 speed = fraction-of-move/second (3 ≈ 0.33 s dash);
   pickups = per-frame distance checks while dashing (no Unity physics).
4. Monster drives a GameObject along the graph (greedy chase);
   `LowerHandle.SwitchTo(monster)` makes the it-handle track it physically.
5. Fresh repo base; toolkit + SpeechIO as submodules. Nothing copied from
   bis-rogue (its game code is dungeon machinery; only its *patterns* were
   used: SwitchTo-driven enemy, speech intro, MoveToPosition haptics).
6. **Levels are authored in real device coordinates (1 unit = 1 cm) directly
   in the scene; there is NO runtime scaling.** ApplyHardwareFit and all
   hardware* tuning fields were deleted 2026-07-17 — they only ever
   compensated for a wrong 3.1/3.8 scale on the Panto root (now reverted).
   Want a bigger track? Move the nodes in the scene, nothing else.

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
- 2026-07-17 (repo): `pantodash/` is now a standalone **public** repo
  (`vadimitri/pantodash`, branch `main`) so a friend can collaborate. Unity Hub's
  throwaway repo (`pantoDashUnity-2026-07-16_21-52-01`, private, 1 boilerplate
  commit) is abandoned — its `.git` was deleted and the toolkit re-added as a
  submodule at `pantoDashUnity/Assets/unity-dualpanto-toolkit` (https URL, branch
  `develop`, pin 5e6d293); SpeechIO stays nested inside it. Gotchas that bit and
  are now fixed: Unity's `.gitattributes` **macros only work in the repo-root
  file**, so they moved to `pantodash/.gitattributes` (else `unityyamlmerge` on
  scenes silently stops applying — matters with two people editing scenes);
  `[attr]lfs` is deliberately left undefined (no large binaries; defining it
  marks `*.png`/`*.fbx` as LFS in a repo with no LFS objects → clone warnings).
  `pantoDashUnity/.gitignore` keeps working in place because leading-slash rules
  anchor to their own directory, so `Library/` (2.9 GB) stays excluded.
  Verified by fresh `git clone --recursive`.
- 2026-07-17 (scale rethink): The "workspace = x±56, z−78..2 (Panto prefab
  scale 3.1/3.8)" finding was an ARTIFACT — someone had copied the
  EmberWorkingArea child's 3.1/3.8 scale onto the Panto ROOT, which rescales
  the device↔Unity mapping itself (see Toolkit API section). Root reverted to
  scale 1 → workspace is x∈[−18,18], z∈[−20.5,+0.5], 1 unit = 1 cm, and the
  authored levels (x±5, z−13..−7 ≈ 10×6 cm patch, centered) fit as-is.
  Deleted ApplyHardwareFit + hardwareScale/ZShift/DashSpeed/PressThreshold
  from GameManager (design decision 6); existing DashController defaults are
  already physical: pressThreshold 1 = 1 cm push, dashSpeed 15 = 15 cm/s.
  Camera fixed in the SCENE (instance override, not the prefab): (0,10,−10),
  ortho 12, cullingMask Everything — nodes/corridors/handles visible in play
  in both modes; `b` still cycles blind views. TrackNode.Start now also
  spawns corridor bars (thin stretched cubes to each neighbor, lower-ID node
  draws) alongside the node spheres (bumped to 0.8). Compile verified clean
  via unity-mcp.
- 2026-07-17 (hardware debug, live via unity-mcp): "park never happens /
  dash doesn't dash / oscillates at node" root-caused from console logs:
  `SwitchTo(Player)` tween is ZERO-distance (object starts at handle pos) and
  firmware never fires TRANSITION_ENDED for a no-op move → `inTransition`
  stuck true 3s ("Abandoning gameobject: Player") → PantoHandle.FixedUpdate
  chase (gated on `!inTransition && !isFrozen`) never engaged; our Freeze()
  landed within the 3s and gated it off permanently. Fix: DashController.
  MoveAlongTrack calls public `handle.TweeningEnded()` right after SwitchTo
  (BUGS.md #9). NOT yet verified on device — Editor segfaulted on the retest
  run: native crash in libserial `CppLib::poll()` on the SECOND Play after a
  recompile (BUGS.md #10). **New pitfall: with the device attached, restart
  the Unity Editor between Play sessions whenever scripts recompiled** —
  the native plugin's stale serial state segfaults the whole Editor.
- 2026-07-19: DashController REWRITTEN per `archived/HANDLING.md` research
  (design decision 3 updated — read it). Old TweeningEnded-hack + per-frame
  MoveTowards drive (a 50 Hz tween flood, the oscillation source) deleted;
  now: park = Freeze + silence, dash = one firmware tween + arrival poll +
  settle(120 ms) before sampling holdPos, edge-trigger re-arm (press must
  relax below 0.6×threshold before next dash; kills chain-dashing).
  `switchToSpeed` field removed; `dashSpeed` is now the firmware
  fraction-per-second speed, default/scene value 3 (was 30 = 66 ms slam).
  Scene YAML updated by hand (dashSpeed 3, settleMs 120, maxDashSeconds 2).
  MonsterController: `TweeningEnded()` right after its fire-and-forget
  SwitchTo (kills the 3 s dead it-handle). BUGS.md #11 (FixedUpdate re-send
  flood), #12 (inTransition vs follow contradiction), #13 (0x93 undocumented)
  recorded. Compile verified via unity-mcp (only known cosmetic
  nsspeechforunity duplicate-plugin errors). NOT yet run on device.
  Note: `Assets/_Recovery/` holds crash-recovered scene copies from today's
  segfaults (BUGS.md #10) — ignore/delete once the scene is confirmed good.
- 2026-07-19 (whole-stack pass on the oscillation/broken-dash/steppy-monster
  report; HANDLING.md no longer exists — worked from firmware + toolkit
  source directly). Root causes traced through firmware:
  (a) **Oscillation = the frozen hold's own physical BUZZ.** Firmware
  `config.cpp` me-handle motors are `pidFactor {Kp=6, Kd=600}`; the huge
  derivative gain turns encoder-velocity noise into force → a stiff buzz
  around the freeze point. We send nothing while PARKED, so this is firmware
  config on shared hardware — NOT ours to damp. Logged BUGS.md #14. Fix is to
  make the DASH TRIGGER immune: DashController now (1) raises pressThreshold
  1→1.5 cm (above buzz floor), (2) adds a **dwell** (`dwellMs=90`: the same
  aligned over-threshold push must persist before firing — a buzz spike is
  brief / flips segment, a real push is held) — THIS is what stops the buzz
  from dashing, (3) captures holdPos as an **average** over the settle window
  (single sample caught the buzz at a random phase → biased baseline → phantom
  press). BestSegment extracted from Update.
  (b) **Broken/laggy dash = arrival never detected.** Old poll exited only on
  `dist ≤ eps` with eps≈0.2–0.5 cm, tighter than firmware settle precision →
  most dashes ran to the 2 s maxDashSeconds timeout. New `PollArrival`: eps
  0.8 cm + a **stall detector** (handle stopped moving 150 ms = arrived/stuck)
  → dashes end promptly. Also dropped the `Free()`→`Freeze()` release-regrab at
  arrival (limp window let the hold land off-baseline); now Freeze directly
  (Freeze then TweeningEnded; isFrozen gates the re-send). The single motor
  release now happens only at dash START (`handle.Free()` before SwitchTo).
  (c) **Steppy it-handle monster = switchToSpeed 20.** The follow is
  PantoHandle.FixedUpdate's 50 Hz position re-send moving the motor at the
  SetSpeed SwitchTo set once; 20 lags the moving monster. bis-rogue uses 100
  for its follow handle → bumped to 100. AI (greedy chase) left as-is; at
  figure-8 junctions it can still thrash between neighbors (possible
  follow-up, not touched).
  Scene YAML patched (serialized values override C# defaults): pressThreshold
  1.5, dwellMs 90, settleMs 200, switchToSpeed 100. Compile clean via
  unity-mcp, 0 console errors.
- 2026-07-19 (device run #1 of the above — two regressions + a re-diagnosis):
  (i) The `PollArrival` **stall detector I added broke the initial park**: at
  game start the handle is stationary until the firmware starts moving it, so
  "not moving 150 ms = arrived" false-fired immediately and froze at the REST
  position → "didn't go to the start node itself", worse on farther level-2
  nodes → "dash didn't work in level 2". REMOVED the stall detector;
  PollArrival is now just eps 0.8 cm + maxDashSeconds cap.
  (ii) Re-diagnosis of the "oscillation": it's SLOW and self-dashes, but an
  overdamped hold (Kd 600 ≫ Kp 6) can't physically oscillate slowly — so the
  "oscillation" is really a **self-dash LOOP** (dash to neighbor → re-baseline
  slightly off → dash back, ~1 s/cycle). A static threshold+dwell can't stop
  it; dwell only rejects FAST spikes. Fix: **auto-recentering baseline
  deadband** in Update — while |press| < 0.4×threshold, slide holdPos toward
  the handle (`recenterRate`=4/s, hardware only) so drift/slow-oscillation is
  absorbed and can never accumulate into a phantom press; reaching the deadband
  is also the re-arm condition, so after a dash the handle must go quiet near
  the new node before it can dash again (breaks the loop). A deliberate push
  leaves the deadband fast → baseline freezes → press builds → dwell → dash.
  Compile clean, 0 errors.
- 2026-07-19 (emulator run — found the "can't move the handle in level 2 at
  all" bug, PRE-EXISTING in the level flow): `GameManager.Win()` sets
  `GameOver=true`, then `StartLevel(next)` does `await player.ParkAt(...)`
  while GameOver is STILL true (it's cleared only AFTER the park, line 49→50).
  Old `DashTo` did `SwitchTo` (→ handle tracking; in the emulator that sets
  userControlledPosition=false so the mouse can't drag it) and then
  `if (gm.GameOver) return;` SKIPPED `Hold()` — the only place that calls
  `Free()` to hand control back. So level 2's handle was left frozen to the
  Player object: unmovable AND un-dashable. Level 1 worked only because its
  first park runs before any win. Fix in DashController (NOT GameManager):
  `Hold()` now ALWAYS runs (never leave the handle tracking/half-frozen), and
  `DashTo(node, park)` — a park ignores GameOver and skips CheckPickups (so it
  finishes during a level (re)start and doesn't vacuum pickups on the way to
  the start node); a gameplay dash still aborts on death/win. Merged the old
  PollArrival back into DashTo. Also dropped pressThreshold 1.5→1.0 (1.5 made
  the emulator drag-to-dash fire far too late — you could almost drag all the
  way to the next node; the auto-recenter+dwell now carry hardware robustness
  so the threshold can stay small). Scene YAML pressThreshold→1. Compile
  clean, 0 errors. NOT yet re-run.
- 2026-07-20 (emulator regression → reverted DashController): the whole
  07-19 buzz-fighting rework (recenter/dwell/averaged-holdPos/arming, all
  NEVER verified on device) broke debug/emulator mode. Decision (Vadim):
  restore the emulator-verified guide-style DashController, keep the clean
  no-scaling architecture. **Git set up properly now**: the buzz rework is
  committed on branch **`hardware-experiment`** (recoverable when back at the
  device); `main` (commit `de6de30`) has the guide-style two-state
  DashController (TA SwitchTo + per-frame MoveTowards drive; fields:
  pressThreshold/maxAngle/dashSpeed[Unity u/s]/switchToSpeed/cooldown/
  freezeAtNodes/logPress) paired with the clean GameManager (ApplyHardwareFit
  stays deleted, 1 unit = 1 cm) + TrackNode corridor bars + MonsterController
  TweeningEnded/switchToSpeed100. Scene DashController reconciled to the guide
  fields: **freezeAtNodes 0** (untick = emulator), dashSpeed 15, switchToSpeed
  20 (buzz-only fields dwellMs/recenterRate/settleMs/maxDashSeconds removed).
  Not re-run since the edit; guide DashController compiled clean at HEAD, only
  the scene scalar edit is new. `_Recovery/` crash copies still on disk
  (untracked) — delete once the scene is confirmed good.
- **NEXT (start here):** run the EMULATOR (DualPantoSync.debug on, Panto
  object). freezeAtNodes is already 0 in the scene. Verify: parks at start
  node, LEFT-drag the me-handle past pressThreshold toward a neighbor → dashes
  one segment, pickups blop, win advances to level 2 and the handle is movable
  there (the pre-existing level-2 lock is fixed in this DashController's
  always-run Hold). Then the HARDWARE path is the `hardware-experiment` branch
  (tick freezeAtNodes); tune recenterRate/pressThreshold/dwellMs there when at
  the device. Then enlarge levels by moving nodes (track is 10×6 cm of
  36×21 cm workspace), then Levels 2+3. logPress is ON — turn off once tuned.

Update the State section when you finish or learn something; keep the rest
stable.
