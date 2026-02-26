using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace DungeonGame.Core
{
    /// <summary>
    /// Increases UnityTransport's receive packet queue size to reduce "Receive queue is full" drops.
    /// Dropped packets can cause the client to never receive the host's NetworkObject spawn (so host is invisible)
    /// or the client's own player spawn (so client can't move). Attach to the same GameObject as NetworkManager
    /// or run from NetworkBootstrap; runs once when the transport is available.
    /// </summary>
    public class UnityTransportQueueFix : MonoBehaviour
    {
        [Tooltip("Receive queue size. Default in UTP is often 128; increase to 256 or 512 if you see 'Receive queue is full'.")]
        [Min(128)] [SerializeField] private int maxPacketQueueSize = 512;

        private static bool _applied;

        private void Awake()
        {
            TryApply();
        }

        private void Start()
        {
            TryApply();
        }

        private void TryApply()
        {
            if (_applied) return;

            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            var transport = nm.NetworkConfig?.NetworkTransport as UnityTransport;
            if (transport == null) return;

            ApplyToTransport(transport, maxPacketQueueSize);
        }

        /// <summary>
        /// Call from NetworkBootstrap after creating or finding the NetworkManager to ensure queue size is set before StartHost/StartClient.
        /// </summary>
        public static void ApplyIfNeeded(NetworkManager networkManager, int queueSize = 512)
        {
            if (networkManager == null || _applied) return;
            var transport = networkManager.NetworkConfig?.NetworkTransport as UnityTransport;
            if (transport == null) return;
            ApplyToTransport(transport, queueSize);
        }

        private static void ApplyToTransport(UnityTransport transport, int queueSize)
        {
            var type = transport.GetType();

            foreach (var name in new[] { "MaxPacketQueueSize", "maxPacketQueueSize", "MaxReceiveQueueSize", "ReceiveQueueCapacity" })
            {
                var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null && (field.FieldType == typeof(int) || field.FieldType == typeof(uint)))
                {
                    field.SetValue(transport, queueSize);
                    _applied = true;
                    Debug.Log($"[Transport] Set {name} = {queueSize}");
                    return;
                }

                var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null && prop.CanWrite && (prop.PropertyType == typeof(int) || prop.PropertyType == typeof(uint)))
                {
                    prop.SetValue(transport, queueSize);
                    _applied = true;
                    Debug.Log($"[Transport] Set {name} = {queueSize}");
                    return;
                }
            }

            Debug.LogWarning("[Transport] Could not find receive/packet queue size on UnityTransport. If you see 'Receive queue is full', add the Player prefab to NetworkManager's Network Prefabs list and increase the transport queue in the Unity Inspector if your UTP version exposes it.");
        }
    }
}
