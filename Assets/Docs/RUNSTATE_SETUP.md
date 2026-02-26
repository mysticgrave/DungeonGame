# Run State Setup — Detailed

The **run state** is a single network object that tracks the current Spire run (floors, segments) and handles end-of-run (evac, victory, wipe) and rewards. It is **not** a child of the NetworkManager; it is **spawned by a bootstrap** in the Town scene when the host starts.

---

## 1. What the Run State is

- **SpireRunState** – Tracks floor count and segment; exposes `EndRunAndReturnToTown(outcome)` which pays rewards, wipes the run, and loads Town.
- **RunDebugHotkeys** – Host-only: F10/F11 add floors, F12 evac (triggers end-of-run and load Town).
- **RunDebugHUD** – Optional on-screen text for floor and hotkeys.
- **RunWipeDetector** – Server-only: when all players are dead, calls end-of-run as Wipe and load Town.

All of these can live on **one prefab** that gets spawned once when the server starts.

---

## 2. Create the RunState prefab

1. In the **Project** window, go to **Assets/Prefabs** (or your prefabs folder).
2. **Right-click → Create Empty**. Name it `RunState`.
3. **Double-click** the RunState prefab to open it in prefab mode (you should see only this object in the Hierarchy).

**Add components on the root (RunState) in this order:**

4. **Network Object**
   - **Add Component** → search `Network Object` → add **Network Object** (from Netcode for GameObjects).
   - Do **not** check “Player Prefab”.
   - Leave **Spawn With Observers** etc. as default.

5. **Spire Run State**
   - **Add Component** → search **Spire Run State** → add **Spire Run State** (`DungeonGame.Run.SpireRunState`).
   - Set **Floors Per Segment** (e.g. `5`).
   - Set **Town Scene Name** to exactly `Town` (same as your hub scene name).

6. **Run Debug Hotkeys**
   - **Add Component** → search **Run Debug Hotkeys** → add **Run Debug Hotkeys** (`DungeonGame.Run.RunDebugHotkeys`).
   - No fields to assign; it finds SpireRunState on the same object.

7. **Run Wipe Detector**
   - **Add Component** → search **Run Wipe Detector** → add **Run Wipe Detector** (`DungeonGame.Run.RunWipeDetector`).
   - Leave **Run State** empty; it will use the SpireRunState on the same object.

8. **Run Debug HUD** (optional)
   - **Add Component** → search **Run Debug HUD** → add **Run Debug HUD** (`DungeonGame.Run.RunDebugHUD`).

9. **Save the prefab** (Ctrl+S) and exit prefab mode.

---

## 3. Add RunState to the network prefab list

The RunState prefab must be in the **same list** the NetworkManager uses for spawnable prefabs, or the server cannot spawn it.

1. In the **Project** window, select **Assets/DefaultNetworkPrefabs** (the asset, not a scene).
2. In the **Inspector** you should see **Default Network Prefabs** with a **List**.
3. Click the **+** at the bottom of the List (or increase **Size** by 1).
4. In the new **Element** slot, drag the **RunState** prefab from the Project window.
5. **Save** (Ctrl+S) or let Unity auto-save.

If your project uses a different asset for the prefab list (e.g. another ScriptableObject referenced by the NetworkManager), add the RunState prefab to **that** list instead.

---

## 4. Bootstrap in the Town scene

The RunState is **not** under the NetworkManager. A separate script **spawns** it when the server starts.

1. Open the **Town** scene.
2. Find or create a GameObject that is **not** the NetworkManager (e.g. create empty **Bootstrap** or use an existing “Game” or “Managers” object).
3. Select that object.
4. **Add Component** → search **Run State Bootstrap** → add **Run State Bootstrap** (`DungeonGame.Run.RunStateBootstrap`).
5. In the inspector, set **Run State Prefab** to your **RunState** prefab (drag from Project).
6. **Save the scene.**

When the host starts (e.g. F1), the bootstrap will instantiate the RunState prefab, spawn its NetworkObject, and keep it across scene loads (DontDestroyOnLoad). Clients will see the same run state.

---

## 5. Quick check

- **Town** has a GameObject (e.g. Bootstrap) with **RunStateBootstrap** and **Run State Prefab** = RunState.
- **RunState prefab** has: NetworkObject, SpireRunState (Town Scene Name = "Town"), RunDebugHotkeys, RunWipeDetector, and optionally RunDebugHUD.
- **DefaultNetworkPrefabs** (or your prefab list) includes the **RunState** prefab.

When you run as host, you should see a log like: `[Run] Spawned SpireRunState singleton`. F10/F11/F12 should work in the Spire scene.
