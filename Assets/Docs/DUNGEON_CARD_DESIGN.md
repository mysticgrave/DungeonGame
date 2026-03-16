# Dungeon Card Design

## Principles

1. **Every card has upside and downside** — At least one buff (multiplier > 1) and one debuff (multiplier < 1). No pure buffs.
2. **Better upside = worse downside** — Stronger benefits should be paired with stronger penalties. Design guideline; enforced via `IsValid()` (requires both).
3. **Rarity reflects power** — Rarer cards have stronger effects. Rarity also affects **pick weight**: Common (4x), Uncommon (2x), Rare (1x), Legendary (0.5x) — rarer cards appear less often in level-up offers.
4. **Cards are unique** — Use the effect list to mix and match. Override `customStatLines` for narrative flavor.

## Effect Types

| Type | Meaning | Example |
|------|---------|---------|
| DamageDealt | Player damage output | 1.25 = +25% damage |
| DamageTaken | Incoming damage | 0.8 = -20% damage taken |
| Gold | Gold drops | 1.5 = +50% gold |
| MaxEnemies | Spawn density | 1.5 = +50% enemies per point |
| RespawnSpeed | Respawn rate (<1 = faster) | 0.5 = 2x faster respawns |
| MoodSwing | Dungeon mood sensitivity | 2 = mood swings 2x faster |

## Card Config Structure

```
DungeonCardConfig
├── id, displayName, description
├── rarity (Common / Uncommon / Rare / Legendary)
├── icon, customStatLines (optional)
└── effects[] — list of { type, multiplier, customLabel? }
```

Each effect: `multiplier > 1` = buff, `multiplier < 1` = debuff. Multiple effects of the same type multiply together.

## Validation

- `IsValid()` returns true only if the card has at least one buff and one debuff.
- Invalid cards are excluded from the level-up pool.
- Editor `OnValidate` warns when a card lacks the required trade-off.

## Example Cards

**Glass Cannon** (Uncommon)
- Damage Dealt: 1.4 (buff)
- Damage Taken: 1.3 (debuff)
- Description: "Hit harder, get hit harder."

**Horde** (Common)
- Max Enemies: 1.5 (buff)
- Respawn Speed: 1.2 (debuff — slower respawns)
- Description: "More enemies, but they take longer to return."

**Greed** (Rare)
- Gold: 1.6 (buff)
- Damage Taken: 1.2 (debuff)
- Description: "Fortune favors the bold — and the wounded."

## Rarity Pick Weights

When drawing 3 cards for a level-up, each card's chance is proportional to its weight:
- Common: 4
- Uncommon: 2
- Rare: 1
- Legendary: 0.5

So a pool of 2 Common + 1 Rare gives: Common 4/(4+4+1) ≈ 44% each, Rare ≈ 11%.
