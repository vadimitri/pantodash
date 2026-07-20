using System.Threading.Tasks;
using UnityEngine;
using DualPantoToolkit;

namespace PantoDash
{
    // Core pantodash mechanic. The me-handle is force-held ("frozen") at a
    // TrackNode; the user's push against the hold is measured as
    // handle position - node position. Push hard enough, roughly toward a
    // neighboring node, and the device dashes the handle there.
    //
    // TA-verified movement pattern: never drive the handle with one-shot
    // MoveToPosition tweens (TRANSITION_ENDED is unreliable on hardware).
    // Instead THIS object is the player: SwitchTo(gameObject) makes the handle
    // track it, and a dash just moves this transform along the segment —
    // the same pattern MonsterController uses for the it-handle.
    public class DashController : MonoBehaviour
    {
        [Header("Tuning (feel must be tuned on real hardware)")]
        [Tooltip("How far (Unity units) the handle must be pushed off the node to trigger a dash.")]
        public float pressThreshold = 1.0f;
        [Tooltip("Max angle between push direction and a track segment for it to count.")]
        public float maxAngle = 45f;
        [Tooltip("Dash speed of this object in Unity units/second; the handle chases it.")]
        public float dashSpeed = 15f;
        [Tooltip("Tracking speed passed to SwitchTo (same semantics as MonsterController.switchToSpeed).")]
        public float switchToSpeed = 20f;
        [Tooltip("Seconds after arriving during which presses are ignored (debounce).")]
        public float cooldown = 0.4f;

        [Header("Debug")]
        [Tooltip("Disable if Freeze() locks the emulator handle so the mouse can't 'press'.")]
        public bool freezeAtNodes = true;
        public bool logPress = false;

        [HideInInspector] public TrackNode current;

        UpperHandle handle;
        GameManager gm;
        bool dashing;
        float cooldownUntil;

        void Awake()
        {
            handle = GameObject.Find("Panto").GetComponent<UpperHandle>();
            gm = FindFirstObjectByType<GameManager>();
        }

        // Level-1 tutorial: from the current (start) node, dash straight to the
        // node on the right. Calls the real dash directly — no reliance on
        // press-detection — so it definitely moves.
        public void AutoDemoDashRight()
        {
            if (current == null) return;
            TrackNode right = null;
            float bestDx = 0.01f;
            foreach (var n in current.neighbors)
            {
                float dx = n.transform.position.x - current.transform.position.x;
                if (dx > bestDx) { bestDx = dx; right = n; }
            }
            if (right != null) _ = DashTo(right);
        }

        // Called by GameManager on level (re)start.
        public async Task ParkAt(TrackNode node)
        {
            dashing = true; // block input while travelling to the start node
            current = node;
            handle.Free(); // clear any stale freeze so tracking works
            transform.position = node.transform.position;
            await handle.MoveToPosition(node.transform.position, switchToSpeed, true); // tween there, Free after
            Hold();
            dashing = false;
        }

        void Update()
        {
            if (current == null || gm == null || gm.GameOver) return;

            if (dashing)
            {
                gm.CheckPickups(handle.GetPosition());
                return;
            }
            if (Time.time < cooldownUntil) return;

            Vector3 press = handle.GetPosition() - current.transform.position;
            press.y = 0;
            if (logPress && press.magnitude > 0.05f)
                Debug.Log($"press: {press.magnitude:F2} dir: {press.normalized}");
            if (press.magnitude < pressThreshold) return;

            // Pick the outgoing segment best aligned with the push; ignore pushes
            // that don't match any segment within maxAngle (pushing "up" on a
            // horizontal track does nothing).
            TrackNode best = null;
            float bestAngle = maxAngle;
            foreach (var n in current.neighbors)
            {
                Vector3 dir = n.transform.position - current.transform.position;
                dir.y = 0;
                float a = Vector3.Angle(press, dir);
                if (a < bestAngle) { bestAngle = a; best = n; }
            }
            if (best != null) _ = DashTo(best);
        }

        async Task DashTo(TrackNode target)
        {
            dashing = true;
            current = target;
            Vector3 goal = target.transform.position;
            // Position-tween the handle straight to the node, then Free (the
            // shouldFreeHandle:true arg makes MoveToPosition call Free() at the end).
            handle.Free();
            _ = handle.MoveToPosition(goal, switchToSpeed, true);
            // Collect pickups while the handle travels the segment.
            float deadline = Time.time + 3f;
            while (Vector3.Distance(handle.GetPosition(), goal) > pressThreshold * 0.5f
                   && Time.time < deadline)
            {
                if (gm.GameOver) { dashing = false; return; } // killed mid-dash
                gm.CheckPickups(handle.GetPosition());
                await Task.Yield();
            }
            Hold();
            dashing = false;
        }

        void Hold()
        {
            if (freezeAtNodes) handle.Freeze();
            else handle.Free(); // emulator: give the mouse back
            cooldownUntil = Time.time + cooldown; // ponytail: debounce, else residual push re-dashes instantly
        }
    }
}
