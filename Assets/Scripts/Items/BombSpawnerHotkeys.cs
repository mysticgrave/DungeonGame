using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonGame.Items
{
    /// <summary>
    /// MVP hotkeys:
    /// - B: spawn a knock bomb at your feet (server)
    /// </summary>
    public class BombSpawnerHotkeys : NetworkBehaviour
    {
        [SerializeField] private NetworkObject knockBombPrefab;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsOwner) enabled = false;
        }

        private void Update()
        {
            if (!IsOwner) return;
            if (Keyboard.current == null) return;

            if (!Keyboard.current.bKey.wasPressedThisFrame) return;

            if (knockBombPrefab == null)
            {
                Debug.LogError("[BombHotkeys] Missing knockBombPrefab");
                return;
            }

            SpawnBombServerRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void SpawnBombServerRpc()
        {
            var pos = transform.position + Vector3.up * 0.25f;
            var no = Instantiate(knockBombPrefab, pos, Quaternion.identity);
            no.Spawn(true);
        }
    }
}
