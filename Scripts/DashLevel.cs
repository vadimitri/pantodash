using UnityEngine;

namespace PantoDash
{
    // Put this on each level's root object. Nodes, pickups and (optionally) a
    // MonsterController live as children of that root.
    // (Named DashLevel because the toolkit already has a global 'Level' class.)
    public class DashLevel : MonoBehaviour
    {
        public TrackNode startNode;
        [TextArea] public string introText;
    }
}
