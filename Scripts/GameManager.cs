using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using DualPantoToolkit;
using SpeechIO;

namespace PantoDash
{
    // Level flow: intro speech -> play -> win speech -> next level (or restart on
    // death). All three levels live in ONE scene as inactive root objects; no
    // scene loading, so the panto connection is never disturbed.
    public class GameManager : MonoBehaviour
    {
        [Tooltip("Level root objects in play order (each has a DashLevel component).")]
        public DashLevel[] levels;
        public AudioClip blopClip;
        public float pickupRadius = 0.6f;

        public bool GameOver { get; private set; }

        DashController player;
        SpeechOut speech;
        Pickup[] pickups = new Pickup[0];
        int levelIndex = -1;

        async void Start()
        {
            player = FindFirstObjectByType<DashController>();
            speech = new SpeechOut();
            // ponytail: emulator ignores handle speed and chases targets at a fixed
            // 5% of remaining distance per physics tick (PantoHandle.FixedUpdate) —
            // faster ticks = tighter chase. Hardware honors real speed; keep 0.02 there
            // (more ticks would flood the serial port).
            if (GameObject.Find("Panto").GetComponent<DualPantoSync>().debug)
                Time.fixedDeltaTime = 0.005f;
            foreach (var l in levels) l.gameObject.SetActive(false);
            await Task.Delay(2000); // let the panto finish its SYNC handshake first
            _ = StartLevel(0);
        }

        async Task StartLevel(int i)
        {
            if (levelIndex >= 0) levels[levelIndex].gameObject.SetActive(false);
            levelIndex = i;
            var level = levels[i];
            level.gameObject.SetActive(true); // re-enabling also resets the monster via OnEnable
            pickups = level.GetComponentsInChildren<Pickup>();
            foreach (var p in pickups) p.ResetPickup();
            await player.ParkAt(level.startNode);
            GameOver = false;
            await speech.Speak(level.introText);
        }

        public void CheckPickups(Vector3 pos)
        {
            if (GameOver) return;
            foreach (var p in pickups)
            {
                if (p.collected || Vector3.Distance(pos, p.transform.position) > pickupRadius) continue;
                p.Collect();
                AudioSource.PlayClipAtPoint(blopClip, p.transform.position);
            }
            if (pickups.All(p => p.collected)) _ = Win();
        }

        async Task Win()
        {
            if (GameOver) return;
            GameOver = true;
            await speech.Speak("hurray! you collected all points");
            if (levelIndex + 1 < levels.Length) _ = StartLevel(levelIndex + 1);
            else await speech.Speak("you finished pantodash. thanks for playing!");
        }

        public async Task Die()
        {
            if (GameOver) return;
            GameOver = true;
            await speech.Speak("you have been killed");
            _ = StartLevel(levelIndex); // restart the current level
        }
    }
}
