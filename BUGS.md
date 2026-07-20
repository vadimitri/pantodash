# Toolkit / setup bugs found while building pantodash

Running log for reporting to the BIS teaching team (bug bounty!). Every
non-obvious failure goes here with repro + suggested fix. Newest last.

## 1. Toolkit breaks on "Input System Package (New)" projects — HIGH, report

- **Where:** `unity-dualpanto-toolkit`: `DualPantoSync.cs:493` (`Input.GetMouseButton`),
  `Vis.cs:20` (`Input.GetKeyDown`), and others using legacy `UnityEngine.Input`.
- **Symptom:** Unity's current templates (e.g. Universal 3D / URP) default
  Player Settings → Active Input Handling to **Input System Package (New)**.
  Every toolkit `Update()` then throws `InvalidOperationException: You are
  trying to read Input using the UnityEngine.Input class...` each frame —
  emulator dead, no handles, no obvious pointer to the cause for a student.
- **Workaround:** Player Settings → Other Settings → Active Input Handling
  → **Both** (requires editor restart).
- **Suggested fix:** document the required setting prominently in the toolkit
  README (or migrate the handful of legacy Input calls to the Input System).

## 2. Emulator cannot "push against" a frozen handle — MEDIUM, report

- **Where:** `PantoHandle.Freeze()` sets `userControlledPosition = false` in
  debug mode; `DualPantoSync.Update()` only applies mouse-drag to
  user-controlled handles. Also `MoveToPosition(..., shouldFreeHandle: false)`
  calls `Freeze()` internally.
- **Symptom:** On hardware, a frozen handle is force-held but the user can
  still physically displace it against the spring (that displacement is
  gameplay input for us). In the emulator a frozen handle ignores the mouse
  entirely — there is no way to simulate pressing against a hold, so any
  game using freeze-and-press cannot be tested without hardware.
- **Workaround:** game-side toggle (`freezeAtNodes`) that skips freezing in
  emulator mode; pass `shouldFreeHandle: !freezeAtNodes` to `MoveToPosition`.
- **Suggested fix:** in debug mode, let the mouse displace a frozen handle
  (e.g. offset while dragged, spring back on release) so emulator behavior
  matches the physical spring-hold.

## 3. Toolkit misses physics2d dependency declaration — LOW, report

- **Where:** `PantoCollider` uses `PolygonCollider2D`, but nothing declares
  `com.unity.modules.physics2d`. New minimal projects fail to compile.
- **Workaround:** add `"com.unity.modules.physics2d": "1.0.0"` to
  `Packages/manifest.json`.

## 4. Toolkit classes live in the global namespace — LOW, report

- **Where:** e.g. `Level`, `SoundManager`, `SceneManager` in
  `Assets/PantoScripts` have no namespace.
- **Symptom:** any student class with the same common name shadows the
  toolkit's and produces confusing compile errors in *toolkit* files.
- **Suggested fix:** put all toolkit scripts in `DualPantoToolkit` (most
  already are — a few generic ones are not).

## 5. Emulator ignores handle speed — handles lag behind tracked objects — MEDIUM, report

- **Where:** `PantoHandle.FixedUpdate()` (PantoHandle.cs:309): in debug mode a
  handle following a GameObject moves `(goal − position) * 0.05` per physics
  tick. The `speed` parameter of `SwitchTo`/`MoveToPosition` is only sent to
  hardware (`pantoSync.SetSpeed`) and never used in the emulator path.
- **Symptom:** In the emulator, a handle tracking a moving object (e.g.
  `SwitchTo(monster)`) trails it by a large, speed-proportional gap (~0.4 ×
  object speed in units at the default 50 Hz physics rate), and
  `MoveToPosition` tweens crawl asymptotically regardless of the speed asked
  for. Hardware behaves correctly, so emulator testing misrepresents timing.
- **Workaround:** shrink `Time.fixedDeltaTime` in emulator mode only (e.g.
  0.005 → 4× tighter chase). Don't do this on hardware — the `FixedUpdate`
  hardware branch sends a serial position update per tick and would flood
  the port.
- **Suggested fix:** integrate the actual speed in the debug branch, e.g.
  `Vector3.MoveTowards(position, goalPos, speed * Time.fixedDeltaTime)`.

## 6. Tween "speed" is undocumented and unintuitive — fraction per second — MEDIUM, report

