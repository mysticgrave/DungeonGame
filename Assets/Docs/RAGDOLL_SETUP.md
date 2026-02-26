# Ragdoll Setup (Polygon Character)

Use the **Ragdoll Builder** to add physics (Rigidbody + CapsuleCollider + CharacterJoint) to the base character model so you can use a real skeletal ragdoll for knockdown/death.

---

## 1. Pick your base character

Use a prefab that has the **Polygon Characters** skeleton (same bone names as `Characters.fbx`), e.g.:

- **Character_Hero_Knight_Male_FixedScale** or **Character_Hero_Knight_Female_FixedScale**  
  (`Assets/PolygonDungeon/Prefabs/Characters/FixedScale/`)

These use the shared rig: **Hips**, **Spine_01/02/03**, **UpperLeg_L/R**, **LowerLeg_L/R**, **Shoulder_L/R**, **Elbow_L/R**, **Hand_L/R**, **Neck**, **Head**.

---

## 2. Build the ragdoll

1. Open the character prefab (double‑click to enter prefab edit).
2. In the **Hierarchy**, select the **root** of the prefab (the top object that contains the whole skeleton and meshes).  
   For the FixedScale prefabs this is the root with many children (Hips, Character_Hero_Knight_Male, etc.).
3. Menu: **Tools → DungeonGame → Build Ragdoll (Polygon Character)**.
4. The script will find the bones by name and add:
   - **Rigidbody** (mass 2, kinematic by default)
   - **CapsuleCollider** (small pill per bone)
   - **CharacterJoint** (connected to parent bone)
5. If you have **Assets/Materials/Physics/RagdollFriction.physicMaterial**, it is assigned to the colliders.
6. **Save the prefab** (Ctrl+S).

---

## 3. Tweak (optional)

- **Radius:** In `RagdollBuilder.cs`, change `CapsuleRadius` (default `0.08`) and re-run, or adjust each bone’s CapsuleCollider in the Inspector.
- **Mass:** Change `MassPerBone` (default `2`) or set each Rigidbody’s mass.
- **Joint limits:** Select a bone with a CharacterJoint and use the **Angular Limits** in the Inspector to reduce stretching.

---

## 4. Using the ragdoll

- **All Rigidbodies start kinematic** so the character doesn’t collapse in the editor. At runtime you can:
  - **Enable ragdoll:** Set the root (e.g. Hips) Rigidbody to **Is Kinematic = false** and optionally disable the **Animator** (and movement). Apply force/torque if you want a “knock back” effect.
  - **Disable ragdoll:** Set all Rigidbodies back to kinematic and re-enable Animator/movement.
- For **death only:** Duplicate the character prefab into a “RagdollCorpse” variant: remove movement/input, keep Animator disabled, set all Rigidbodies non-kinematic, and spawn this when the player dies.
- Your existing **KnockableCapsule** is a simple capsule knockdown; you can later switch to this skeletal ragdoll for the same trigger (e.g. when HP &lt;= 0 or when hit by a knockback).

---

## 5. If bones are not found

The builder looks for these exact names under the selected root:

**Hips**, **Spine_01**, **Spine_02**, **Spine_03**, **UpperLeg_L**, **UpperLeg_R**, **LowerLeg_L**, **LowerLeg_R**, **Shoulder_L**, **Shoulder_R**, **Elbow_L**, **Elbow_R**, **Hand_L**, **Hand_R**, **Neck**, **Head**.

If your model uses different names (e.g. another pack), either rename the bones in the source asset or duplicate `RagdollBuilder.cs` and change the `PolygonCharacterBones` array to match your skeleton.
