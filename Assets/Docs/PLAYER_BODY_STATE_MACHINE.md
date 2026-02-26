# Player Body State Machine

The player uses a **body state machine** for status effects: Standing, Ragdoll, Stunned, Frozen (and more later).

## States

| State      | Behavior |
|-----------|----------|
| **Standing** | Normal movement, camera, and input. |
| **Ragdoll**  | Physics ragdoll, third-person follow camera, timer then auto-recovery. |
| **Stunned**  | Movement disabled (same component list as ragdoll). Auto-returns to Standing after `defaultStunnedSeconds` (or custom duration). |
| **Frozen**   | Same as Stunned; duration controlled by `defaultFrozenSeconds`. |

## Flow

- **Knock** (K key or `KnockRpc`) → if state machine + ragdoll are present, calls `PlayerBodyStateMachine.EnterRagdoll(impulse, duration)`. Otherwise uses legacy timer in `KnockableCapsule`.
- **Ragdoll** runs its timer (ground contact + duration or `maxRagdollSeconds`), then transitions to **Standing** and runs recovery (snap root, SetStanding).
- **Stunned / Frozen**: call `EnterStunned(seconds)` or `EnterFrozen(seconds)` from gameplay (e.g. combat, traps). They disable movement and auto-return to Standing when the timer expires.

## Triggering from code

```csharp
var stateMachine = player.GetComponent<PlayerBodyStateMachine>();
if (stateMachine != null)
{
    stateMachine.EnterRagdoll(impulse, 4f);   // knock into ragdoll
    stateMachine.EnterStunned(2f);            // stun for 2 seconds
    stateMachine.EnterFrozen(3f);              // freeze for 3 seconds
    stateMachine.EnterStanding();             // force back to standing
}
```

## Events

- **OnStateChanged(newState, previousState)** – use for UI, camera, or other reactions.
- **IsMovementDisabled** – true when state is not Standing.
- **IsRagdoll** – true when state is Ragdoll.

Camera and “disable while knocked” list are still driven by the existing ragdoll/knock flow (RagdollColliderSwitch + KnockableCapsule), so no change needed there.

## Adding new states

1. Add an enum value to `PlayerBodyStateMachine.BodyState`.
2. In **EnterState**: when entering the new state, disable movement (or do custom logic) and set any timer.
3. In **ExitState**: when leaving the new state, re-enable movement if needed.
4. In **Update** (or a timer): when the new state’s duration expires, call `EnterStanding()`.

Optional: add a public `EnterNewState(duration)` and wire it from gameplay.

## Prefab

The **Player** prefab has **PlayerBodyStateMachine** added. Ragdoll duration is configured on the state machine (default 4 s on ground, max 8 s). If you remove the component, knock/ragdoll falls back to the legacy path in `KnockableCapsule`.
