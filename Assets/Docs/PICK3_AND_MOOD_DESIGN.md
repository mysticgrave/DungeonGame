# Pick-3 & Dungeon Mood System Design

## Overview

Two systems to force chaos and make dungeons adapt to player behavior:

1. **Level-up Pick-1-of-3 (in-dungeon)** — Players earn run-only EXP from kills. On level-up, they choose 1 of 3 random modifier cards. Modifiers are per-player (damage dealt/taken, gold). Resets when run ends.

2. **Dungeon Mood** — During the run, the dungeon tracks player metrics (kills, health, deaths, pace) and shifts its "mood." The mood influences enemy density, respawn speed, and ambient intensity.

3. **Pre-run Pick-3 (optional)** — Host can optionally pick 3 run-wide modifiers before entering (TownPlayController.pick3Controller). Level-up cards are the primary system.

---

## Pick-3 System

### Flow

1. Host clicks "Enter Dungeon" (or "Play") in Town
2. Pick-3 panel appears with 6–9 random modifiers
3. Host selects exactly 3
4. Selections stored in `SpireRunState`
5. Dungeon loads; systems read modifiers

### Modifier Effects (Examples)

| Modifier | Effect |
|----------|--------|
| Horde | +50% max alive per spawn point |
| Glass Cannon | +25% damage dealt, +25% damage taken |
| Greed | +30% gold, +20% enemy count |
| Rush | Respawn 50% faster |
| Curse | Random debuff per floor |
| Chaos | 2x mood swing speed |

### Data Flow

- `DungeonCardConfig` — ScriptableObject: id, displayName, description, effect multipliers
- `SpireRunState.ModifierIdsNet` — FixedString64Bytes storing "id1|id2|id3" (synced to clients)
- `Pick3Controller` — UI; host picks, calls `SpireRunState.SetRunModifiers(ids)`, then `LoadDungeonScene()`
- `TownPlayController` — If `pick3Controller` assigned and host, shows Pick-3 before loading dungeon

---

## Dungeon Mood System

### Mood States

| Mood | Trigger | Effect |
|------|---------|--------|
| Calm | High health, few kills, slow pace | -20% spawn rate, slower respawn |
| Tense | Mid health, moderate activity | Baseline |
| Chaotic | Low health OR high kill rate | +30% spawn rate, faster respawn |
| Desperate | Multiple deaths, very low health | +50% spawn, ambush bias |

### Metrics Tracked (Server)

- **Health %** (lowest among players)
- **Kills this run** (total; from `NetworkHealth.OnAnyDiedStatic`)
- **Deaths this run** (from `PlayerHealth.OnAnyPlayerDied`)
- **Run time** (for kills-per-minute)

### Implementation

- `DungeonMoodTracker` — Server component in dungeon scene; subscribes to death events, recomputes mood every `updateInterval`; `moodSwingMultiplier` from Pick-3 makes chaos trigger easier
- `DungeonMood` — Static: `Current`, `SpawnRateMultiplier`, `RespawnSpeedMultiplier`; `Reset()` on run end
- `EnemySpawner` — `GetEffectiveMaxAlive()`, `GetEffectiveRespawnDelay()` using `SpireRunState` modifiers + `DungeonMood`

---

## Integration Points

1. **SpireRunState** — Holds `ActiveModifiers` (NetworkList)
2. **TownPlayController** — Opens Pick-3 panel first; on confirm → store modifiers → load scene
3. **EnemySpawner** — `GetEffectiveMaxAlive(point)`, `GetEffectiveRespawnDelay(point)` using modifiers + mood
4. **SpireLayoutGenerator** — (Optional) Mood could bias room types (more combat vs rest)
