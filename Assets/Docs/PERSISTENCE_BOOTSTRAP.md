# Persistence Bootstrap

Player data (gold, EXP, unlocks) is now initialized **before the first scene loads**, so it's ready when they reach the main menu.

---

## What changed

1. **PersistenceBootstrap** — Runs at game launch (BeforeSceneLoad). Creates MetaProgression so save/load is ready before Host or Join.
2. **SteamLobbyManager** — When a client joins a Steam lobby, waits 3 frames before calling StartClient. This gives Steam time to propagate lobby state and can fix "player not completely joining" issues.

---

## No setup required

PersistenceBootstrap runs automatically. MetaProgression is created at launch and persists across scenes. Town still has MetaProgression in its scene; the first one created (by the bootstrap) wins; Town's instance destroys itself if one already exists.

---

## Flow

1. Game launches → PersistenceBootstrap runs → MetaProgression created → Load() runs (reads gold, EXP, etc. from PlayerPrefs).
2. Main Menu loads → Steam initializes (SteamManager on NetworkManager).
3. User clicks Host or Join → data is already loaded.
4. Client joins Steam lobby → 3-frame delay → StartClient → improves join reliability.
