# Spinning Knock Trap

A hazard that spins and knocks players into ragdoll when they walk into it.

## Why player-side detection

The player uses a **CharacterController**, which does not trigger **OnTriggerEnter** or **OnCollisionEnter** on other objects. So the trap never sees the collision. Instead, the **player** detects the hit via **OnControllerColliderHit** and requests the knock.

## Setup

### 1. Trap

- Create a GameObject (e.g. spinning blade or pillar).
- Add the **Spinning Knock Trap** component.
- Set **Detection Radius** so the server checks every FixedUpdate whether any player is inside that sphere. This avoids clipping/tunneling when the trap spins fast (no single-frame collision miss).
- Configure **Spin Speed**, **Knock Impulse Magnitude**, **Knock Upward Bias**, **Knock Duration**, **Per Player Cooldown**, etc.

### 2. Player

- On the **Player** prefab, add the **Trap Knock On Contact** component (same GameObject as CharacterController and KnockableCapsule).
- Set **Cooldown Per Trap Seconds** (e.g. 1) so the same trap doesn’t knock every frame while the player is touching it.

### 3. Multiplayer

- When the player hits the trap, they call their own **KnockRpc** (ServerRpc), so the server applies the ragdoll and all clients stay in sync. The trap does not need a NetworkObject.

## Notes

- The player must have **KnockableCapsule** (and your ragdoll/state machine) for the knock to work.
- For a visible spin, assign **Spin Pivot** to a child transform that has the mesh.
- To avoid lag, use a simple collider (Box/Capsule/Sphere) on the trap and do not use a non-kinematic Rigidbody on the spinning object.
