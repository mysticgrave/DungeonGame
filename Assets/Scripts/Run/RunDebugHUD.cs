using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Run
{
    /// <summary>
    /// Tiny OnGUI HUD for debugging run state.
    /// Safe for MVP; replace with real UI later.
    /// </summary>
    public class RunDebugHUD : MonoBehaviour
    {
        [Tooltip("Uncheck to hide the debug HUD while playing.")]
        [SerializeField] private bool visible;

        private SpireRunState run;

        private void Awake()
        {
            run = GetComponent<SpireRunState>();
            if (run == null) run = FindFirstObjectByType<SpireRunState>();
        }

        private void OnGUI()
        {
            if (!visible || run == null) return;
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;

            GUILayout.BeginArea(new Rect(10, 10, 450, 180), GUI.skin.box);
            GUILayout.Label("RUN DEBUG");
            GUILayout.Label($"Mode: {(NetworkManager.Singleton.IsHost ? "Host" : NetworkManager.Singleton.IsClient ? "Client" : "?")}");
            GUILayout.Space(6);
            GUILayout.Label($"Floor: {run.Floor}");
            GUILayout.Label($"Segment: {run.Segment} (per {run.FloorsPerSegment} floors)");
            GUILayout.Label($"Unlocked Segment: {run.HighestUnlockedSegment}");
            GUILayout.Space(6);
            GUILayout.Label("Host hotkeys: F10 +1 floor | F11 +5 floors | F12 evac + Town");
            GUILayout.EndArea();
        }
    }
}