- **Where:** firmware `Panto::inverseKinematics` (panto.cpp:206-209):
  `m_tweeningValue += 1e-6 * dt_us * m_tweeningSpeed`, i.e. the speed passed to
  `MoveToPosition`/`SwitchTo` is the **fraction of the whole move completed per
  second**, independent of distance. Speed 1 = every move takes exactly 1s;
  speed 30 = 33ms teleport-slam; `MaxMovementSpeed() = 100` suggests a very
  different scale. (`setTarget` also computes `m_tweeningStep = velocity/d`
  from a `[mm/s] maybe?` formula that is then never used.)
- **Symptom:** students pick "reasonable-looking" speeds like 20-50 and get
  violent instantaneous jumps on hardware; the emulator ignores the value
  entirely (bug #5) so it only shows up on the device.
- **Suggested fix:** document the unit, or implement actual mm/s using the
  already-computed `m_tweeningStep`.

## 7. UpdateHandlePosition bounds check uses Unity coords against panto-mm bounds — LOW, report

- **Where:** `DualPantoSync.UpdateHandlePosition` (DualPantoSync.cs:580):
  `IsInBounds(new Vector2(pos.x, pos.z))` is called with the **Unity** position,
  but `pantoBounds` is in panto millimeters; the conversion happens after. Out-
  of-bounds targets are silently sent as NaN (the warning log is commented out).
- **Symptom:** the check passes/fails for the wrong reasons depending on the
  Panto object's scale; genuinely unreachable targets are not caught, and when
  the check does trip, the tween silently never happens — `SwitchTo` then hangs
  3s and "abandons", with no hint why.
- **Suggested fix:** check `UnityToPanto(point)` against the bounds and log a
  warning when rejecting.

## 8. Serial plugin leaks port file descriptors in the Editor — connection dies until restart — HIGH, report

- **Where:** two compounding leaks:
  - `utils/serial/src/serial/unix.cpp` `DPSerial::setup()`: opens the fd, then
    returns false on `tcgetattr`/`tcsetattr` failure **without `close(fd)`**.
  - `DualPantoSync.SetPort()`: `Handle = OpenPort(portName)` overwrites a
    previous non-zero `Handle` without `Close()` — each port-window submit
    leaks a full open handle.
- **Symptom:** the native plugin lives in the Editor process, so leaked fds
  survive Play-mode stops. Once one session leaves a handle behind, every
  subsequent `Open` fails (`[DualPanto] Open failed`), each retry leaks
  another fd, and the device appears permanently disconnected until Unity is
  fully restarted. Observed: `lsof /dev/cu.SLAB_USBtoUART` showed 6 stale
  Unity fds.
- **Diagnosis:** `lsof /dev/cu.SLAB_USBtoUART` — if Unity holds fds while not
  in Play mode, this is it.
- **Workaround:** quit and restart the Unity Editor (not just Play mode).
- **Suggested fix:** `close(fd)` on all error paths in `setup()`; in
  `SetPort()` call `Close(Handle)` first if `Handle != 0`.

## 9. Firmware never sends TRANSITION_ENDED for a zero-distance tween — SwitchTo hangs 3s — MEDIUM, report

- **Where:** `PantoHandle.SwitchTo` sends one tween to the target's current
  position, then busy-waits on `inTransition` until `TweeningEnded()` (fired
  by the TRANSITION_ENDED packet), giving up after 3s. If the target object
  starts at the handle's own position (the natural pattern for
  "attach handle to player object, then move the object"), the tween is
  zero-distance and the firmware never fires TRANSITION_ENDED.
