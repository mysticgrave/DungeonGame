# Synty Animation Sword Combat Setup

From `ANIMATION_SwordCombat_UserGuide.pdf`. Use this with **AC_Polygon_Masculine** (Base Locomotion) and Polygon characters.

---

## 1. Prop Bone (required for proper sword pose)

Sword Combat uses a **Prop_R** bone under Hand_R for correct weapon orientation. Characters need this bone.

### Option A: Use Prop Bone Binder Tool (older / custom rigs)

1. Select your character (prefab or scene instance).
2. Menu: **Synty** → **Tools** → **Animation** → **Setup Prop Bones**.
3. Attach your sword to **Prop_R_Socket** under `Hand_R` in the skeleton.

**Path for Prop_R:**  
`Armature/Hips/Spine_01/Spine_02/Spine_03/Clavicle_R/Shoulder_R/Elbow_R/Hand_R/Prop_R`

If your rig doesn’t have this path, animations won’t drive the prop correctly.

### Option B: Use Sword Combat character (has Prop_R built-in)

If your character has no prop bone, replace with the Sword Combat character:

1. Unpack the Base Locomotion player prefab.
2. Delete `PolygonSyntyCharacterMesh` and `Root` under `PolygonSyntyCharacter`.
3. Drag `PolygonSyntyCharacter` from Sword Combat into the scene.
4. Unpack it, then drag `PolygonSyntyCharacterMesh` and `Root` into the player.
5. Set the Animator **Avatar** to `PolygonSyntyCharacterAvatar` from the Sword Combat pack.

---

## 2. Add sword hold to Base Locomotion (animation layers)

Use override layers so the character holds a sword while keeping locomotion:

1. Open **AC_Polygon_Masculine** (or your locomotion controller).
2. Create two layers: **SwordArm** and **SwordHand**.
3. Set both to **Override** blending.
4. Weight: **SwordArm** ≈ 0.6–0.7, **SwordHand** = 1.
5. Create **Mask_Arm_R**: enable only right arm in the AvatarMask.
6. Create **Mask_Hand_R**: enable only right hand.
7. Apply Mask_Arm_R to SwordArm, Mask_Hand_R to SwordHand.
8. Set the default state for both layers to **A_Idle_Base_Sword**  
   (`Assets/Synty/AnimationSwordCombat/Animations/Polygon/Idle/Base/`).
9. Parent the sword to the **Prop_R** bone (or Prop_R_Socket).

Result: character holds the sword; hand pose is fully overridden, arm is partially blended with locomotion.

---

## 3. Combat animations (attacks, blocks, etc.)

For attacks/blocks/parries:

- Add new states and transitions to the Animator Controller.
- Use clips from `Assets/Synty/AnimationSwordCombat/Animations/Polygon/`.
- **AC_Polygon_Masculine** includes:
  - Parameter: `attack_sword_01` (Trigger) — used by WeaponController.
  - Any State → `A_Attack_LightCombo01A_Sword` when trigger fires.
  - Attack → Grounded/locomotion on exit (Has Exit Time ~0.69).

When playing full-body combat animations, disable or override the SwordArm/SwordHand layer overrides so the combat clip drives the whole body.

**SwordCombatLayerBlender** (on Player): Automatically sets SwordArm/SwordHand weight to 0 during attack states so the full-body attack drives the arm. Add the component and assign the Animator (or leave null to auto-find).

### Attack step (without root motion)

When the attack animation shows a lunge/step but root motion is off, use **Animation Events** to drive a scripted step:

1. Open your attack clip (e.g. `A_Attack_LightCombo01A_Sword` or one of its sub-clips: WindUp, Hit, FollowThrough).
2. Scrub to the frame where the character’s foot pushes off or the lunge happens.
3. Add **Animation Event**: right-click the clip timeline → Add Animation Event.
4. In the Inspector, set **Function** to `ApplyAttackStep` (no arguments).
5. **AttackStepReceiver** is added automatically to the Animator’s GameObject. Tune **Lunge Distance** on it (default 0.4) to match the animation.

---

## 4. Avatar (Polygon finger mapping)

For Synty Polygon, the third “mitt” finger must map to **Middle**, not Little:

1. Select the character mesh → Rig tab → **Configure…**.
2. In the hand mapping, map the third finger to **Middle**.
3. Apply and save.

---

## 5. Animations without Prop_R

You can parent the sword to **Hand_R** and skip Prop_R, but some Sword Combat animations (stab, flourish, draw/sheathe) will look wrong. Animations that depend on Prop_R include:

- A_Attack_LightCombo01C_Sword  
- A_Attack_LightFencing01_Sword  
- A_Attack_HeavyStab01_Sword  
- A_Death_R_01_Sword  
- A_Draw_Sword / A_Sheathe_Sword  
- A_Idle_Flourish01_Sword  

---

## 6. Integration with our WeaponController

The Synty Sword Combat pack handles **animation** only. Our **WeaponController** handles:

- Damage, range, cooldown
- Overlap/sphere hit detection
- Config (WeaponConfig asset)

Wire them together by:

1. Adding Sword Combat attack clips to the Animator (e.g. via a trigger or state).
2. Keeping **WeaponController** on the Player for hit detection and damage.
3. Using **Weapon Bone Attach** = Prop_R (or Prop_R_Socket) so the sword visual follows the animator.
4. Optionally using Animation Events to time damage windows with attack phases (WindUp → Hit → FollowThrough).

---

## 7. Gallery / samples

- Scenes: `Assets/Synty/AnimationSwordCombat/Samples/Scenes/`
- Sword prefab: `Assets/Synty/AnimationSwordCombat/Samples/Prefabs/Wep_Sword_01.prefab`
- Example masking: `BaseLocomotionMasking.controller`
