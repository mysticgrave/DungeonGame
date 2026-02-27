# Weapon setup: basic sword, animation, and hitting enemies

This walks through setting up a basic sword attack with **WeaponConfig**, a **separate sword** object, **animation**, and **multiple swords** to choose from later.

---

## 1. Create a WeaponConfig for the sword

1. In the Project window: **Right-click** → **Create** → **DungeonGame** → **Weapon Config**.
2. Name it (e.g. `BasicSword`).
3. Set the fields:
   - **Weapon Id**: `basic_sword` (used by loadout/registry).
   - **Display Name**: `Basic Sword`.
   - **Attack Type**: **Melee**.
   - **Damage**: e.g. `3`.
   - **Range**: e.g. `2.5` (world units in front of the player).
   - **Cooldown**: e.g. `0.6` (seconds between swings).
   - **Hit Radius**: e.g. `0.5` (radius of the overlap sphere; tune so the sword “reaches” the enemy).

Save the asset. You can duplicate it later for different swords (e.g. `FireSword`, `IronSword`) and change damage/range/cooldown per asset.

---

## 2. Assign the config to the player

1. Select your **Player** prefab.
2. Find the **Weapon Controller** component.
3. Assign your sword config to **Config Fallback**: drag the `BasicSword` (or other) WeaponConfig asset into the field.

Now left-click uses that config. If you use **classes** (ClassDefinition) or **MetaProgression** (equipped weapon), the controller will use that config instead when set; otherwise it uses Config Fallback.

---

## 3. Sword as a separate object (dual display: FPS arms + hand bone)

The **sword model** is only for display. The actual hit is done by **WeaponController** with an overlap sphere (no need for the sword collider to touch the enemy). The game uses a **dual display** so the local player sees the sword in their FPS arms, while other players see it on the character’s hand.

### FPS arms (local player)

1. Create layer **`FPSArms`** in **Tags & Layers** (Edit → Project Settings → Tags and Layers).
2. Create an **FPS arms prefab** (hands/forearms mesh; placeholder capsule/cube is fine for testing).
3. Add a child empty named **`WeaponMount`** where the sword will attach. Put the prefab on the **FPSArms** layer.
4. On the **Player** prefab, add **FPSArmsController** and assign your FPS arms prefab to **Fps Arms Prefab**.
5. The camera is created at runtime, so the arms are instantiated as a child of the camera when the player spawns.

### Weapon visual (WeaponController)

1. Add your **sword mesh** as a child of the Player (e.g. `Player > Sword`). Assign this transform to **Weapon Controller → Weapon Visual**.
2. Assign the **right-hand bone** on your character rig (e.g. `Armature/Hips/Spine/Chest/UpperArm/ LowerArm/Hand`) to **Weapon Controller → Weapon Bone Attach**.
3. WeaponController will parent the weapon visual to:
   - **FPSArmsController.FPSWeaponMount** for the owner (in front of their view).
   - **Weapon Bone Attach** for other players (on the hand in third-person).

### Attack origin (optional)

WeaponController needs an **attack origin** (point and direction for the overlap sphere). By default it uses **Camera.main** (the runtime camera). To make the hit feel like it comes from the sword, assign **Weapon Controller → Attack Origin** to a point in front of the player or under the sword.

---

## 4. Animation (sword swing)

1. **Animator**
   - Use the same **Animator** that drives your player (or the arm/camera that holds the sword).
   - Add a **trigger** parameter (e.g. `attack_sword_01`) in Animator window → Parameters → + → Trigger.

2. **Attack clip**
   - Create or import a clip (e.g. “SwordSwing”): a short swing (e.g. 0.2–0.4 s).
   - In the **Animator**, add a state for this clip (e.g. “SwordAttack”) and a **Any State → attack (or Idle/Walk/Run → attack)** with:
     - **Condition**: your trigger (e.g. `attack_sword_01`).
     - **Has Exit Time**: off (so it plays once and exits).
   - Add a transition **back to Idle** (or locomotion) when the attack state finishes (e.g. “SwordAttack” → “Idle” with exit time or a short duration).

3. **Hook up to WeaponController**
   - On the **Player** prefab, in **Weapon Controller**: assign **Animator** (the one with the attack trigger) and set **Attack Trigger Name** to match (e.g. `attack_sword_01`).
   - On left-click, the controller fires the trigger and runs the server attack.

You can add **Animation Events** on the swing clip for sound or VFX; damage is already handled by WeaponController on the server.

---

## 5. Hitting an enemy (how it works)

- **Melee** uses **Physics.OverlapSphere**:
  - **Center**: `attackOrigin.position + attackOrigin.forward * (range * 0.5f)` (in front of the player).
  - **Radius**: `hitRadius` from the config (or default).
- Any **Collider** inside that sphere is considered. The controller looks for **NetworkHealth** on that collider or its parents.
- If it finds **NetworkHealth** and it’s **not** a player object, it calls **TakeDamage(damage)** on the server.
- So for an enemy to be hit:
  1. It (or a child) has a **Collider** (e.g. Capsule, Box).
  2. It has a **NetworkHealth** component (or a child with it).
  3. It’s within **range** and **hit radius** of the attack origin when you swing.

No need for the sword GameObject to have a collider for damage; the overlap sphere is independent of the mesh.

---

## 6. Different swords to pick from later

- **One config = one “weapon type”.**  
  Create more WeaponConfig assets (e.g. `IronSword`, `FireSword`) with different damage/range/cooldown. Each is a separate asset.

- **Who chooses the sword**
  - **Class**: Assign a different **Default Weapon** (WeaponConfig) per **ClassDefinition**. The player’s class then decides which sword config is used.
  - **Loadout / MetaProgression**: Register your configs in **WeaponRegistry** and use **MetaProgression** “equipped weapon” so the player picks one of several swords (or bows, magic) in the UI. WeaponController already uses “equipped unlock” and “class default” before Config Fallback.

- **Visual swap (optional)**  
  To show a different sword model per weapon:
  - Have multiple sword GameObjects (e.g. under the camera or hand), one per weapon type.
  - Enable the one that matches the current **WeaponController.Config** (e.g. by `weaponId` or display name) and disable the others. You can do this in a small script that runs when the config changes (e.g. in `OnNetworkSpawn` and when loadout changes), or from your loadout UI.

---

## 7. Upper body follows camera (for other players)

Add **UpperBodyLookSync** to the Player prefab and assign the **Spine** or **Chest** bone from your humanoid rig. Other players will see the character's upper body tilt when you look up or down.

---

## Quick checklist

| Step | What to do |
|------|------------|
| Layers | Create `FPSArms` and `PlayerLocalCull` in Tags & Layers. |
| FPS arms | Create FPS arms prefab with `WeaponMount` child; add FPSArmsController to Player and assign the prefab. |
| Config | Create WeaponConfig asset, set Attack Type = Melee, damage/range/cooldown/hit radius. |
| Player | Assign config to Weapon Controller Config Fallback. Assign Weapon Visual (sword) and Weapon Bone Attach (hand bone). |
| Upper body | Add UpperBodyLookSync, assign Spine/Chest bone. |
| Animation | Animator has attack trigger (e.g. `attack_sword_01`); add swing state and transitions. Assign Animator and Attack Trigger Name to Weapon Controller. |
| Enemy | Enemy (or child) has Collider + NetworkHealth; no extra setup for receiving the hit. |
| More swords | Duplicate WeaponConfig assets; assign per class or via WeaponRegistry + MetaProgression; optionally swap visible sword by config. |
