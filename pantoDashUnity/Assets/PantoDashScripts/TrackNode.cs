using System.Collections.Generic;
using UnityEngine;

namespace PantoDash
{
    // A bifurcation/endpoint of the track graph. Levels are just linked TrackNodes.
    public class TrackNode : MonoBehaviour
    {
        public List<TrackNode> neighbors = new List<TrackNode>();

        void Start()
        {
            // ponytail: gizmos never render in play mode without the toolbar
            // toggle; primitives make nodes AND corridors visible everywhere.
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(marker.GetComponent<Collider>());
            marker.transform.SetParent(transform, false);
            marker.transform.localScale = Vector3.one * 0.8f;

            foreach (var n in neighbors)
            {
                if (n == null) continue;
                // corridors are bidirectional; let only the lower-ID node draw the bar
                if (n.neighbors.Contains(this) && n.GetInstanceID() < GetInstanceID()) continue;
                Vector3 a = transform.position, b = n.transform.position;
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(bar.GetComponent<Collider>());
                bar.transform.SetParent(transform, true);
                bar.transform.position = (a + b) * 0.5f;
                bar.transform.rotation = Quaternion.LookRotation(b - a);
                bar.transform.localScale = new Vector3(0.15f, 0.05f, (b - a).magnitude);
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(transform.position, 0.15f);
            foreach (var n in neighbors)
                if (n != null) Gizmos.DrawLine(transform.position, n.transform.position);
        }
    }
}
