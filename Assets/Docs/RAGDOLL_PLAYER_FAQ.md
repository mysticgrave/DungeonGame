# Ragdoll + Player: Capsule, visibility, Animator

## One set of colliders at a time (recommended)

Use **RagdollColliderSwitch** so the root capsule and ragdoll bone colliders never fight:

- **Standing:** Root **CapsuleCollider** enabled (so you don’t fall through the floor); all ragdoll bone colliders **disabled** and bone Rigidbodies **kinematic**. Animator on.
- **Ragdoll (knocked):** Root CapsuleCollider **disabled**; ragdoll bone colliders **enabled** and bone Rigidbodies **non-kinematic**. Impulse is applied to the ragdoll root (e.g. Hips). Animator off.

**Setup:** Add `RagdollColliderSwitch` to the player root. Assign **Ragdoll Root** to the Hips (or parent of all ragdoll bones). Optionally assign **Animator**. `KnockableCapsule` will call `SetRagdoll(impulse)` on knock and `SetStanding()` + `SnapRootToRagdoll()` on recovery. The camera rig uses **Follow Target When Knocked** (auto-set from Ragdoll Root when present) so the camera follows the body (Hips) instead of the root while ragdolling.

---

## Do I need the CapsuleCollider on the player root?

**It depends how you use knockdown:**

- **If you use the full skeletal ragdoll** (Rigidbodies on bones) for knockdown/death:  
  You **do not** need a CapsuleCollider on the root. The bone colliders handle physics. You can **remove** the root CapsuleCollider. You can also **remove or disable** `KnockableCapsule` and drive the ragdoll yourself (disable Animator, set bone Rigidbodies to non-kinematic, apply force).

- **If you keep the simple capsule knockdown** (`KnockableCapsule`):  
  You **do** need the root **CapsuleCollider** and **Rigidbody** — that component uses them when knocked. In that case the **skeletal ragdoll Rigidbodies should stay kinematic** so they don’t fight the Animator. The capsule is only enabled while knocked.

So: **skeletal ragdoll only** → no root capsule, no KnockableCapsule. **Capsule knockdown only** → keep root capsule + KnockableCapsule. **Both** → use **RagdollColliderSwitch** (see above) so only one set of colliders is active at a time.

---

## I can’t see the player model when I host

Check these in the **Player prefab**:

1. **Mesh GameObject is active**  
   The object that has the **Skinned Mesh Renderer** (the character body) must have its **checkbox enabled** in the Inspector (top-left). If the ragdoll wizard or a parent was disabled, the mesh won’t show.

2. **Skinned Mesh Renderer is enabled**  
   On that same object, the **Skinned Mesh Renderer** component must be **enabled** (checkbox on the component).

3. **Animator**  
   The **Animator** (usually on the same object as the mesh or on the root) should be **enabled**, with a **Controller** and **Avatar** assigned. If the Animator is disabled, the character can still be visible (default pose). If the **mesh is a child of a bone** that’s under an inactive object, the mesh won’t show — so ensure the whole hierarchy from root down to the mesh is active.

4. **Ragdoll wizard**  
   If you used **GameObject → 3D Object → Ragdoll**, Unity may have created a **Ragdoll** parent or changed the hierarchy. Make sure the **original character mesh** (Skinned Mesh Renderer) is still under an **active** GameObject and not disabled by the wizard.

5. **Layer / camera**  
   The mesh’s **Layer** must be one that your **Main Camera** renders (e.g. **Default**). Check the camera’s **Culling Mask**.

---

## Does the Animator cause problems with the ragdoll?

No, as long as you use them in the right mode:

- **Standing / moving:**  
  **Animator** drives the bones. All **ragdoll Rigidbodies** should be **Is Kinematic = true** so they don’t fight the animation.

- **Ragdolling (knockdown / death):**  
  **Disable the Animator** and set the ragdoll Rigidbodies (at least the root, e.g. Hips) to **Is Kinematic = false** so physics drives the skeleton.

If the Animator and non-kinematic Rigidbodies run at the same time, they will fight and the character can stretch or disappear. So: one or the other at a time.

---

## Quick checklist for “model not visible”

- [ ] Player prefab: mesh GameObject **active**.
- [ ] Skinned Mesh Renderer **enabled**.
- [ ] Animator **enabled**, Controller and Avatar assigned.
- [ ] No parent of the mesh is **inactive**.
- [ ] All ragdoll Rigidbodies **kinematic** when you’re not ragdolling.
- [ ] Mesh **Layer** is in the camera **Culling Mask**.
