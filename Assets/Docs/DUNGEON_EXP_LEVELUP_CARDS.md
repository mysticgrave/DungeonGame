# Dungeon Run EXP & Level-up Cards

## Overview

Players share a **single EXP bar** for the whole team. Any kill from any player adds to this pool. EXP resets when they return to Town. On level-up, **all players** see 3 random cards; **first to pick** chooses for the whole team. Level-up modifiers apply run-wide (damage, gold, spawn, etc. for everyone).

## Flow

1. Enter dungeon → shared run EXP = 0, level = 1
2. Any player kills enemy → server adds EXP to shared pool
3. Shared EXP reaches threshold → level up → server sends 3 cards to **all** players
4. First player to pick 1 → server adds to run-wide modifiers, closes pick panel for everyone
5. Run ends → run EXP resets, level-up modifiers cleared

## Components

| Component | Purpose |
|-----------|---------|
| `DungeonRunExp` | Server: tracks shared EXP, awards on kill, triggers level-up card pick. Must be on a NetworkObject in the dungeon scene. |
| `DungeonExpBarUI` | Shows shared EXP bar (slider/fill). Subscribes to `DungeonRunExp.OnExpChanged`. |
| `LevelUpCardPickerUI` | Shows 3 cards on level-up to all players. Subscribes to `OnLevelUpRequestCards` and `OnCardPicked`. First pick applies for the team. |
| `SpireRunState` | Holds run-wide modifiers (pre-run + level-up). `AddLevelUpModifier(modId)`. `GetDamageDealtMultiplier` etc. use `GetActiveModifiers()`. |
| `WeaponController` | Applies damage-dealt multiplier when dealing damage to enemies. |
| `PlayerHealth` | Applies damage-taken multiplier when taking damage. |

## Setup

1. **DungeonRunExp** – Add to dungeon scene (e.g. on a NetworkObject game manager):
   - Assign `cardPool` (DungeonCardConfig[] — same or subset of SpireRunState.cardPool)
   - Tune `expPerKill`, `expPerLevelBase`, `expPerLevelIncrement`

2. **DungeonExpBarUI** – Add to dungeon HUD:
   - Assign Slider (or Image with Fill), optional label Text, optional root GameObject

3. **LevelUpCardPickerUI** – Add to dungeon HUD:
   - Assign panel, cardContainer, cardPrefab (Button + Text for name/description)
   - Card prefab: Button + child Text for displayName, optionally another Text for description

4. **Dungeon Card assets** – Create via Assets > Create > DungeonGame > Dungeon Card. Each card needs effects (buffs + debuffs), rarity, and passes `IsValid()`. See DUNGEON_CARD_DESIGN.md. Assign to SpireRunState.cardPool and DungeonRunExp.cardPool.

## Kill attribution

`NetworkHealth.TakeDamage(amount, dealerClientId)` stores the last attacker. On death, `OnAnyDiedStatic` fires with the killer's clientId. `WeaponController` passes `OwnerClientId` when dealing damage. Enemies that damage via non-Player sources (e.g. traps) use `TakeDamage(amount)` with no dealer → killer = 0 → no EXP awarded.
