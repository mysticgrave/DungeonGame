# DungeonGame — Design (Living Doc)

This project is about **controlled unpredictability**: even the developer shouldn’t fully know what to expect, but outcomes must stay readable and fair.

## High Concept
A persistent-character dungeon crawler where **Spire dungeons** erupt into the world and persist until cleared. Inside the spire: **always-on PvPvE** (players fight mobs, traps, and each other). Clearing a spire can award an **account-bound unique relic** that stays with the winner forever.

## Pillars
- **Tense / horror-leaning** atmosphere (sound + darkness + uncertainty).
- **Medium TTK** PvP (outplay windows; mistakes hurt but aren’t instant deletion).
- **Bosses are generated** from large attack pools, but each spire stores a fixed boss kit for its lifetime.
- **Persistent builds**: gear and equipment persist across death.
- **Meaningful loss**: death drops mats/consumables (lootable), and gear takes durability damage.
- **Economy + build mastery**: crafting/provisioning and item experimentation are core.

## MVP Rules (Draft)
### PvP scope
- Overworld/town: safe (PvP off) for MVP.
- Inside spires: PvP always on.

### Spire structure
- Overworld is shared.
- Spire interior is split into **layers/instances** for playability.
- Layer count may be randomized (within a controlled range).

### Death & durability
- Player keeps equipped gear + unique relics.
- Player drops consumables + crafting materials as a loot bag.
- Gear loses durability from use; extra durability loss on death.

### Carry limits
- Weight/encumbrance system prevents infinite supplies.

### Darkness & navigation
- Some floors are full darkness.
- Wall torches exist; may spawn lit/unlit per spire seed.
- Minimap exists with fog-of-war (reveals as explored).

### Campfires (player-built)
- Players can deploy campfires in spires.
- Campfires persist in-layer until they burn out.
- Anyone can use campfires (contested utility).
- Campfires enable water purification + cooking (and create visibility/noise risk).

## Open Questions (Later)
- Spire max lifetime/rotation rules if uncleared.
- Unique relic equip limits and/or recharge mechanics.
- Convergence floors cadence (layer merge points).
- Trade rules: what’s tradeable vs bind-on-pickup vs bind-on-equip.

## First Prototype Target
One playable loop:
1) Enter spire → fight to a camp node → deploy campfire
2) Purify water/cook fish under PvP pressure
3) Reach boss → learn its kit → die/return → try again
