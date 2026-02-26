# Bosses & Spire Victory

## Goal
One or more bosses act as run end goals. Beating the Spire = defeat the final boss (Spire Heart) → Victory → gold/EXP → Town.

## Implemented
- **SpireHeart** – Place in the final room. Has `NetworkHealth`; when it dies, server calls `SpireRunState.EndRunAndReturnToTown(RunOutcome.Victory)` so everyone gets victory rewards and returns to Town.

## Setup
1. Create a prefab (e.g. `SpireHeart.prefab`) with:
   - `NetworkObject`
   - `NetworkHealth` (e.g. maxHp = 50)
   - `SpireHeart` (assign `runState` or leave null to auto-find)
   - Collider (for weapons) + simple visual
2. Place the prefab in your top-floor / final room (manually or via generator “end room”).
3. When the heart is killed, the run ends in Victory.

## Next: Real Bosses
- **Phases** – Boss changes behavior at HP thresholds (e.g. 66%, 33%).
- **Arena** – Dedicated boss room prefab with doors that close, no adds.
- **Spawn from generator** – Mark one room as “Boss” or “Spire Heart”; generator places boss prefab there.
- **Multiple bosses** – Different final bosses per segment or at random; same victory flow.

## Enemy Variety (separate from bosses)
- See `ENEMY_NEXT.md`. Add ranged, tank, swarm variants; reuse NavMesh AI with different stats and one special ability each.
