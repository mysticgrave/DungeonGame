# Synty AC_Polygon Animation Setup

The Player uses **AC_Polygon_Masculine.controller**. PlayerLocomotionAnimator drives these parameters:

| Parameter        | Type  | What We Set |
|------------------|-------|-------------|
| MoveSpeed        | Float | 0–7 m/s (smoothed) |
| StrafeDirectionX | Float | -1 to +1 (left/right, smoothed) |
| StrafeDirectionZ | Float | -1 to +1 (back/forward, smoothed) |
| IsStrafing       | Float | 1 (FPS strafe mode) |
| IsGrounded       | Bool  | CharacterController.isGrounded |
| IsJumping        | Bool  | Ascending in air |
| IsStopped        | Bool  | No movement |
| IsWalking        | Bool  | Moving, not sprinting |
| CurrentGait      | Int   | 0=Idle, 1=Walk, 2=Run, 3=Sprint |
| MovementInputHeld | Bool | True after holding input > 0.15s (matches SamplePlayerAnimationController) |
| MovementInputPressed | Bool | True 0–0.15s after first input (mutually exclusive with Tapped/Held) |
| MovementInputTapped | Bool | True only on first frame of input |
| ForwardStrafe | Float | 1 = forward tree, 0 = backward/lateral (picks Walk_StrafeForwards vs Backwards) |
| IsTurningInPlace | Bool | True when standing still but rotating (look around) |
| CameraRotationOffset | Float | Yaw delta in degrees when turning in place |
| ShuffleDirectionX/Z | Float | Set on MovementInputTapped for shuffle start direction |
| IsStarting | Bool | True when starting from idle (first 0.2s) |

Set **Use Normalized Speed** OFF for Synty (passes raw 0–7 m/s).

## Physics vs CharacterController

**CharacterController** (current) is recommended for player movement in physics-heavy games:

- **CharacterController**: Predictable movement, no bouncing/sliding, easy slope handling. Physics is used for *interactions* (raycasts, overlaps, ragdolls, moving platforms). Most FPS games use this.
- **Rigidbody**: Player is a physics object. Good if you want knockback, forces, riding platforms physically. Harder to get tight FPS feel, more tuning.

Keep CharacterController for the player. Physics (Rigidbodies) handle world objects, ragdolls, traps. You can switch later if you need pushable players or similar.

## Attack Animation

AC_Polygon_Masculine has **no attack trigger**. To add attack:

1. Duplicate the controller or add an override layer.
2. Add a Trigger parameter (e.g. `attack_sword_01`).
3. Add an Attack state and transitions: Any State → Attack when trigger, Attack → Idle on exit.

Or use a separate Animator for FPS arms and assign WeaponController to that (arms show attack, body stays locomotion).

## WeaponController

WeaponController only calls `SetTrigger` if the trigger exists. If the controller has no attack trigger, attacks still work (melee logic runs) but no attack animation plays.
