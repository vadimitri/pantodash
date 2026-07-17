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
            // toggle; a tiny primitive sphere makes nodes visible everywhere.
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(marker.GetComponent<Collider>());
            marker.transform.SetParent(transform, false);
            marker.transform.localScale = Vector3.one * 0.4f;
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
