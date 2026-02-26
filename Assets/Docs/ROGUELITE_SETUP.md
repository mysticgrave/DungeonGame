# Roguelite Setup — Step-by-Step

Follow in order. You need **Town**, **Spire_Slice**, **Player.prefab**, and a **RunState** prefab. For full RunState setup (what goes on the prefab, bootstrap, prefab list), see **RUNSTATE_SETUP.md**.

---

## Step 1: Meta progression (gold & EXP)

1. Open the **Town** scene.
2. In the Hierarchy: **Right-click → Create Empty**. Name it `MetaProgression`.
3. With `MetaProgression` selected, in the Inspector click **Add Component**.
4. Search for `Meta Progression` and add **Meta Progression** (script `DungeonGame.Meta.MetaProgression`).
5. Save the scene (Ctrl+S).

No fields to assign. Gold and class EXP will persist via PlayerPrefs.

---

## Step 2: Class definitions (ScriptableObjects)

1. In the Project window, go to **Assets** (or **Assets/ScriptableObjects** if you use that folder).
2. **Right-click → Create → DungeonGame → Class Definition**. Name it `Warrior`.
3. Select `Warrior` and in the Inspector set:
   - **Class Id**: `warrior`
   - **Display Name**: `Warrior`
   - **Description**: (optional) e.g. `Melee fighter with high HP.`
   - **Base Max Hp**: e.g. `12`
   - **Base Move Speed**: e.g. `5`
   - **Base Sprint Speed**: e.g. `7.5`
   - **Default Weapon**: (assign in Step 5) drag your Sword weapon config here so this class gets that weapon.
   - **Preferred Weapon Type**: e.g. `melee`
4. Create a second class: **Right-click → Create → DungeonGame → Class Definition**. Name it `Mage`.
5. Set **Class Id** to `mage`, **Display Name** to `Mage`, **Base Max Hp** to `8`, **Default Weapon** to your Pistol (after you create it), **Preferred Weapon Type** to `ranged`.

---

## Step 3: Class registry (Town)

1. Stay in the **Town** scene.
2. **Create Empty** in the Hierarchy. Name it `ClassRegistry`.
3. **Add Component** → search **Class Registry** → add **Class Registry** (`DungeonGame.Classes.ClassRegistry`).
4. In the **Classes** list set **Size** to `2` (or how many classes you have).
5. Drag **Warrior** into **Element 0** and **Mage** into **Element 1**. Order matters: index 0 = Warrior, index 1 = Mage for networking.
6. Save the scene.

---

## Step 4: Player class (Player prefab)

1. In Project, open **Assets/Prefabs/Player.prefab** (double-click to enter prefab edit).
2. Select the **root** of the prefab (the top object).
3. **Add Component** → **Player Class** (`DungeonGame.Classes.PlayerClass`).
4. In **Default Class** drag your **Warrior** Class Definition (or whichever class new players should get).
5. Save the prefab (Ctrl+S) and exit prefab edit if you want.

---

## Step 5: Weapon configs (ScriptableObjects)

1. In Project, **Right-click → Create → DungeonGame → Weapon Config**. Name it `Sword`.
2. Select `Sword`. Set:
   - **Weapon Id**: `sword`
   - **Display Name**: `Sword`
   - **Attack Type**: `Melee`
   - **Damage**: e.g. `2`
   - **Range**: e.g. `2.5`
   - **Cooldown**: e.g. `0.6`
   - **Hit Radius**: e.g. `0.5`
   - **Unlock Cost Gold**: `0` (starter weapon; no cost to unlock).
3. Create another: **Create → DungeonGame → Weapon Config**. Name it `Pistol`.
4. Set **Weapon Id** `pistol`, **Attack Type** `Ranged`, **Damage** `1`, **Range** `12`, **Cooldown** `0.4`, **Hit Layers** = Everything. Set **Unlock Cost Gold** to e.g. `50` so players can buy it with gold in town.
5. Go back to your **Warrior** and **Mage** Class Definitions and set **Default Weapon** to Sword and Pistol respectively.

---

## Step 5b: Weapon registry (Town)

1. In **Town** scene, **Create Empty**. Name it `WeaponRegistry`.
2. **Add Component** → **Weapon Registry** (`DungeonGame.Weapons.WeaponRegistry`).
3. Set **Weapons** list **Size** to the number of weapon configs (e.g. 2). Drag **Sword** into Element 0 and **Pistol** into Element 1.
4. Save the scene.

---

## Step 6: Weapon on Player prefab

1. Open **Player.prefab** again.
2. Select the **root**.
3. **Add Component** → **Weapon Controller** (`DungeonGame.Weapons.WeaponController`).
4. **Config Fallback**: (optional) drag **Sword** here only if you want a fallback when the class has no default weapon and no equipped unlock. Usually leave empty — the weapon comes from the class or from the player’s equipped unlock.
5. **Attack Origin**: leave **empty**. The controller will auto-use the player’s camera (created on start) when available.
6. (Optional) Disable or remove **Debug Damage Raycaster** so only the weapon deals damage.
7. Save the prefab.

