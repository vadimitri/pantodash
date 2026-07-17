using UnityEngine;

namespace PantoDash
{
    // A collectible point on a track segment. Put this on a small sphere.
    public class Pickup : MonoBehaviour
    {
        [HideInInspector] public bool collected;

        public void Collect()
        {
            collected = true;
            GetComponent<Renderer>().enabled = false;
        }

        public void ResetPickup()
        {
            collected = false;
            GetComponent<Renderer>().enabled = true;
        }
    }
}
