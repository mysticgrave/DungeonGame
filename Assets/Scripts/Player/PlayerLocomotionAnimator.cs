using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonGame.Player
{
    /// <summary>
    /// Drives the character Animator for locomotion: sets a "Speed" float (0 = idle, ~0.5 = walk, 1 = run).
    /// Uses move input from FirstPersonMotor when available so animation starts/stops with key press; otherwise falls back to velocity.
    /// Disable Apply Root Motion on the Animator to prevent the character drifting or walking away from the camera.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DefaultExecutionOrder(-100)]
    public class PlayerLocomotionAnimator : NetworkBehaviour
    {
        [Tooltip("Optional. Leave empty — Animator is auto-found on this object, children, or parent.")]
        [SerializeField] private Animator animator;

        [Tooltip("Walk speed (match FirstPersonMotor).")]
        [SerializeField] private float walkSpeed = 5f;
        [Tooltip("Sprint speed (match FirstPersonMotor).")]
        [SerializeField] private float sprintSpeed = 7.5f;

        [Tooltip("Animator parameter name for movement speed. Synty uses 'MoveSpeed'.")]
        [SerializeField] private string speedParamName = "MoveSpeed";

        [Tooltip("When true, Speed is driven by input. When false, uses CharacterController velocity.")]
        [SerializeField] private bool useInputForSpeed = true;

        [Tooltip("When off, passes 0-7 m/s (Synty). When on, passes 0-1 for simple controllers.")]
        [SerializeField] private bool useNormalizedSpeed = false;

        [Header("Synty (MoveSpeed, StrafeDirection, IsStrafing)")]
        [Tooltip("Smoothing for MoveSpeed and StrafeDirection. Higher = smoother, slower response. 12-15 works well.")]
        [SerializeField] private float paramSmoothSpeed = 12f;
        [Tooltip("Animator Bool for grounded.")]
        [SerializeField] private string isGroundedParamName = "IsGrounded";
        [Tooltip("Animator Bool for jumping.")]
        [SerializeField] private string isJumpingParamName = "IsJumping";
        [Tooltip("Animator Float for strafe X (-1 left, +1 right). Synty: StrafeDirectionX")]
        [SerializeField] private string strafeDirectionXParamName = "StrafeDirectionX";
        [Tooltip("Animator Float for strafe Z (-1 back, +1 forward). Synty: StrafeDirectionZ")]
        [SerializeField] private string strafeDirectionZParamName = "StrafeDirectionZ";
        [Tooltip("Animator Float for strafe mode. FPS: use 1. Synty: IsStrafing")]
        [SerializeField] private string isStrafingParamName = "IsStrafing";
        [Tooltip("Animator Bool. Synty: IsStopped")]
        [SerializeField] private string isStoppedParamName = "IsStopped";
        [Tooltip("Animator Bool. Synty: IsWalking")]
        [SerializeField] private string isWalkingParamName = "IsWalking";
        [Tooltip("Animator Int. 0=Idle, 1=Walk, 2=Run, 3=Sprint. Synty: CurrentGait")]
        [SerializeField] private string currentGaitParamName = "CurrentGait";
        [Tooltip("Animator Bool. Required for Idle→Walk. Synty: MovementInputHeld")]
        [SerializeField] private string movementInputHeldParamName = "MovementInputHeld";
        [Tooltip("Animator Bool. Synty: MovementInputPressed")]
        [SerializeField] private string movementInputPressedParamName = "MovementInputPressed";
        [Tooltip("Animator Bool. Synty: MovementInputTapped")]
        [SerializeField] private string movementInputTappedParamName = "MovementInputTapped";
        [Tooltip("Seconds before input counts as 'held' vs 'pressed'. Matches SamplePlayerAnimationController.")]
        [SerializeField] private float movementInputHoldThreshold = 0.15f;
        [Tooltip("Float. 1=forward strafe tree, 0=backward. Synty: ForwardStrafe")]
        [SerializeField] private string forwardStrafeParamName = "ForwardStrafe";
        [Tooltip("Bool. True when rotating while standing still. Synty: IsTurningInPlace")]
        [SerializeField] private string isTurningInPlaceParamName = "IsTurningInPlace";
        [Tooltip("Float. Degrees offset for turn-in-place blend. Synty: CameraRotationOffset")]
        [SerializeField] private string cameraRotationOffsetParamName = "CameraRotationOffset";
        [Tooltip("Float. Shuffle direction X. Synty: ShuffleDirectionX")]
        [SerializeField] private string shuffleDirectionXParamName = "ShuffleDirectionX";
        [Tooltip("Float. Shuffle direction Z. Synty: ShuffleDirectionZ")]
        [SerializeField] private string shuffleDirectionZParamName = "ShuffleDirectionZ";
        [Tooltip("Bool. True when starting from idle. Synty: IsStarting")]
        [SerializeField] private string isStartingParamName = "IsStarting";
        [Tooltip("Degrees/sec yaw to count as turning in place. ~25 = gentle look-around.")]
        [SerializeField] private float turnInPlaceThreshold = 25f;
        [Tooltip("Mouse sensitivity for turn detection. Match FirstPersonCameraRig.lookSensitivity.")]
        [SerializeField] private float lookSensitivityForTurn = 0.12f;

        private CharacterController _cc;
        private FirstPersonMotor _motor;
        private PlayerBodyStateMachine _bodyState;
        private int _speedParamId;
        private int _isGroundedParamId;
        private int _isJumpingParamId;
        private int _strafeDirectionXParamId;
        private int _strafeDirectionZParamId;
        private int _isStrafingParamId;
        private int _isStoppedParamId;
        private int _isWalkingParamId;
        private int _currentGaitParamId;
        private int _movementInputHeldParamId;
        private int _movementInputPressedParamId;
        private int _movementInputTappedParamId;
        private int _forwardStrafeParamId;
        private int _isTurningInPlaceParamId;
        private int _cameraRotationOffsetParamId;
        private int _shuffleDirectionXParamId;
        private int _shuffleDirectionZParamId;
        private int _isStartingParamId;
        private bool? _speedParamValid;
        private bool? _hasIsGroundedParam;
        private bool? _hasIsJumpingParam;
        private bool? _hasStrafeDirectionXParam;
        private bool? _hasStrafeDirectionZParam;
        private bool? _hasIsStrafingParam;
        private bool? _hasIsStoppedParam;
        private bool? _hasIsWalkingParam;
        private bool? _hasCurrentGaitParam;
        private bool? _hasMovementInputHeldParam;
        private bool? _hasMovementInputPressedParam;
        private bool? _hasMovementInputTappedParam;
        private bool? _hasForwardStrafeParam;
        private bool? _hasIsTurningInPlaceParam;
        private bool? _hasCameraRotationOffsetParam;
        private bool? _hasShuffleDirectionXParam;
        private bool? _hasShuffleDirectionZParam;
        private bool? _hasIsStartingParam;
        private float _movementInputDuration;
        private float _locomotionStartTimer;
        private float _lastYawForNonOwner;
        private float _smoothedMoveSpeed;
        private float _smoothedStrafeX;
        private float _smoothedStrafeZ;
        private Vector3 _lastPositionForNonOwner;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _motor = GetComponent<FirstPersonMotor>();
            _bodyState = GetComponent<PlayerBodyStateMachine>();
            CacheAnimator();
            _speedParamId = Animator.StringToHash(speedParamName);
            _isGroundedParamId = !string.IsNullOrEmpty(isGroundedParamName) ? Animator.StringToHash(isGroundedParamName) : 0;
            _isJumpingParamId = !string.IsNullOrEmpty(isJumpingParamName) ? Animator.StringToHash(isJumpingParamName) : 0;
            _strafeDirectionXParamId = !string.IsNullOrEmpty(strafeDirectionXParamName) ? Animator.StringToHash(strafeDirectionXParamName) : 0;
            _strafeDirectionZParamId = !string.IsNullOrEmpty(strafeDirectionZParamName) ? Animator.StringToHash(strafeDirectionZParamName) : 0;
            _isStrafingParamId = !string.IsNullOrEmpty(isStrafingParamName) ? Animator.StringToHash(isStrafingParamName) : 0;
            _isStoppedParamId = !string.IsNullOrEmpty(isStoppedParamName) ? Animator.StringToHash(isStoppedParamName) : 0;
            _isWalkingParamId = !string.IsNullOrEmpty(isWalkingParamName) ? Animator.StringToHash(isWalkingParamName) : 0;
            _currentGaitParamId = !string.IsNullOrEmpty(currentGaitParamName) ? Animator.StringToHash(currentGaitParamName) : 0;
            _movementInputHeldParamId = !string.IsNullOrEmpty(movementInputHeldParamName) ? Animator.StringToHash(movementInputHeldParamName) : 0;
            _movementInputPressedParamId = !string.IsNullOrEmpty(movementInputPressedParamName) ? Animator.StringToHash(movementInputPressedParamName) : 0;
            _movementInputTappedParamId = !string.IsNullOrEmpty(movementInputTappedParamName) ? Animator.StringToHash(movementInputTappedParamName) : 0;
            _forwardStrafeParamId = !string.IsNullOrEmpty(forwardStrafeParamName) ? Animator.StringToHash(forwardStrafeParamName) : 0;
            _isTurningInPlaceParamId = !string.IsNullOrEmpty(isTurningInPlaceParamName) ? Animator.StringToHash(isTurningInPlaceParamName) : 0;
            _cameraRotationOffsetParamId = !string.IsNullOrEmpty(cameraRotationOffsetParamName) ? Animator.StringToHash(cameraRotationOffsetParamName) : 0;
            _shuffleDirectionXParamId = !string.IsNullOrEmpty(shuffleDirectionXParamName) ? Animator.StringToHash(shuffleDirectionXParamName) : 0;
            _shuffleDirectionZParamId = !string.IsNullOrEmpty(shuffleDirectionZParamName) ? Animator.StringToHash(shuffleDirectionZParamName) : 0;
            _isStartingParamId = !string.IsNullOrEmpty(isStartingParamName) ? Animator.StringToHash(isStartingParamName) : 0;
        }

        private void CacheAnimator()
        {
            if (animator != null) return;
            animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (animator == null) animator = GetComponentInParent<Animator>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _lastPositionForNonOwner = transform.position;
            _lastYawForNonOwner = transform.eulerAngles.y;
        }

        private void LateUpdate()
        {
            if (animator == null) CacheAnimator();
            if (animator == null || !animator.enabled) return;

            if (!_speedParamValid.HasValue)
            {
                _speedParamValid = HasSpeedParameter();
                if (!_speedParamValid.Value)
                    Debug.LogWarning($"[PlayerLocomotionAnimator] Animator has no Float parameter named \"{speedParamName}\". Add it in the Animator Controller for Idle/Walk/Run. See LOCOMOTION_ANIMATOR.md.");
            }

            if (!_speedParamValid.Value) return;

            bool grounded = _cc.isGrounded;
            bool jumping = !grounded && _cc.velocity.y > 0.1f;

            if (_isGroundedParamId != 0)
            {
                if (!_hasIsGroundedParam.HasValue) _hasIsGroundedParam = HasBoolParameter(_isGroundedParamId);
                if (_hasIsGroundedParam.Value) animator.SetBool(_isGroundedParamId, grounded);
            }
            if (_isJumpingParamId != 0)
            {
                if (!_hasIsJumpingParam.HasValue) _hasIsJumpingParam = HasBoolParameter(_isJumpingParamId);
                if (_hasIsJumpingParam.Value) animator.SetBool(_isJumpingParamId, jumping);
            }

            // Only drive locomotion when we can move (Standing). Stunned/Frozen/Ragdoll → idle.
            if (_bodyState != null && _bodyState.IsMovementDisabled)
            {
                _smoothedMoveSpeed = 0f;
                _smoothedStrafeX = 0f;
                _smoothedStrafeZ = 0f;
                animator.SetFloat(_speedParamId, 0f);
                if (_isGroundedParamId != 0 && _hasIsGroundedParam == true) animator.SetBool(_isGroundedParamId, true);
                if (_isJumpingParamId != 0 && _hasIsJumpingParam == true) animator.SetBool(_isJumpingParamId, false);
                if (_strafeDirectionXParamId != 0 && _hasStrafeDirectionXParam == true) animator.SetFloat(_strafeDirectionXParamId, 0f);
                if (_strafeDirectionZParamId != 0 && _hasStrafeDirectionZParam == true) animator.SetFloat(_strafeDirectionZParamId, 0f);
                if (_isStoppedParamId != 0 && _hasIsStoppedParam == true) animator.SetBool(_isStoppedParamId, true);
                if (_isWalkingParamId != 0 && _hasIsWalkingParam == true) animator.SetBool(_isWalkingParamId, false);
                if (_currentGaitParamId != 0 && _hasCurrentGaitParam == true) animator.SetInteger(_currentGaitParamId, 0);
                if (_movementInputHeldParamId != 0 && _hasMovementInputHeldParam == true) animator.SetBool(_movementInputHeldParamId, false);
                if (_movementInputPressedParamId != 0 && _hasMovementInputPressedParam == true) animator.SetBool(_movementInputPressedParamId, false);
                if (_movementInputTappedParamId != 0 && _hasMovementInputTappedParam == true) animator.SetBool(_movementInputTappedParamId, false);
                if (_forwardStrafeParamId != 0) animator.SetFloat(_forwardStrafeParamId, 0f);
                if (_isTurningInPlaceParamId != 0 && _hasIsTurningInPlaceParam == true) animator.SetBool(_isTurningInPlaceParamId, false);
                if (_cameraRotationOffsetParamId != 0) animator.SetFloat(_cameraRotationOffsetParamId, 0f);
                if (_isStartingParamId != 0 && _hasIsStartingParam == true) animator.SetBool(_isStartingParamId, false);
                _movementInputDuration = 0f;
                return;
            }

            Vector2 moveDir;
            float targetMoveSpeed;
            bool isSprinting = false;
            if (IsOwner && _motor != null)
            {
                moveDir = _motor.GetMoveInput();
                if (useInputForSpeed)
                {
                    float moveInput = _motor.GetMoveInputMagnitude();
                    isSprinting = _motor.IsSprinting();
                    targetMoveSpeed = moveInput > 0f ? (isSprinting ? sprintSpeed : walkSpeed) * moveInput : 0f;
                }
                else
                {
                    Vector3 horizontalVel = _cc.velocity;
                    horizontalVel.y = 0f;
                    targetMoveSpeed = horizontalVel.magnitude;
                }
            }
            else
            {
                Vector3 pos = transform.position;
                Vector3 delta = pos - _lastPositionForNonOwner;
                _lastPositionForNonOwner = pos;
                delta.y = 0f;
                float step = Time.deltaTime;
                targetMoveSpeed = step > 0f ? Mathf.Clamp(delta.magnitude / step, 0f, sprintSpeed) : 0f;
                if (delta.sqrMagnitude > 0.0001f)
                {
                    Vector3 dir = delta.normalized;
                    moveDir = new Vector2(Vector3.Dot(transform.right, dir), Vector3.Dot(transform.forward, dir));
                }
                else
                    moveDir = Vector2.zero;
            }

            if (useNormalizedSpeed && sprintSpeed > 0f)
                targetMoveSpeed = targetMoveSpeed / sprintSpeed;

            float dt = Time.deltaTime;
            float smooth = Mathf.Clamp(paramSmoothSpeed * dt, 0f, 1f);
            _smoothedMoveSpeed = Mathf.Lerp(_smoothedMoveSpeed, targetMoveSpeed, smooth);
            _smoothedStrafeX = Mathf.Lerp(_smoothedStrafeX, moveDir.x, smooth);
            _smoothedStrafeZ = Mathf.Lerp(_smoothedStrafeZ, moveDir.y, smooth);

            // Synty sample: Tapped (frame 0) -> Pressed (0..holdThreshold) -> Held (>holdThreshold). Mutually exclusive.
            bool hasMoveInput = moveDir.sqrMagnitude > 0.01f;
            bool tapped = false;
            bool pressed = false;
            bool held = false;
            if (hasMoveInput)
            {
                if (_movementInputDuration == 0f)
                    tapped = true;
                else if (_movementInputDuration > 0f && _movementInputDuration < movementInputHoldThreshold)
                    pressed = true;
                else
                    held = true;
                _movementInputDuration += Time.deltaTime;
            }
            else
            {
                _movementInputDuration = 0f;
            }

            if (_movementInputHeldParamId != 0)
            {
                if (!_hasMovementInputHeldParam.HasValue) _hasMovementInputHeldParam = HasBoolParameter(_movementInputHeldParamId);
                if (_hasMovementInputHeldParam.Value) animator.SetBool(_movementInputHeldParamId, held);
            }
            if (_movementInputPressedParamId != 0)
            {
                if (!_hasMovementInputPressedParam.HasValue) _hasMovementInputPressedParam = HasBoolParameter(_movementInputPressedParamId);
                if (_hasMovementInputPressedParam.Value) animator.SetBool(_movementInputPressedParamId, pressed);
            }
            if (_movementInputTappedParamId != 0)
            {
                if (!_hasMovementInputTappedParam.HasValue) _hasMovementInputTappedParam = HasBoolParameter(_movementInputTappedParamId);
                if (_hasMovementInputTappedParam.Value) animator.SetBool(_movementInputTappedParamId, tapped);
            }

            animator.SetFloat(_speedParamId, _smoothedMoveSpeed);

            if (_strafeDirectionXParamId != 0)
            {
                if (!_hasStrafeDirectionXParam.HasValue) _hasStrafeDirectionXParam = HasFloatParameter(_strafeDirectionXParamId);
                if (_hasStrafeDirectionXParam.Value) animator.SetFloat(_strafeDirectionXParamId, _smoothedStrafeX);
            }
            if (_strafeDirectionZParamId != 0)
            {
                if (!_hasStrafeDirectionZParam.HasValue) _hasStrafeDirectionZParam = HasFloatParameter(_strafeDirectionZParamId);
                if (_hasStrafeDirectionZParam.Value) animator.SetFloat(_strafeDirectionZParamId, _smoothedStrafeZ);
            }

            float forwardStrafe = _smoothedStrafeZ > 0.1f ? 1f : 0f;
            if (_forwardStrafeParamId != 0)
            {
                if (!_hasForwardStrafeParam.HasValue) _hasForwardStrafeParam = HasFloatParameter(_forwardStrafeParamId);
                if (_hasForwardStrafeParam.Value) animator.SetFloat(_forwardStrafeParamId, forwardStrafe);
            }

            float yawDelta = 0f;
            float yawSpeed = 0f;
            if (IsOwner && Mouse.current != null)
            {
                float mouseDeltaX = Mouse.current.delta.ReadValue().x;
                yawDelta = mouseDeltaX * lookSensitivityForTurn;
                yawSpeed = Mathf.Abs(yawDelta) / Mathf.Max(Time.deltaTime, 0.001f);
            }
            else if (!IsOwner)
            {
                float currentYaw = transform.eulerAngles.y;
                yawDelta = Mathf.DeltaAngle(_lastYawForNonOwner, currentYaw);
                _lastYawForNonOwner = currentYaw;
                yawSpeed = Mathf.Abs(yawDelta) / Mathf.Max(Time.deltaTime, 0.001f);
            }
            bool turningInPlace = !hasMoveInput && grounded && yawSpeed >= turnInPlaceThreshold;
            float cameraRotationOffset = turningInPlace ? Mathf.Clamp(yawDelta, -180f, 180f) : 0f;

            if (_isTurningInPlaceParamId != 0)
            {
                if (!_hasIsTurningInPlaceParam.HasValue) _hasIsTurningInPlaceParam = HasBoolParameter(_isTurningInPlaceParamId);
                if (_hasIsTurningInPlaceParam.Value) animator.SetBool(_isTurningInPlaceParamId, turningInPlace);
            }
            if (_cameraRotationOffsetParamId != 0)
            {
                if (!_hasCameraRotationOffsetParam.HasValue) _hasCameraRotationOffsetParam = HasFloatParameter(_cameraRotationOffsetParamId);
                if (_hasCameraRotationOffsetParam.Value) animator.SetFloat(_cameraRotationOffsetParamId, cameraRotationOffset);
            }

            if (tapped && hasMoveInput)
            {
                if (_shuffleDirectionXParamId != 0)
                {
                    if (!_hasShuffleDirectionXParam.HasValue) _hasShuffleDirectionXParam = HasFloatParameter(_shuffleDirectionXParamId);
                    if (_hasShuffleDirectionXParam.Value) animator.SetFloat(_shuffleDirectionXParamId, moveDir.x);
                }
                if (_shuffleDirectionZParamId != 0)
                {
                    if (!_hasShuffleDirectionZParam.HasValue) _hasShuffleDirectionZParam = HasFloatParameter(_shuffleDirectionZParamId);
                    if (_hasShuffleDirectionZParam.Value) animator.SetFloat(_shuffleDirectionZParamId, moveDir.y);
                }
            }

            if (_isStrafingParamId != 0)
            {
                if (!_hasIsStrafingParam.HasValue) _hasIsStrafingParam = HasFloatParameter(_isStrafingParamId);
                if (_hasIsStrafingParam.Value) animator.SetFloat(_isStrafingParamId, 1f);
            }

            float rawSpeed = useNormalizedSpeed ? _smoothedMoveSpeed * sprintSpeed : _smoothedMoveSpeed;
            _locomotionStartTimer = _locomotionStartTimer > 0f ? Mathf.Max(0f, _locomotionStartTimer - Time.deltaTime) : 0f;
            bool isStarting = false;
            if (_locomotionStartTimer <= 0f && hasMoveInput && rawSpeed < 1f && grounded)
            {
                isStarting = true;
                _locomotionStartTimer = 0.2f;
            }
            if (_locomotionStartTimer > 0f) isStarting = true;
            if (_isStartingParamId != 0)
            {
                if (!_hasIsStartingParam.HasValue) _hasIsStartingParam = HasBoolParameter(_isStartingParamId);
                if (_hasIsStartingParam.Value) animator.SetBool(_isStartingParamId, isStarting);
            }
            bool stopped = rawSpeed < 0.1f && moveDir.sqrMagnitude < 0.01f;
            bool walking = rawSpeed >= 0.1f && rawSpeed < 4f && (IsOwner ? (_motor == null || !_motor.IsSprinting()) : true);
            int gait = rawSpeed < 0.1f ? 0 : (rawSpeed < 3f ? 1 : (rawSpeed < 6f ? 2 : 3));

            if (_isStoppedParamId != 0)
            {
                if (!_hasIsStoppedParam.HasValue) _hasIsStoppedParam = HasBoolParameter(_isStoppedParamId);
                if (_hasIsStoppedParam.Value) animator.SetBool(_isStoppedParamId, stopped);
            }
            if (_isWalkingParamId != 0)
            {
                if (!_hasIsWalkingParam.HasValue) _hasIsWalkingParam = HasBoolParameter(_isWalkingParamId);
                if (_hasIsWalkingParam.Value) animator.SetBool(_isWalkingParamId, walking);
            }
            if (_currentGaitParamId != 0)
            {
                if (!_hasCurrentGaitParam.HasValue) _hasCurrentGaitParam = HasIntParameter(_currentGaitParamId);
                if (_hasCurrentGaitParam.Value) animator.SetInteger(_currentGaitParamId, gait);
            }
        }

        private bool HasSpeedParameter()
        {
            if (animator?.runtimeAnimatorController == null) return false;
            for (int i = 0; i < animator.parameterCount; i++)
            {
                var p = animator.GetParameter(i);
                if (p.nameHash == _speedParamId && p.type == AnimatorControllerParameterType.Float)
                    return true;
            }
            return false;
        }

        private bool HasBoolParameter(int hash)
        {
            if (animator?.runtimeAnimatorController == null) return false;
            for (int i = 0; i < animator.parameterCount; i++)
            {
                var p = animator.GetParameter(i);
                if (p.nameHash == hash && p.type == AnimatorControllerParameterType.Bool)
                    return true;
            }
            return false;
        }

        private bool HasFloatParameter(int hash)
        {
            if (animator?.runtimeAnimatorController == null) return false;
            for (int i = 0; i < animator.parameterCount; i++)
            {
                var p = animator.GetParameter(i);
                if (p.nameHash == hash && p.type == AnimatorControllerParameterType.Float)
                    return true;
            }
            return false;
        }

        private bool HasIntParameter(int hash)
        {
            if (animator?.runtimeAnimatorController == null) return false;
            for (int i = 0; i < animator.parameterCount; i++)
            {
                var p = animator.GetParameter(i);
                if (p.nameHash == hash && p.type == AnimatorControllerParameterType.Int)
                    return true;
            }
            return false;
        }
    }
}
