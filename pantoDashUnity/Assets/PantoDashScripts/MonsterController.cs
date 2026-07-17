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
        public float switchToSpeed = 20f;

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
