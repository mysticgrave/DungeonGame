using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Player
{
    /// <summary>
    /// Instantiates FPS arms as a child of the runtime camera. Only the local player sees these.
    /// Put the arms prefab on the FPSArms layer so only the local camera renders them.
    /// Exposes a weapon mount point for WeaponController to parent the sword to.
    /// </summary>
    public class FPSArmsController : NetworkBehaviour
    {
        [Tooltip("Prefab with arms/hands mesh. Should have a child named WeaponMount (or assign below) for the sword.")]
        [SerializeField] private GameObject fpsArmsPrefab;
        [Tooltip("Layer for FPS arms (create in Tags & Layers). Only the local camera renders this.")]
        [SerializeField] private string fpsArmsLayerName = "FPSArms";
        [Tooltip("Child transform under the arms prefab where the weapon attaches. If null, searches for 'WeaponMount'.")]
        [SerializeField] private string weaponMountChildName = "WeaponMount";
        [Tooltip("Local offset of arms from camera (X=right, Y=down, Z=forward). Arms at (0,0,0) may be inside or behind the camera.")]
        [SerializeField] private Vector3 armsOffset = new Vector3(0.1f, -0.18f, 0.38f);
        [Tooltip("If true and no prefab assigned, creates a bright cube to verify FPS arms pipeline.")]
        [SerializeField] private bool createDebugCubeWhenNoPrefab = true;

        private Transform _fpsWeaponMount;
        private GameObject _armsInstance;
        private bool _setupDone;
        private RagdollColliderSwitch _ragdollSwitch;
        private KnockableCapsule _knock;

        /// <summary>Transform where the weapon should be parented for the local player's FPS view. Null until setup.</summary>
        public Transform FPSWeaponMount => _fpsWeaponMount;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsOwner) { enabled = false; return; }
            _ragdollSwitch = GetComponent<RagdollColliderSwitch>();
            _knock = GetComponent<KnockableCapsule>();
        }

        private void Start()
        {
            if (!IsOwner) return;
            TrySetupFPSArms();
        }

        private void LateUpdate()
        {
            if (!IsOwner) return;
            if (!_setupDone) TrySetupFPSArms();

            if (_armsInstance != null)
            {
                bool isRagdolling = (_ragdollSwitch != null && _ragdollSwitch.IsRagdoll) ||
                                    (_knock != null && _knock.IsKnocked());
                _armsInstance.SetActive(!isRagdolling);
            }
        }

        private void TrySetupFPSArms()
        {
            if (_setupDone) return;

            var camRig = GetComponent<FirstPersonCameraRig>();
            if (camRig == null || !camRig.enabled) return;
            var cam = camRig.Camera;
            if (cam == null) return;

            if (fpsArmsPrefab == null)
            {
                if (createDebugCubeWhenNoPrefab)
                {
                    _armsInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    _armsInstance.name = "FPSArms_DebugCube";
                    _armsInstance.transform.SetParent(cam.transform, false);
                    _armsInstance.transform.localPosition = armsOffset;
                    _armsInstance.transform.localScale = new Vector3(0.1f, 0.2f, 0.4f);
                    Object.Destroy(_armsInstance.GetComponent<Collider>());
                    int debugLayer = LayerMask.NameToLayer(fpsArmsLayerName);
                    if (debugLayer >= 0) SetLayerRecursively(_armsInstance.transform, debugLayer);
                    var debugMount = new GameObject(weaponMountChildName).transform;
                    debugMount.SetParent(_armsInstance.transform, false);
                    debugMount.localPosition = Vector3.forward * 0.3f;
                    _fpsWeaponMount = debugMount;
                    _setupDone = true;
                    Debug.Log("[FPSArmsController] No prefab assigned — created DEBUG CUBE. If you see it, the pipeline works. Assign fpsArmsPrefab for real arms.");
                }
                else
                {
                    Debug.LogWarning("[FPSArmsController] fpsArmsPrefab not assigned. Assign an FPS arms prefab in the inspector.", this);
                    _setupDone = true;
                }
                return;
            }

            int layer = LayerMask.NameToLayer(fpsArmsLayerName);
            if (layer < 0)
            {
                Debug.LogWarning($"[FPSArmsController] Layer '{fpsArmsLayerName}' not found. Create it in Tags & Layers. FPS arms disabled.", this);
                _setupDone = true;
                return;
            }

            _armsInstance = Instantiate(fpsArmsPrefab, cam.transform);
            _armsInstance.name = "FPSArms";
            _armsInstance.transform.localPosition = armsOffset;
            _armsInstance.transform.localRotation = Quaternion.identity;
            _armsInstance.transform.localScale = Vector3.one;
            SetLayerRecursively(_armsInstance.transform, layer);

            var mount = _armsInstance.transform.Find(weaponMountChildName);
            if (mount == null)
            {
                var go = new GameObject(weaponMountChildName);
                go.transform.SetParent(_armsInstance.transform, false);
                go.transform.localPosition = Vector3.forward * 0.5f;
                go.transform.localRotation = Quaternion.identity;
                mount = go.transform;
            }
            _fpsWeaponMount = mount;

            _setupDone = true;
            Debug.Log("[FPSArmsController] FPS arms created. If you don't see them, check: prefab has Mesh/SkinnedMeshRenderer, FPSArms layer exists, Arms Offset is correct.");
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner && _armsInstance != null)
            {
                Destroy(_armsInstance);
                _armsInstance = null;
                _fpsWeaponMount = null;
            }
            base.OnNetworkDespawn();
        }

        private static void SetLayerRecursively(Transform t, int layer)
        {
            if (t == null) return;
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursively(t.GetChild(i), layer);
        }
    }
}
