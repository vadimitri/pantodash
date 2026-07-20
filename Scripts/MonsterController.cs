using UnityEngine;
using DualPantoToolkit;

namespace PantoDash
{
    // Level 3 monster. Walks the same TrackNode graph, greedily chasing the
    // player. The physical it-handle tracks it via LowerHandle.SwitchTo(), so the
    // player can feel where the monster is with their second hand.
    // Add a looping AudioSource (spatial blend = 1) to this object for audio cues.
    public class MonsterController : MonoBehaviour
    {
        public TrackNode startNode;
        public float speed = 3f;
        public float killRadius = 0.8f;
        // The it-handle follows this object via PantoHandle.FixedUpdate's 50 Hz
        // position re-send, which moves the firmware motor at whatever SetSpeed
        // SwitchTo last set. Too low = the handle lags the moving monster =
        // "stepping". bis-rogue uses the max (100) for exactly this follow role.
        public float switchToSpeed = 100f;

        LowerHandle itHandle;
        DashController player;
        GameManager gm;
        TrackNode target;

        void OnEnable()
        {
            itHandle = GameObject.Find("Panto").GetComponent<LowerHandle>();
            player = FindFirstObjectByType<DashController>();
            gm = FindFirstObjectByType<GameManager>();
            transform.position = startNode.transform.position;
            target = startNode;
            _ = itHandle.SwitchTo(gameObject, switchToSpeed);
            // Un-gate the FixedUpdate slow-chase immediately: fire-and-forget
            // SwitchTo leaves inTransition true (up to 3s if TRANSITION_ENDED
            // doesn't fire), during which the it-handle would be dead (BUGS.md #9).
            itHandle.TweeningEnded();
        }

        void OnDisable()
        {
            if (itHandle != null) itHandle.Free();
        }

        void Update()
        {
            if (gm == null || gm.GameOver) return;

            Vector3 to = target.transform.position - transform.position;
            to.y = 0;
            if (to.magnitude < 0.05f)
            {
                // At a node: pick the neighbor closest to the player (greedy chase).
                TrackNode best = target;
                float bestDist = float.MaxValue;
                foreach (var n in target.neighbors)
                {
                    float d = Vector3.Distance(n.transform.position, player.transform.position);
                    if (d < bestDist) { bestDist = d; best = n; }
                }
                target = best;
            }
            else
            {
                transform.position += to.normalized * Mathf.Min(speed * Time.deltaTime, to.magnitude);
            }

            if (Vector3.Distance(transform.position, player.transform.position) < killRadius)
                _ = gm.Die();
        }
    }
}
