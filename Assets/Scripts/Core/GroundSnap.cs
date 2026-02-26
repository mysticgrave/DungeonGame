using UnityEngine;

namespace DungeonGame.Core
{
    /// <summary>
    /// Shared ground-snap utility. Finds the floor beneath a position without hitting ceilings.
    /// Uses a two-pass strategy:
    ///   1. Short downward ray from just above the position (stays below ceilings).
    ///   2. If that misses (embedded in geometry), ray upward to escape, then down again.
    /// </summary>
    public static class GroundSnap
    {
        private const float ShortCastAbove = 3f;
        private const float EscapeCastUp = 5f;
        private const float DownCastMax = 20f;
        private static readonly int GroundMask = ~0;

        /// <summary>
        /// Find the ground Y at the given XZ position, starting near the provided Y.
        /// Returns true if ground was found; result is in groundPoint.
        /// </summary>
        public static bool TryFindGround(Vector3 position, out Vector3 groundPoint)
        {
            groundPoint = position;

            // Pass 1: cast down from a few meters above. Stays below ceilings in most rooms.
            Vector3 origin1 = new Vector3(position.x, position.y + ShortCastAbove, position.z);
            if (Physics.Raycast(origin1, Vector3.down, out RaycastHit hit1, ShortCastAbove + DownCastMax, GroundMask, QueryTriggerInteraction.Ignore))
            {
                if (hit1.point.y <= position.y + 1f)
                {
                    groundPoint = hit1.point;
                    return true;
                }
            }

            // Pass 2: we might be inside geometry. Cast UP to find the surface we're embedded in,
            // then cast DOWN from just above that surface.
            Vector3 originUp = new Vector3(position.x, position.y - 0.5f, position.z);
            if (Physics.Raycast(originUp, Vector3.up, out RaycastHit hitUp, EscapeCastUp, GroundMask, QueryTriggerInteraction.Ignore))
            {
                Vector3 aboveSurface = hitUp.point + Vector3.up * 0.1f;
                if (Physics.Raycast(aboveSurface, Vector3.down, out RaycastHit hit2, DownCastMax, GroundMask, QueryTriggerInteraction.Ignore))
                {
                    groundPoint = hit2.point;
                    return true;
                }
                groundPoint = hitUp.point;
                return true;
            }

            // Pass 3: nothing above or below — try from further up as last resort
            // but only use the result if it's below a reasonable ceiling height.
            Vector3 origin3 = new Vector3(position.x, position.y + 10f, position.z);
            if (Physics.Raycast(origin3, Vector3.down, out RaycastHit hit3, 30f, GroundMask, QueryTriggerInteraction.Ignore))
            {
                groundPoint = hit3.point;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Snap a transform to the ground, accounting for a CharacterController's center/height.
        /// </summary>
        public static void SnapTransform(Transform t, CharacterController cc)
        {
            if (cc == null) { SnapTransform(t); return; }

            float halfHeight = cc.height * 0.5f;
            float bottomOffset = cc.center.y - halfHeight;

            if (TryFindGround(t.position, out Vector3 ground))
            {
                float targetY = ground.y - bottomOffset + 0.05f;
                t.position = new Vector3(t.position.x, targetY, t.position.z);
            }
        }

        /// <summary>Snap a transform so its pivot sits on the ground.</summary>
        public static void SnapTransform(Transform t)
        {
            if (TryFindGround(t.position, out Vector3 ground))
                t.position = new Vector3(t.position.x, ground.y + 0.05f, t.position.z);
        }
    }
}
