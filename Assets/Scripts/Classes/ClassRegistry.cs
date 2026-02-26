using System.Collections.Generic;
using UnityEngine;

namespace DungeonGame.Classes
{
    /// <summary>
    /// Resolves classId to ClassDefinition. Populate via inspector or load from Resources.
    /// </summary>
    public class ClassRegistry : MonoBehaviour
    {
        private static ClassRegistry _instance;
        public static ClassRegistry Instance => _instance;

        [SerializeField] private List<ClassDefinition> classes = new();

        private readonly Dictionary<string, ClassDefinition> _byId = new();

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Rebuild();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void Rebuild()
        {
            _byId.Clear();
            foreach (var c in classes)
            {
                if (c != null && !string.IsNullOrEmpty(c.classId))
                    _byId[c.classId] = c;
            }
        }

        public static ClassDefinition Get(string classId)
        {
            if (Instance == null) return null;
            return Instance._byId.TryGetValue(classId ?? "", out var def) ? def : null;
        }

        public static int IndexOf(ClassDefinition definition)
        {
            if (Instance == null || definition == null) return -1;
            for (int i = 0; i < Instance.classes.Count; i++)
            {
                if (Instance.classes[i] == definition) return i;
            }
            return -1;
        }

        public static ClassDefinition GetByIndex(int index)
        {
            if (Instance == null || index < 0 || index >= Instance.classes.Count) return null;
            return Instance.classes[index];
        }

        public static int GetClassCount()
        {
            if (Instance == null || Instance.classes == null) return 0;
            return Instance.classes.Count;
        }

        public static void SetClass(PlayerClass playerClass, ClassDefinition definition)
        {
            if (playerClass == null || definition == null) return;
            playerClass.SetClass(definition);
        }

        public static ClassDefinition GetClass(PlayerClass playerClass)
        {
            if (playerClass == null) return null;
            return Get(playerClass.ClassId);
        }

        private void OnValidate()
        {
            Rebuild();
        }
    }
}
