using DungeonGame.Spire;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonGame.Player
{
    /// <summary>
    /// Local player interaction: look at a WallTorch and press E to light it.
    /// MVP: no inventory cost, no extinguish.
    /// </summary>
    public class TorchInteractor : NetworkBehaviour
    {
        [SerializeField] private float interactRange = 3.0f;
        [SerializeField] private LayerMask mask = ~0;

        private LocalPlayerCameraRig rig;

        private void Awake()
        {
            rig = GetComponent<LocalPlayerCameraRig>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsOwner) enabled = false;
        }

        private void Update()
        {
            if (!IsOwner) return;
            if (Keyboard.current == null) return;

            if (!Keyboard.current.eKey.wasPressedThisFrame) return;

            var camT = rig != null ? rig.CameraTransform : null;
            if (camT == null) return;

            var ray = new Ray(camT.position, camT.forward);
            if (!Physics.Raycast(ray, out var hit, interactRange, mask, QueryTriggerInteraction.Ignore)) return;

            var torch = hit.collider.GetComponentInParent<WallTorch>();
            if (torch == null) return;

            torch.LightRpc();
        }
    }
}
