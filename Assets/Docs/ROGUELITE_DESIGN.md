# Roguelite Design: Spire Crawl

## Vision
Co-op roguelite dungeon crawl ("friend slop"): players run the Spire together, gain EXP and gold across runs, level classes, unlock cosmetics, and have a clear win condition (beat the Spire / boss) for loot and progression.

---

## Core Loop

1. **Town (hub)** – Pick class, spend gold on cosmetics, start run.
2. **Run (Spire_Slice)** – Fight through procedural floors, clear rooms, survive rest-room hordes, reach top.
3. **End of run** – Victory (beat boss/heart), Evac (leave early), or Wipe (party dead). Rewards: gold + EXP.
4. **Meta progression** – Gold and EXP persist. EXP levels your **class**; gold buys **cosmetics** and other unlocks.

---

## Systems Overview

### 1. Meta Progression (persistent beyond the lobby)
- **Gold** – Earned from kills, chests, floors, victory bonus. Spent in town (cosmetics, upgrades, etc.).
- **EXP** – Earned per run; applied to the **class** you played. Level up that class to unlock passives/talents (later).
- **Cosmetics** – Unlocked with gold; stored in meta progression.
- **Persistence** – Saved **per player on their machine** (e.g. PlayerPrefs). Persists **across sessions and across lobbies**: same player keeps gold/EXP/cosmetics no matter who hosts or how many times they quit. Each client saves their own copy when they receive run rewards.

### Run state (wipes every run)
- **Spire run** (floors, segment) is **ephemeral**. On run end (evac, victory, or wipe), run state is **reset** (floor = 0). The next time players enter the Spire, it’s a **fresh run**. Only meta progression (gold, EXP, cosmetics) carries over.

### 2. Classes
- **ClassDefinition** (ScriptableObject) – id, displayName, description, base stats (maxHp, moveSpeed), preferred weapon type, icon.
- **Selection** – In town or at run start; stored for the run on the player (e.g. `PlayerClass` component with `NetworkVariable`).
- **Progression** – Each class has a level (from EXP). Future: talent trees, unlockable abilities.

### 3. Weapons
- **WeaponConfig** (ScriptableObject) – damage, range, cooldown, attack type (melee / ranged), hitbox size or projectile prefab.
- **WeaponController** (on player) – Server-authoritative attacks: melee (overlap/sphere) or ranged (raycast/projectile). Replaces or complements click-to-damage.
- **Examples** – Melee (sword/club), Ranged (pistol/crossbow). More weapons added as configs + prefabs.

### 4. Enemies & Bosses
- **More enemies** – Variants: ranged, tank, swarm, fast/slow. Same AI patterns (NavMesh, chase, attack) with different stats and abilities.
- **Boss** – One per run at top of Spire (or “Spire Heart”). Phases, arena, big health bar. Killing it = **Victory**.
- **Roadmap** – See `ENEMY_NEXT.md` and new `BOSSES_NEXT.md` for placement and spawn rules.

### 5. Run End & Rewards
- **Outcomes** – Victory (boss dead), Evac (F12 / leave early), Wipe (all players dead).
- **Reward formula** – Gold: base + per-floor + per-kill + victory bonus. EXP: base + per-floor + victory bonus, applied to the class you used.
- **Flow** – Server computes rewards, sends `RunResult` (gold, exp, classId, outcome) to all clients; clients add to `MetaProgression`, save, then server loads Town (or victory/defeat screen then Town).

### 6. Beat the Spire / Loot
- **Goal** – Reach top floor, defeat **Spire Heart** (or final boss). On kill: victory, big gold/EXP bonus, “loot” (could be a summary screen: gold, EXP, maybe one random unlock).
- **Loot** – MVP: gold + EXP. Later: random items, blueprints, cosmetic drops.

### 7. Cosmetics & Gold Sink
- **Cosmetics** – Skins for character/weapon, purchased with gold in town. Stored in meta progression; applied when spawning player.
- **Other gold sinks** – Potions, permanent upgrades, rerolls. Design as needed.

---

## Implementation Phases

| Phase | Focus | Deliverables |
|-------|--------|---------------|
| **1** | Foundation | MetaProgression (gold, EXP, save/load), RunResult, end-of-run flow (evac/victory grants rewards, load Town). |
| **2** | Classes | ClassDefinition SO, PlayerClass on player, class selection (Town or run start). EXP applied to selected class. |
| **3** | Weapons | WeaponConfig SO, WeaponController (melee + ranged), replace or supplement DebugDamageRaycaster. |
| **4** | Victory & boss | Spire Heart or boss at top floor; on death → victory, rewards, load Town. One boss stub + arena. |
| **5** | More enemies | 2–3 new enemy types (ranged, tank, swarm), reuse AI with different configs. |
| **6** | Polish | Run summary UI, class select UI, cosmetics list and gold spend in town. |

---

## File Layout (suggested)

```
Scripts/
  Meta/           MetaProgression.cs, RunResult.cs, MetaProgressionPersistence.cs
  Classes/        ClassDefinition.cs (SO), PlayerClass.cs
  Weapons/        WeaponConfig.cs (SO), WeaponController.cs, MeleeWeapon.cs, RangedWeapon.cs
  Run/            SpireRunState.cs (add outcome, EndRunWithRewards), RunRewardsCalculator.cs
  Spire/          SpireHeart.cs (or Boss), triggers victory on death
  Enemies/        (existing + new variants)
ScriptableObjects/
  Classes/        Warrior.asset, Mage.asset, ...
  Weapons/        Sword.asset, Pistol.asset, ...
```

---

## Notes
- **Friend slop** – Keep difficulty and systems readable; shared rewards and clear “we won / we left / we died” so everyone feels progress.
- **Server authority** – All combat, rewards, and run outcome decided on server; clients display and persist their own meta progression from RunResult.
- **Persistence summary** – **Meta** (gold, class EXP, cosmetics) = saved on the player’s machine, survives sessions and lobbies. **Run** (spire floors, current attempt) = wiped when the run ends so each new Spire entry is a fresh run.
