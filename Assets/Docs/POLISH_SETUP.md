# Polish UI Setup (Run Summary, Class Select, Weapon Shop)

Add these in the **Town** scene so players see run results, pick class, and buy/equip weapons.

---

## 1. Run Summary (after a run)

Shows a panel when you return to Town with the last run result (Victory / Evac / Wipe, gold, EXP, class level).

1. In Town, **Create Empty** → name it `RunSummaryUI`.
2. **Add Component** → **Run Summary UI** (`DungeonGame.UI.RunSummaryUI`).
3. Set **Town Scene Name** to `Town` (must match your scene name).
4. Optional: **Auto Hide After Seconds** (e.g. `8`) to hide the panel automatically; leave `0` to require clicking Continue.
5. Leave **Canvas / Panel / Title Text / Rewards Text / Continue Button** empty — the script will create a simple UI at runtime the first time it needs to show.

The panel appears when you have a last run result (after evac, victory, or wipe and load Town). Click **Continue** to dismiss.

---

## 2. Class Select

Lets the player choose which class to use for the next run. Selection is saved and applied when the player spawns (in Town or when entering the Spire).

1. In Town, **Create Empty** → name it `ClassSelectUI`.
2. **Add Component** → **Class Select UI** (`DungeonGame.UI.ClassSelectUI`).
3. Leave **Canvas** and **Button Container** empty — the script builds a small side panel with one button per class from **ClassRegistry**.

The selected class is stored in **MetaProgression** and sent to the server when your player spawns (via `PlayerClass.RequestSetClassFromSelectionServerRpc`).

---

## 3. Weapon Shop

Lists all weapons from **WeaponRegistry**. Unlocked weapons show an **Equip** button; locked ones show **Buy** (with gold cost) or **Free**.

1. In Town, **Create Empty** → name it `WeaponShopUI`.
2. **Add Component** → **Weapon Shop UI** (`DungeonGame.UI.WeaponShopUI`).
3. Leave **Canvas**, **Content Root**, **Gold Text** empty — the script builds a panel on the right with gold and weapon rows.

Ensure **WeaponRegistry** is in the scene and its **Weapons** list is filled (see ROGUELITE_SETUP Step 5b). Weapons use **Unlock Cost Gold** from their config; cost 0 = free to unlock.

---

## Quick check

- **Town** has: MetaProgression, ClassRegistry, WeaponRegistry, RunStateBootstrap, and the three UI GameObjects above.
- After a run (evac/victory/wipe), returning to Town shows the run summary once.
- Class select updates “selected class for next run”; that class is applied when you spawn.
- Weapon shop: Buy unlocks and deducts gold; Equip sets the weapon for the next run.
