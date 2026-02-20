using UnityEngine;

namespace DungeonGame.SpireGen
{
    /// <summary>
    /// A connection point on a room prefab.
    /// Place this component on an empty child transform.
    /// Its forward direction should point OUT of the room.
    /// </summary>
    public class RoomSocket : MonoBehaviour
    {
        public SocketType socketType = SocketType.DoorSmall;

        [Tooltip("Optional size class. Only sockets with matching size can connect.")]
        public int size = 0;

        [Tooltip("If true, generator will avoid using this socket except as a fallback.")]
        public bool lowPriority;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = lowPriority ? new Color(1f, 0.6f, 0.1f) : Color.cyan;
            var p = transform.position;
            Gizmos.DrawSphere(p, 0.08f);
            Gizmos.DrawLine(p, p + transform.forward * 0.5f);
        }
    }
}
