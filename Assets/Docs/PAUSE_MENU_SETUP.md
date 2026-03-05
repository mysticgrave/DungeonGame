# Pause Menu Setup

A local pause menu that freezes the player's camera and movement (multiplayer-safe) and unlocks the cursor so they can click Resume, Settings, or Quit.

---

## Overview

- **Escape** toggles the pause menu.
- When paused: camera and movement freeze, cursor unlocks for mouse input.
- **Resume** — closes menu, locks cursor, resumes gameplay.
- **Back to Town** — (dungeon only, host only) loads Town via network; clients follow. Shows loading screen, then fades into Town.
- **Settings** — placeholder; wire to your settings panel.
- **Quit to Main Menu** — disconnects (if networked) and loads Main Menu. Shows loading screen, then fades in.

---

## Setup

### Option A: Runtime-created UI

1. In **Town** and **Spire_Slice** scenes, **Create Empty** → name it `PauseMenuController`.
2. **Add Component** → **Pause Menu Controller** (`DungeonGame.UI.PauseMenuController`).
3. Leave **Panel**, **Resume Button**, etc. empty — the script creates a simple panel with buttons at runtime.
4. Set **Main Menu Scene Name** to `MainMenu` if different.
5. Optionally edit **Allowed Scenes** (default: Town, Spire_Slice).

### Option B: Custom UI

1. Create a **Canvas** with a **Panel** (semi-transparent overlay).
2. Add **Resume**, **Settings**, **Quit** buttons as children.
3. Add **Pause Menu Controller** to the Canvas or Panel.
4. Assign **Panel** and the three **Button** references in the Inspector.

---

## Wire the buttons

If you create custom UI, wire the Button **On Click** events:

| Button  | Method                     |
|---------|----------------------------|
| Resume         | PauseMenuController.Resume |
| Back to Town   | PauseMenuController.BackToTown (host only, in dungeon) |
| Settings       | (your settings panel logic) |
| Quit to Main Menu | PauseMenuController.QuitToMainMenu |

---

## Scenes

Add **PauseMenuController** to both **Town** and **Spire_Slice** so the pause menu is available in the hub and during runs. The script only allows pause in scenes listed in **Allowed Scenes** (or any scene except Main Menu if the list is empty).

---

## Quick checklist

| Item                  | Action                                                |
|-----------------------|-------------------------------------------------------|
| PauseMenuController   | Create Empty in Town + Spire_Slice → add component    |
| Panel/Buttons         | Leave empty for auto-created UI, or assign custom      |
| Main Menu Scene Name  | Set to `MainMenu` (or your main menu scene name)     |
| Build Settings        | MainMenu, Town, Spire_Slice in Scenes In Build        |
