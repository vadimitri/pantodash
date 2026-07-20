using System.Threading.Tasks;
using UnityEngine;
using DualPantoToolkit;

namespace PantoDash
{
    // Room-trigger mechanic (bis-rogue style). The me-handle is FREE — the
    // player physically moves it around the current node ("room"). Each node
    // has an invisible ring of radius triggerRadius; pushing the handle across
    // that ring toward a neighbor is the N/S/E/W "door" and dashes the handle
    // to that neighbor. No Freeze() → no firmware buzz to fight (BUGS.md #14).
    //
    // Dash uses the TA pattern: SwitchTo(gameObject) so the handle tracks THIS
    // object, then move the transform along the segment; Free() on arrival to
    // hand control back to the player.
    public class DashController : MonoBehaviour
    {
        [Header("Tuning")]
        [Tooltip("How far (Unity units) the handle must leave the node to cross a door and dash.")]
        public float triggerRadius = 2.0f;
        [Tooltip("Max angle between the handle's exit direction and a segment for that door to count.")]
        public float maxAngle = 60f;
        [Tooltip("Dash speed of this object in Unity units/second; the handle chases it.")]
        public float dashSpeed = 15f;
        [Tooltip("Tracking speed passed to SwitchTo (same semantics as MonsterController.switchToSpeed).")]
        public float switchToSpeed = 20f;
        [Tooltip("Seconds after arriving during which the handle must return inside the ring before it can dash again.")]
        public float cooldown = 0.3f;

        [Header("Debug")]
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

        // Called by GameManager on level (re)start: pull the handle to the start node, then free it.
        public async Task ParkAt(TrackNode node)
        {
            dashing = true;
            current = node;
            // MoveToPosition = one firmware tween to the target, then Free(). This is the
            // API that actually moves the handle on hardware; the per-frame SwitchTo drive
            // never reaches the motor because inTransition gates FixedUpdate's re-send.
            await handle.MoveToPosition(node.transform.position, switchToSpeed, shouldFreeHandle: true);
            cooldownUntil = Time.time + cooldown;
            dashing = false;
        }

        void Update()
        {
            if (current == null || gm == null || gm.GameOver) return;

            gm.CheckPickups(handle.GetPosition());
            if (dashing || Time.time < cooldownUntil) return;

            Vector3 exit = handle.GetPosition() - current.transform.position;
            exit.y = 0;
            if (logPress && exit.magnitude > 0.05f)
                Debug.Log($"exit: {exit.magnitude:F2} dir: {exit.normalized}");
            if (exit.magnitude < triggerRadius) return;

            // Which door did we cross? Neighbor best aligned with the exit direction.
            TrackNode best = null;
            float bestAngle = maxAngle;
            foreach (var n in current.neighbors)
            {
                Vector3 dir = n.transform.position - current.transform.position;
                dir.y = 0;
                float a = Vector3.Angle(exit, dir);
                if (a < bestAngle) { bestAngle = a; best = n; }
            }
            if (best != null) _ = DashTo(best);
        }

        async Task DashTo(TrackNode target)
        {
            dashing = true;
            current = target;
            // One firmware tween moves the handle to the next room, then frees it so the
            // player can roam. shouldFreeHandle:true => Free() at the end.
            await handle.MoveToPosition(target.transform.position, switchToSpeed, shouldFreeHandle: true);
            cooldownUntil = Time.time + cooldown;
            dashing = false;
        }
    }
}