---

## Step 7: Run state and wipe

See **RUNSTATE_SETUP.md** for full RunState prefab setup. In short: the RunState prefab should have **Network Object**, **Spire Run State** (Town Scene Name = `Town`), **Run Debug Hotkeys**, and **Run Wipe Detector**. The RunState prefab must be in **DefaultNetworkPrefabs** (or your network prefab list), and in Town a GameObject (e.g. Bootstrap) must have **RunStateBootstrap** with **Run State Prefab** assigned.

---

## Step 8: Spire Heart prefab (victory condition)

1. In Project, **Right-click** in Prefabs → **Create Empty**. Name it `SpireHeart`.
2. Double-click to edit the prefab.
3. Add a **Capsule** (or any visual): **Add Component → Capsule Collider** (or 3D Object → Capsule). Resize/position so the heart is visible and hittable.
4. On the **root** of the prefab:
   - **Add Component** → **Network Object** (Netcode). If asked, do **not** make it the player prefab.
   - **Add Component** → **Network Health** (`DungeonGame.Combat.NetworkHealth`). Set **Max Hp** to e.g. `50`.
   - **Add Component** → **Spire Heart** (`DungeonGame.Spire.SpireHeart`). Leave **Run State** empty to auto-find, or drag the RunState prefab’s SpireRunState reference in at runtime (usually auto-find is enough).
5. Ensure the root has a **Collider** (the Capsule counts). Enemies/weapons need something to hit.
6. Save the prefab.

---

## Step 9: Add Spire Heart to the network prefab list

You **must** add the SpireHeart prefab to the list your NetworkManager uses (usually **DefaultNetworkPrefabs**). Without this, Netcode will not recognize the prefab.

**Detailed steps and troubleshooting:** see **SPIREHEART_NETWORK_PREFAB.md**.

Short version:
1. In the **Project** window, select the **DefaultNetworkPrefabs** asset (under Assets), not the NetworkManager in the Hierarchy.
2. In the **Inspector**, find the **List** of prefabs.
3. **Increase Size** by 1 (or click **+**).
4. Drag the **SpireHeart** prefab from the Project into the new list slot.
5. Save. If the slot won’t accept the prefab, ensure SpireHeart’s **root** has a **Network Object** component, and that you’re dragging the **prefab** from the Project, not a scene instance.

---

## Step 10: Place Spire Heart in the Spire (victory room)

1. Open the **Spire_Slice** scene.
2. Drag **SpireHeart** from the Project (Prefabs) into the Hierarchy. Put it in the room you want to be the **final room** (top of the spire). Position it where players will fight it.
3. If your spire is fully procedural, you may need to spawn the heart from code later; for now, placing it in the scene is enough to test victory.
4. Save the scene.

---

## Quick test checklist

- **Town**: Has `MetaProgression`, `ClassRegistry`, `WeaponRegistry`, and a Bootstrap with **RunStateBootstrap** (Run State Prefab assigned). RunState prefab is in DefaultNetworkPrefabs.
- **Player prefab**: Has `PlayerClass` (default class = Warrior, class has Default Weapon) and `WeaponController` (attack origin auto-found from camera; config from class or equipped unlock). Spire Heart is in the network prefab list.
- **Spire_Slice**: Spire Heart is in the scene (e.g. final room).
- **Run**: Host, F5 to load Spire_Slice. Play: LMB attacks with sword; killing the Spire Heart should trigger Victory and load Town with rewards. F12 = Evac with rewards. All players dead = Wipe with rewards.

---

## Flow summary

| Event            | Result                                              |
|------------------|-----------------------------------------------------|
| **F12 (Evac)**   | Rewards (gold + EXP by floor) → load Town.          |
| **Spire Heart killed** | Victory rewards (gold + EXP + bonus) → load Town. |
| **All players dead**   | Wipe rewards (reduced gold) → load Town.          |

Each client applies rewards to their own **MetaProgression** using the **class** they had for the run. Gold and class EXP persist across runs (PlayerPrefs).

---

## Weapon unlocks (gold)

Weapons can be unlocked with gold (e.g. in a town shop). Use **MetaProgression**:

- **UnlockWeapon(weaponId, costGold)** – Unlocks the weapon and deducts gold. Use the weapon’s **Weapon Id** (e.g. `"pistol"`) and **Unlock Cost Gold** from its config. Cost 0 = free.
- **SetEquippedWeaponId(weaponId)** – Sets which unlocked weapon to use for the next run. Must be already unlocked.
- **IsWeaponUnlocked(weaponId)** – For UI (e.g. show “Owned” or “Buy for 50 gold”).

**WeaponController** uses, in order: equipped unlocked weapon → class **Default Weapon** → **Config Fallback** on the component. Attack origin is auto-found from the player’s camera when it exists.
