# Locomotion Animator: Idle, Walk, Run

Use **PlayerLocomotionAnimator** plus one **Float** parameter in your Animator Controller to drive Idle → Walk → Run (and back) from movement.

---

## Synty AC_Polygon controllers

For **Synty Animation Base Locomotion** (AC_Polygon_Masculine etc.):

- **Speed Param Name**: `MoveSpeed`.
- **IsGrounded** and **IsJumping** are driven automatically. Ensure these Animator params exist.
- **Apply Root Motion**: OFF.
- **Note (walk blend)**: If the character looks like it’s floating, adjust the character child’s local Y position so its feet align with the CharacterController bottom. Try small steps (e.g. −0.1 to −0.2) or inspect the rig’s pivot (pelvis vs feet).

---

## 0. Apply Root Motion — turn it OFF

If your character **drifts, walks away from the camera, or looks offset**, the Animator is probably moving the root. With **CharacterController** movement you must **disable Apply Root Motion**:

1. Select your **Player** (or the GameObject that has the **Animator**).
2. In the **Animator** component, **uncheck** **Apply Root Motion**.

Movement is driven only by **FirstPersonMotor** (CharacterController). The Animator should only play Idle/Walk/Run for visuals, not move the character.

---

## 1. Add the script

1. Select your **Player** prefab (root).
2. **Add Component** → **Player Locomotion Animator** (`DungeonGame.Player.PlayerLocomotionAnimator`).
3. Leave **Animator** empty to auto-find the one on your character, or drag the Animator from a child.
4. Set **Walk Speed** and **Sprint Speed** to match your **FirstPersonMotor** (default 5 and 7.5). These are used to normalize the **Speed** parameter (0 = idle, ~0.67 at walk, 1 at sprint).
5. **Speed Param Name** should be `Speed` (default). This must match the parameter you add in the Animator Controller.
6. **Use Input For Speed** (default on): Speed is driven by key press so animation starts/stops with input; turn off to use velocity (slight delay).

---

## 2. Animator Controller: parameter

1. Open your character’s **Animator Controller** (double-click it in the Project window).
2. In the **Parameters** panel (left), click **+** → **Float**.
3. Name it **Speed** (same as the script’s **Speed Param Name**).

---

## 3. Animator Controller: states

Create three motion states:

1. **Idle**  
   - Right-click in the grid → **Create State** → **Empty** (or use your idle clip).  
   - Rename to `Idle`.  
   - If you created Empty, assign your **Idle** animation clip in the Inspector.

2. **Walk**  
   - Create another state, name it `Walk`, assign your **Walk** animation clip.

3. **Run**  
   - Create another state, name it `Run`, assign your **Run** animation clip.

Set **Idle** as the **Entry** state (right-click Idle → **Set as Layer Default State** if it isn’t already).

---

## 4. Transitions (Idle ↔ Walk ↔ Run)

### Idle → Walk

1. Right-click **Idle** → **Make Transition** → click **Walk**.
2. Select the transition arrow (Idle → Walk).
3. In the Inspector, **Conditions**: click **+** → choose **Speed** → **Greater** → `0.1`.
4. Uncheck **Has Exit Time** (so it switches as soon as you move).
5. **Transition Duration**: e.g. `0.1`–`0.2` for a quick blend.

### Walk → Idle

1. **Walk** → **Make Transition** → **Idle**.
2. Condition: **Speed** **Less** `0.1`.
3. Uncheck **Has Exit Time**.
4. Duration: e.g. `0.15`.

### Walk → Run

1. **Walk** → **Make Transition** → **Run**.
2. Condition: **Speed** **Greater** `0.6` (or `0.65` if you want run to kick in a bit later).
3. Uncheck **Has Exit Time**.
4. Duration: e.g. `0.1`.

### Run → Walk

1. **Run** → **Make Transition** → **Walk**.
2. Condition: **Speed** **Less** `0.6` (same threshold).
3. Uncheck **Has Exit Time**.
4. Duration: e.g. `0.1`.

### Run → Idle (optional)

You can go Run → Walk → Idle with the above, or add a direct **Run** → **Idle** with **Speed** **Less** `0.1` for a faster stop.

---

## 5. Threshold summary

| State | Condition        | Meaning              |
|-------|------------------|----------------------|
| Idle  | Speed &lt; 0.1   | Stopped or nearly so |
| Walk  | 0.1 ≤ Speed &lt; 0.6 | Moving, not sprinting |
| Run   | Speed ≥ 0.6      | Sprinting            |

If your walk feels too early/late, change **Walk → Idle** and **Idle → Walk** to use `0.05`–`0.15`. If run kicks in too early/late, change `0.6` to something like `0.55` or `0.7`.

---

## 6. Quick check

- **Player** root has **Player Locomotion Animator** (and **FirstPersonMotor**).
- **Animator Controller** has a **Speed** (Float) parameter and three states: **Idle**, **Walk**, **Run**.
- Transitions use **Speed** with the conditions above and **Has Exit Time** off.

In Play mode, moving should blend Idle → Walk → Run; releasing input should blend back to Idle.

**Idle “finishes” or glides before walk/run:** In the Animator Controller, shorten **Transition Duration** on Idle → Walk and Walk → Run (e.g. 0.05–0.15 s) and uncheck **Has Exit Time** so transitions happen as soon as the Speed condition is met.

---

## 7. State machine and movement

If **PlayerBodyStateMachine** is on the player, locomotion is only driven when the body state is **Standing**. When **Stunned**, **Frozen**, or **Ragdoll**, Speed is forced to 0 so you don’t play walk/run while disabled.

---

## 8. Fall animation and hard-landing ragdoll

**PlayerFallDetection** (add to the Player) does two things:

1. **Fall animation**  
   After the player has been in the air for a short time (default 0.35 s), it sets the Animator Bool **IsFalling** to true so you can play a fall animation. Add a **Bool** parameter `IsFalling` in your Animator and a **Fall** state with transitions: e.g. Any State → Fall when **IsFalling** is true, Fall → Idle when **IsFalling** is false.

2. **Ragdoll on hard landing (instead of fall damage)**  
   If the player is in the air for at least **Fall Ragdoll Threshold Seconds** (default 1.2 s) and then lands, they go into **Ragdoll** instead of taking damage. Configure **Landing Ragdoll Impulse** (e.g. slight down + forward) and **Landing Ragdoll Duration** on the component.

### Animator setup for fall

1. In the Animator Controller, add a **Bool** parameter named **IsFalling**.
2. Create a **Fall** state and assign your fall animation clip.
3. **Any State** → **Fall**: condition **IsFalling** **true**; uncheck **Has Exit Time**.
4. **Fall** → **Idle** (or **Land**): condition **IsFalling** **false**; uncheck **Has Exit Time**; short duration (e.g. 0.1).

If you don’t add the **IsFalling** parameter, the script skips setting it (no errors). Hard-landing ragdoll still works.
