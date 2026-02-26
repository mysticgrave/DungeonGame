# Why P_Sky and Global Fog Disappear

## What’s going on

**P_Sky** and **Global Fog** live in the **Town** scene under **Env Elements**. They disappear in two common cases:

### 1. You press Play with another scene open

When you press **Play**, Unity always loads the **first scene in Build Settings** (Town), not the scene you have open.

- If you have **Spire_Slice** (or any other scene) open and press Play, Town loads and your current scene is unloaded. So anything you only added in Spire_Slice (e.g. sky/fog) isn’t there.
- **Fix:** Ensure P_Sky and Global Fog are in **Town** (they already are). When you press Play, Town loads and they will show. If you also want them in the dungeon, use option 2 or 3 below.

### 2. You load the dungeon (Spire_Slice) after Play

When the host loads **Spire_Slice** (e.g. F5 or “Enter dungeon”), it uses **LoadSceneMode.Single**. That **unloads Town** and everything in it, including **Env Elements** (P_Sky, Global Fog).

- **Fix A – Persist in memory:** Add **PersistEnvironmentAcrossScenes** to the **Env Elements** GameObject (or a parent of P_Sky + Global Fog) in Town. On Awake it calls `DontDestroyOnLoad`, so that root and its children stay alive when you load Spire_Slice.
- **Fix B – Copy into dungeon:** Add P_Sky and Global Fog (or the same prefabs) to the **Spire_Slice** scene so they exist when the dungeon is loaded. No script needed; each scene has its own copy.

## Quick setup for “persist across scenes”

1. In **Town**, select **Env Elements** (or create an empty GameObject that parents P_Sky and Global Fog).
2. Add Component → **Persist Environment Across Scenes** (`DungeonGame.Core.PersistEnvironmentAcrossScenes`).
3. When you load Spire_Slice (or any other scene), that object and its children (P_Sky, Global Fog) will stay in the hierarchy and keep rendering.

If you prefer the sky and fog only in the dungeon, add them to **Spire_Slice** and don’t use the script.
