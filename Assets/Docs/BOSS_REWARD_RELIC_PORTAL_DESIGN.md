# Boss Reward: Relics + Escape/Continue Portals

Design for post-boss flow: 3 relics (dungeon modifiers) and two portal options (Escape vs Continue).

---

## Flow Overview

```
Boss defeated
    → Spawn: 3 relic pedestals + Escape portal + Continue portal
    → Player picks ONE relic (walk up + F, or UI click)
    → Player chooses (F-interact):
        - Escape Portal: End run, return to Town, keep rewards
        - Continue Portal: Add relic to run, load next dungeon segment
```

---

## Option A: Relics as DungeonCardConfig (Reuse)

Your existing `DungeonCardConfig` already defines modifiers (DamageDealt, Gold, MaxEnemies, etc.). **Relics can be the same configs** — just a different pool and moment of choice.

**Setup:**
- Create a **Relic Pool** (ScriptableObject or list on BossRewardManager): 10–20 `DungeonCardConfig` assets for boss rewards.
- Pick 3 at random (like level-up); store chosen ID in `SpireRunState.AddLevelUpModifier(id)`.
- `DungeonMoodTracker`, spawn logic, etc. already read `GetActiveCards()` — no changes needed.

**Pros:** Zero new data types, modifiers apply automatically.  
**Cons:** Relics might want different flavor (e.g. unique boss-only effects). You can add new `CardEffectType` values as needed.

---

## Option B: Dedicated RelicConfig (More Customization)

If relics need distinct behavior (e.g. "next boss has 2 phases", "unlock secret room"), create:

```
RelicConfig (ScriptableObject)
├── id, displayName, description, icon
├── effects[] (can mirror CardEffect or be custom)
└── OnApply(SpireRunState) — optional one-time logic
```

Then either:
- Map `RelicConfig` → `DungeonCardConfig` internally when adding to run, or
- Extend `SpireRunState` to track relic IDs separately and apply relic-specific logic.

---

## Implementation Components

### 1. BossDeathTrigger

When the boss dies (e.g. `NetworkHealth.OnDied` on boss), trigger:

```csharp
// Pseudocode
void OnBossDied()
{
    SpawnRelicPedestals(3);   // Or show UI
    SpawnEscapePortal();
    SpawnContinuePortal();
    SetBossRewardPhase(true); // Disable combat, etc.
}
```

### 2. Relic Pedestals (IInteractable)

- Each pedestal has a `DungeonCardConfig` (or `RelicConfig`).
- `InteractPrompt`: "Take [Relic Name]"
- `CanInteract`: Only if no relic chosen yet, and this pedestal hasn't been taken.
- `Interact`: Call `SpireRunState.AddLevelUpModifier(relicId)`, mark chosen, maybe hide other pedestals or disable them.

### 3. Escape Portal (IInteractable)

- `InteractPrompt`: "Escape to Town"
- `CanInteract`: Always (or only after relic chosen, depending on design).
- `Interact`: `SpireRunState.EndRunAndReturnToTown(RunOutcome.Victory)` (or `RunOutcome.Evac` if you want to distinguish "fled" vs "finished").

### 4. Continue Portal (IInteractable)

- `InteractPrompt`: "Continue to Next Dungeon"
- `CanInteract`: Only if player has chosen a relic.
- `Interact`: `SpireRunState.AddFloorRpc(floorsPerSegment)` (or load next segment scene), then load next dungeon.

---

## Portal Placement Options

| Layout | Description |
|--------|--------------|
| **Side by side** | Escape left, Continue right. Clear choice. |
| **Escape only after relic** | Both portals appear only after picking a relic. |
| **Escape anytime** | Escape always available (leave without relic); Continue requires relic. |
| **Single portal, two modes** | One portal object; interact opens a small UI: "Escape" / "Continue". |

---

## RunOutcome

Your `RunOutcome` likely has `Evac`, `Wipe`, etc. Consider adding:

- `Victory` — Boss defeated, escaped with rewards.
- `Continue` — Not needed if Continue just advances floors; Victory/Evac cover "run ended in town".

---

## Scene / Spawn Flow

1. **Boss arena** — Has spawn points for pedestals and portals (empty until boss dies).
2. **On boss death** — Server spawns (or enables) NetworkObjects for pedestals and portals.
3. **Relic prefab** — `NetworkObject` + `IInteractable` + `RelicPedestal` (holds config reference).
4. **Portal prefab** — `NetworkObject` + `IInteractable` + `EscapePortal` or `ContinuePortal` (references SpireRunState).

---

## Quick Start Checklist

- [ ] Create 5–10 relic cards (reuse or extend DungeonCardConfig).
- [ ] Add `BossRewardManager` or hook into boss `NetworkHealth.OnDied`.
- [ ] Implement `RelicPedestal : IInteractable` (or `RelicInteractable`).
- [ ] Implement `EscapePortal : IInteractable` and `ContinuePortal : IInteractable`.
- [ ] Add spawn points in boss arena; spawn prefabs on boss death.
- [ ] (Optional) Add boss reward UI if you prefer click-to-pick over walk-up interact.
