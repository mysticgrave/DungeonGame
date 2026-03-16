using DungeonGame.Interaction;
using DungeonGame.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonGame.Player
{
    /// <summary>
    /// General-purpose F-key interaction system. Raycasts forward from the camera,
    /// finds IInteractable on hit objects, shows a prompt, and executes the interaction
    /// on the server. Owner-only (like CrosshairUI).
    /// </summary>
    public class PlayerInteractor : NetworkBehaviour
    {
        [SerializeField] private float interactRange = 4f;
        [SerializeField] private LayerMask mask = ~0;

        private LocalPlayerCameraRig _rig;
        private IInteractable _currentTarget;
        private NetworkObject _currentTargetNetObj;
        private string _currentPrompt;

        // OnGUI style cache
        private GUIStyle _promptStyle;
        private GUIStyle _shadowStyle;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsOwner)
            {
                enabled = false;
                return;
            }
            _rig = GetComponent<LocalPlayerCameraRig>();
        }

        private void Update()
        {
            if (!IsOwner) return;
            if (PauseMenuController.IsPaused) { _currentTarget = null; return; }

            UpdateRaycast();

            if (_currentTarget != null
                && Keyboard.current != null
                && Keyboard.current.fKey.wasPressedThisFrame)
            {
                RequestInteract();
            }
        }

        private void UpdateRaycast()
        {
            _currentTarget = null;
            _currentTargetNetObj = null;
            _currentPrompt = null;

            var camT = _rig != null ? _rig.CameraTransform : null;
            if (camT == null) return;

            var ray = new Ray(camT.position, camT.forward);
            if (!Physics.Raycast(ray, out var hit, interactRange, mask, QueryTriggerInteraction.Ignore))
                return;

            var interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable == null) return;

            // Client-side quick check: CanInteract uses localClientId
            if (!interactable.CanInteract(NetworkManager.Singleton.LocalClientId)) return;

            _currentTarget = interactable;
            _currentPrompt = interactable.InteractPrompt;

            // Cache NetworkObject for the RPC target
            var mb = interactable as MonoBehaviour;
            if (mb != null)
                _currentTargetNetObj = mb.GetComponentInParent<NetworkObject>();
        }

        private void RequestInteract()
        {
            if (_currentTargetNetObj == null)
            {
                Debug.LogWarning("[PlayerInteractor] Target has no NetworkObject — cannot send RPC.");
                return;
            }

            InteractServerRpc(_currentTargetNetObj.NetworkObjectId);
        }

        [Rpc(SendTo.Server)]
        private void InteractServerRpc(ulong targetNetworkObjectId)
        {
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                    targetNetworkObjectId, out var netObj))
            {
                Debug.LogWarning($"[PlayerInteractor] Server: target {targetNetworkObjectId} not found.");
                return;
            }

            var interactable = netObj.GetComponentInChildren<IInteractable>();
            if (interactable == null)
            {
                Debug.LogWarning("[PlayerInteractor] Server: target has no IInteractable.");
                return;
            }

            if (!interactable.CanInteract(OwnerClientId))
            {
                Debug.Log($"[PlayerInteractor] Server: client {OwnerClientId} cannot interact with this.");
                return;
            }

            interactable.Interact(OwnerClientId);
        }

        // ─── OnGUI Prompt ───────────────────────────────────────────

        private void OnGUI()
        {
            if (_currentTarget == null || string.IsNullOrEmpty(_currentPrompt)) return;
            if (Event.current.type != EventType.Repaint) return;

            EnsureStyles();

            string text = $"[F]  {_currentPrompt}";
            var content = new GUIContent(text);
            var size = _promptStyle.CalcSize(content);

            float x = (Screen.width - size.x) * 0.5f;
            float y = Screen.height * 0.72f; // slightly below center

            // Shadow
            var shadowRect = new Rect(x + 1f, y + 1f, size.x, size.y);
            GUI.Label(shadowRect, content, _shadowStyle);

            // Main text
            var mainRect = new Rect(x, y, size.x, size.y);
            GUI.Label(mainRect, content, _promptStyle);
        }

        private void EnsureStyles()
        {
            if (_promptStyle != null) return;

            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _promptStyle.normal.textColor = new Color(1f, 1f, 1f, 0.95f);

            _shadowStyle = new GUIStyle(_promptStyle);
            _shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.7f);
        }
    }
}
