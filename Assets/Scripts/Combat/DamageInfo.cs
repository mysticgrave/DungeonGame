using UnityEngine;

namespace DungeonGame.Combat
{
    public enum DamageType
    {
        Physical = 0,
        Fire,
        Ice,
        Lightning,
        Poison
    }

    /// <summary>
    /// Rich damage metadata passed through the damage pipeline.
    /// Server-side only — never serialized over the network directly.
    /// Individual fields are sent via ClientRpc for client-side feedback.
    /// </summary>
    public struct DamageInfo
    {
        public int Amount;
        public DamageType Type;
        public ulong AttackerClientId;
        public bool IsCrit;
        public Vector3 HitPosition;
        public Vector3 KnockImpulse;

        /// <summary>Create a simple Physical damage info.</summary>
        public DamageInfo(int amount)
        {
            Amount = amount;
            Type = DamageType.Physical;
            AttackerClientId = 0;
            IsCrit = false;
            HitPosition = Vector3.zero;
            KnockImpulse = Vector3.zero;
        }

        /// <summary>Create damage info with type.</summary>
        public DamageInfo(int amount, DamageType type)
        {
            Amount = amount;
            Type = type;
            AttackerClientId = 0;
            IsCrit = false;
            HitPosition = Vector3.zero;
            KnockImpulse = Vector3.zero;
        }
    }
}