- **Symptom:** `inTransition` stays true for 3s ("Abandoning gameobject that
  couldn't be reached"), and during those 3s the `FixedUpdate` follow branch
  (`!inTransition && !isFrozen`) is gated off — the handle silently ignores
  the object it was just switched to. If the game calls `Freeze()` within
  those 3s, tracking never engages at all. Observed live: me-handle never
  parked, dashes did nothing.
- **Workaround:** call the public `handle.TweeningEnded()` immediately after
  `SwitchTo(obj, speed)` when the object starts at the handle position.
- **Suggested fix:** firmware should acknowledge zero-distance tweens with
  TRANSITION_ENDED (or `SwitchTo` should skip the transition wait when the
  target is already within epsilon of the handle).

## 10. Editor hard-crash (SEGV) in libserial `CppLib::poll()` on second Play after recompile — HIGH, report

- **Where:** native plugin `Assets/Resources/libserial.dylib`, `CppLib::poll()`
  called from `DualPantoSync.Update()` → `Poll` (DualPantoSync.cs:485).
- **Repro:** with the device connected — Play (session works, device syncs),
  Stop, recompile scripts (domain reload), Play again → entire Unity Editor
  segfaults ("Got a segv while executing native code", crash reporter opens).
- **Cause (likely):** the native plugin outlives Play mode and the domain
  reload, keeping serial/session state from run 1; run 2 re-opens the port
  and `poll()` touches the stale state. Related to the fd-handling sloppiness
  in bug #8 but distinct: this is a crash, not a failed open.
- **Workaround:** restart the Unity Editor between device Play sessions after
  any script change (annoying but reliable).
- **Suggested fix:** reset/close all native session state in
  `DualPantoSync.OnDestroy`/`OnApplicationQuit` and guard `poll()` against a
  closed handle.

## 11. PantoHandle.FixedUpdate re-sends a full position tween every physics tick while tracking — serial flood on fast moves — HIGH, report

- **Where:** `PantoHandle.FixedUpdate` (PantoHandle.cs:312-315): while
  `handledGameObject != null && !inTransition && !isFrozen`, it calls
  `UpdateHandlePosition(...)` → `SendMotor` (mode 0, a complete firmware
  position tween) every FixedUpdate, ~50/s, whether or not the target moved.
- **Symptom:** using SwitchTo + moving the object fast (a dash) spams ~50
  tween targets/second — the serial flood that desyncs packet IDs / the god
  object; felt as juddering/oscillation at the handle. Fine only for the slow
  NPC-chase pattern (bis-rogue moves its enemy at 0.2 u/s).
- **Suggested fix:** re-send only when the target moved more than an epsilon
  and/or rate-limit; or expose a "set target once" mode for one-shot tweens.

## 12. SwitchTo's inTransition gate contradicts the FixedUpdate follow mechanism — MEDIUM, report

- **Where:** `SwitchTo` sets `inTransition = true` until TRANSITION_ENDED
  (or 3s timeout); `FixedUpdate`'s follow branch is gated on `!inTransition`.
- **Symptom:** the documented pattern "SwitchTo(obj) then move the object"
  doesn't track on hardware until TRANSITION_ENDED arrives — and never
  arrives for a zero-distance initial tween (bug #9) — so the handle is dead
  for up to 3s after every fire-and-forget SwitchTo. The two mechanisms
  (one-shot transition await vs continuous follow) actively fight each other.
- **Note:** the gate is also the only thing *preventing* the bug-#11 flood
  during the initial tween — fixing either should consider both.

## 13. Firmware TRANSITION_ENDED (0x93) is undocumented — LOW, report

- **Where:** `documentation/protocol/protocol.md:298-300` lists 0x93 with no
  description of when it fires; empirically it does NOT fire for
  zero-distance tweens (bug #9).
- **Suggested fix:** document the exact trigger condition.

## 14. Frozen me-handle physically buzzes — derivative gain amplifies encoder noise — MEDIUM, report

- **Where:** `firmware/src/config/config.cpp` — `pidFactor` for the me-handle
  motors is `{Kp=6, Ki=0, Kd=600}`. `receiveFreeze` (`serial.cpp:476`) pins
  the god object to the current pos and holds it with this PD controller.
- **Symptom:** a frozen handle isn't dead still — the very large Kd multiplies
  encoder-velocity quantization noise into a rendered force, so the handle
  buzzes/jitters around the freeze point. If a game measures "how hard is the
  user pushing" as `|handle − holdPos|` (pantodash's whole mechanic), that
  buzz reads as phantom pushes and can spuriously trigger actions.
- **Workaround (game side):** don't measure press against a single freeze
  sample — average over a settle window for the baseline — and require a
  push to be sustained (temporal debounce) and above the buzz amplitude
  before acting. Done in pantodash DashController.
- **Suggested fix:** low-pass the derivative term, or expose per-game Kd, or
  document that a frozen handle has a noise floor games must filter.

## Not toolkit bugs, but student-facing traps (mention in course docs)

- Unity template pins `com.unity.inputsystem: 1.12.0`, incompatible with
  Unity 6000.3 (`BuildTarget.ReservedCFE` compile error in package code) →
  bump to 1.14.0. Any compile error in any assembly makes Unity report
  "Prefab contains script that does not derive from MonoBehaviour" on the
  Panto prefab — the prefab is fine; check Editor.log for the first `error CS`.
- Gizmos (used by many haptic-debug visualizations) render in the Game view
  only when the Game view's own Gizmos toggle is on.
