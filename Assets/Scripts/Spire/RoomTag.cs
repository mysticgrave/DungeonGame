using UnityEngine;

namespace DungeonGame.Spire
{
    /// <summary>
    /// Tags a room prefab instance for spawn director / pacing logic.
    /// Attach to the root of a room module prefab.
    /// </summary>
    public class RoomTag : MonoBehaviour
    {
        public enum Tag
        {
            Combat = 0,
            Breather = 1,
            Rest = 2,
            Landmark = 3,
            Transition = 4,
        }

        public Tag tag = Tag.Combat;
    }
}
