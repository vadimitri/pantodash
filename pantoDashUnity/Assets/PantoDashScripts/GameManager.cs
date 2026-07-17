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

        [Header("Hardware fit (applied on real panto only, not emulator)")]
        [Tooltip("Levels are authored ~10 units wide, but the physical workspace is ~110x80 Unity units (Panto prefab scale 3.1/3.8). Level roots are scaled by this so the track fills the device.")]
        public float hardwareScale = 7f;
        [Tooltip("Z shift for level roots after scaling. Workspace is z in [-78, +2]; this centers the scaled track in it.")]
        public float hardwareZShift = 30f;
        [Tooltip("Press distance (Unity units) to trigger a dash on hardware. 3 units is roughly 1cm of physical displacement against the freeze hold. Overrides DashController.pressThreshold.")]
        public float hardwarePressThreshold = 3f;

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
            else
                ApplyHardwareFit();
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

        // Levels are authored small so they fit the emulator camera and mouse. On the
        // real device that same layout is a ~3cm patch at the top of the workspace, so
        // scale everything up at runtime (scene stays untouched) and convert the
        // distance-based tuning values along with it.
        void ApplyHardwareFit()
        {
            foreach (var l in levels)
            {
                var t = l.transform;
                t.localScale = Vector3.Scale(t.localScale, new Vector3(hardwareScale, 1, hardwareScale));
                t.position += new Vector3(0, 0, hardwareZShift);
            }
            pickupRadius *= hardwareScale;
            player.pressThreshold = hardwarePressThreshold;
            player.dashSpeed *= hardwareScale; // distances grew x7, keep dash duration
            foreach (var m in FindObjectsByType<MonsterController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                m.speed *= hardwareScale;
                m.killRadius *= hardwareScale;
            }
            // The toolkit camera is a 7.5-unit ortho over the authored area and its
            // startup culling mask is the blind view (layer 9 only) — the scaled
            // track would be invisible. Re-fit it to the whole workspace and show
            // all layers so nodes/points are visible while playing.
            var cam = Camera.main;
            cam.transform.position = new Vector3(0, 10, -38);
            cam.orthographicSize = 45;
            cam.cullingMask = -1;
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
