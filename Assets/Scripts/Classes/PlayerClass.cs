using System;
using DungeonGame.Meta;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Classes
{
    /// <summary>
    /// Synced class for this run. Server sets it when player spawns (default or from client's class-select choice).
    /// Used for run rewards (EXP applied to this class) and stat application.
    /// Class is synced as an index into ClassRegistry's list.
    /// </summary>
    public class PlayerClass : NetworkBehaviour
    {
        [SerializeField] private ClassDefinition defaultClass;

        private readonly NetworkVariable<int> classIndexNet = new(-1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private bool _requestedClassFromSelection;

        /// <summary>Fired when the class index changes (previousIndex, newIndex).</summary>
        public event Action<int, int> OnClassIndexChanged;

        /// <summary>Current class index in ClassRegistry.</summary>
        public int ClassIndex => classIndexNet.Value;

        /// <summary>
        /// Class id for this run (for meta progression EXP). Empty if not set.
        /// </summary>
        public string ClassId => ResolveClassId();

        public ClassDefinition Definition => ClassRegistry.Get(ClassId);

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            classIndexNet.OnValueChanged += HandleClassIndexChanged;

            if (IsServer && classIndexNet.Value < 0)
            {
                if (defaultClass != null)
                    SetClass(defaultClass);
            }
        }

        public override void OnNetworkDespawn()
        {
            classIndexNet.OnValueChanged -= HandleClassIndexChanged;
            base.OnNetworkDespawn();
        }

        private void HandleClassIndexChanged(int previousValue, int newValue)
        {
            OnClassIndexChanged?.Invoke(previousValue, newValue);
        }

        private void Update()
        {
            if (!IsOwner) return;
            if (_requestedClassFromSelection) return;
            if (classIndexNet.Value >= 0) return;
            var meta = MetaProgression.Instance;
            if (meta == null) return;
            int selected = meta.GetSelectedClassIndex();
            if (selected < 0) return;
            if (ClassRegistry.GetByIndex(selected) == null) return;
            _requestedClassFromSelection = true;
            RequestSetClassFromSelectionServerRpc(selected);
        }

        /// <summary>
        /// Public method for UI to request a class change at any time (e.g. from ClassSelectUI in Town).
        /// Resets the guard flag and sends the ServerRpc.
        /// </summary>
        public void RequestClassChange(int classIndex)
        {
            if (!IsOwner) return;
            _requestedClassFromSelection = true;
            RequestSetClassFromSelectionServerRpc(classIndex);
        }

        [ServerRpc]
        private void RequestSetClassFromSelectionServerRpc(int classIndex)
        {
            if (classIndex < 0) return;
            if (ClassRegistry.GetByIndex(classIndex) == null) return;
            classIndexNet.Value = classIndex;
        }

        /// <summary>
        /// Server: set the class for this run (from ClassRegistry).
        /// </summary>
        public void SetClass(ClassDefinition definition)
        {
            if (!IsServer || definition == null) return;
            int idx = ClassRegistry.IndexOf(definition);
            if (idx >= 0) classIndexNet.Value = idx;
        }

        /// <summary>
        /// Server: set the class by index (for class-select sync).
        /// </summary>
        public void SetClassByIndex(int classIndex)
        {
            if (!IsServer || classIndex < 0) return;
            if (ClassRegistry.GetByIndex(classIndex) == null) return;
            classIndexNet.Value = classIndex;
        }

        private string ResolveClassId()
        {
            var def = ClassRegistry.GetByIndex(classIndexNet.Value);
            return def != null ? def.classId : "";
        }
    }
}
