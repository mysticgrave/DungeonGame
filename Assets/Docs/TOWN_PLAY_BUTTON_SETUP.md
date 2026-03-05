# Town Play Button & Loading Screen Setup

Add a **Play** or **Enter Dungeon** button in the Town scene so the host can start a dungeon run. Optionally show a loading screen while the dungeon loads.

---

## Overview

- **TownPlayController** – Call `EnterDungeon()` from a button. Host-only; loads the dungeon scene (e.g. `Spire_Slice`). Clients follow via Netcode.
- **LoadingScreenManager** – Shows a loading overlay when the host loads the dungeon. Uses Netcode scene events (`Load` → show, `LoadEventCompleted` → hide).

---

## Step 1: Add Loading Screen (optional but recommended)

1. In the **Town** scene, **Create Empty** → name it `LoadingScreenManager`.
2. **Add Component** → **Loading Screen Manager** (`DungeonGame.UI.LoadingScreenManager`).
3. Leave **Canvas**, **Panel**, **Status Text** empty — the script creates a simple full-screen overlay at runtime.
4. Optional: set **Loading Message** (e.g. `"Loading dungeon..."`).

The script uses `DontDestroyOnLoad`, so the loading screen survives when Town unloads during the transition.

---

## Step 2: Add Town Play Controller

1. In **Town**, create or select a GameObject for your UI (e.g. `TownUI` or `Canvas`).
2. **Add Component** → **Town Play Controller** (`DungeonGame.UI.TownPlayController`).
3. Set **Dungeon Scene Name** to `Spire_Slice` (or your first dungeon scene).
4. Optional: assign **Loading Screen** to the LoadingScreenManager from Step 1. If left empty, it uses `LoadingScreenManager.Instance` if present.

---

## Step 3: Add a Play Button

### Option A: Existing Canvas

1. Under your Town Canvas, **Right-click** → **UI** → **Button - TextMeshPro** (or **Button**).
2. Name it `PlayButton` or `EnterDungeonButton`.
3. Set the button’s **Text** child to "Enter Dungeon" or "Play".
4. Position it where you want (e.g. center of screen, or near other Town UI).

### Option B: No Canvas yet

1. **Right-click Hierarchy** → **UI** → **Canvas**.
2. **Right-click Canvas** → **UI** → **Button - TextMeshPro**.
3. Name it `PlayButton`. Edit the text to "Enter Dungeon".

---

## Step 4: Wire the Button

1. Select **PlayButton**.
2. In the **Button** component, find **On Click ()**.
3. Click **+** to add a listener.
4. **None (Object)** → drag the GameObject that has **TownPlayController** (e.g. TownUI or Canvas).
5. **No Function** → **DungeonGame.UI** → **TownPlayController** → **EnterDungeon ()**.

---

## Step 5: Build Settings

Ensure **Spire_Slice** (or your dungeon scene) is in **Build Settings**:

- **File** → **Build Settings**.
- Add **Spire_Slice** to **Scenes In Build** if it isn’t there.

---

## Flow

| Step | What happens |
|------|---------------|
| 1 | Host in Town clicks **Enter Dungeon** |
| 2 | TownPlayController.EnterDungeon() runs (host-only) |
| 3 | Loading screen shows (if LoadingScreenManager is present) |
| 4 | Server loads `Spire_Slice`; Netcode syncs clients |
| 5 | Clients receive `Load` event → loading screen shows |
| 6 | When everyone finishes loading, `LoadEventCompleted` → loading screen hides |
| 7 | All players are in the dungeon |

---

## Loading screen: Main Menu to Town (fade into Town)

A full-screen loading overlay now **replaces the connecting panel** for Host/Join. It blocks the view so players don't see the character spawn or camera change, then **fades out** to reveal Town.

### Setup (Main Menu)

If you want a loading screen **between when they host (Main Menu) and when they reach Town**:

1. Add **LoadingScreenManager** to the **Main Menu** scene (same setup as Step 1).
2. Leave **Canvas**, **Panel** empty (script creates overlay at runtime).
3. Optional: set **Fade Out Duration** (default 1.5s) for the fade into Town.
   - The loading screen will hide when the Town scene’s load completes.

Since the main menu uses `SceneManager.LoadScene` (not NetworkSceneManager) for Town, the Netcode `LoadEventCompleted` won’t fire for that transition. Options:

- **A)** Use `SceneManager.sceneLoaded` in LoadingScreenManager to hide when Town loads (add a flag: “we’re loading via SceneManager, not Netcode”).
- **B)** Keep the connecting panel from LobbyMenuController visible until Town loads; that already acts as a “loading” state.

For **host → Town**, the existing **Connecting Panel** in the main menu already covers “Creating lobby…” / “Connecting…”. For **host → dungeon**, the LoadingScreenManager covers the gap.

---

## Quick checklist

| Item | Action |
|------|--------|
| LoadingScreenManager | Create Empty in Main Menu (and optionally Town) → add component |
| TownPlayController | Add to UI root → set Dungeon Scene Name |
| Play button | UI → Button → wire On Click → TownPlayController.EnterDungeon |
| Build Settings | Spire_Slice (or your dungeon) in Scenes In Build |
