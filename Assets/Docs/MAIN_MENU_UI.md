# Main Menu: Normal UI Buttons (with optional glow)

Use **standard UI buttons** on the left side over your 3D scene (dragon + dungeon). The 3D view stays as the background; buttons are a **Screen Space** canvas overlay.

---

## What you need

1. **Canvas** (Screen Space Overlay or Screen Space – Camera)
2. **EventSystem** (Unity usually adds this when you add a Canvas)
3. **Buttons** on the left side (Play, Settings, Quit, etc.)
4. **Optional**: A bit of **glow** on hover (color tint, outline, or glow image)

---

## Step 1: Add a Canvas

1. In your **MainMenu** scene, **Right-click in Hierarchy** → **UI** → **Canvas**.
2. Unity will create **Canvas** + **EventSystem** + **Canvas Scaler** + **Graphic Raycaster**.
3. Select the **Canvas**:
   - **Render Mode**: **Screen Space - Overlay** (draws on top of the 3D scene) or **Screen Space - Camera** if you want the UI to be in 3D space (assign your main camera).
   - **Canvas Scaler**: **Scale With Screen Size**; **Reference Resolution** e.g. 1920×1080; **Match** 0.5 (or 0 for width, 1 for height); **Reference Pixels Per Unit** 100.

---

## Step 2: Put buttons on the left

1. **Right-click the Canvas** → **UI** → **Panel** (optional; gives you a dark semi-transparent strip on the left if you want).
2. **Right-click Canvas** (or the Panel) → **UI** → **Button - TextMeshPro** (or **Button** for legacy Text).
3. Name it e.g. **PlayButton**. Duplicate for **SettingsButton**, **QuitButton**, etc.

**Position the first button (e.g. Play):**

- Select **PlayButton**.
- In **RectTransform**:
  - **Anchor**: Left side (e.g. **Left stretch** — click the anchor square, hold Alt, pick the left-middle preset).
  - **Left**: 80 (padding from left edge).
  - **Right**: 400 (or leave “Width” and set **Width** to 320).
  - **Top / Bottom**: set so height is ~60; **Pos Y** so it’s where you want (e.g. 0 for first button).
  - Or use **Anchor Presets**: left-middle, then set **Pos X** 200, **Width** 280, **Height** 50.

Stack the rest:

- **SettingsButton**: same width, **Pos Y** about -70 (below Play).
- **QuitButton**: **Pos Y** about -140 (below Settings).

(Adjust numbers to match your layout.)

---

## Step 3: Style the buttons

- **Button** component: set **Source Image** to a sprite (e.g. rounded rectangle from Unity’s UI default, or your own).
- **Color**: e.g. dark gray/brown so they look like they belong in a dungeon; **Highlighted** / **Pressed** in the Button’s **Color Tint** for hover/click.
- **Text (or TextMeshPro)** child: set label (“Play”, “Settings”, “Quit”); font size and color to taste.

---

## Step 4: Add a subtle glow on hover

Pick one (or combine):

### A) Built-in Button Color Tint (easiest)

- Select the **Button**.
- In **Button** component → **Transition** = **Color Tint**.
- Set **Highlighted Color** to a slightly brighter or more saturated color (e.g. light cyan or gold).
- Set **Normal Color** to your default.
- On hover, the button will tint — looks like a soft “glow” without code.

### B) Outline-only glow (background + text)

To glow **only the outlines** (no fill):

1. Add **Main Menu Button Outline Glow** (`DungeonGame.UI.MainMenuButtonOutlineGlow`) to the **Button** (same GameObject as the Button component).
2. Set **Glow Color** (e.g. cyan or gold) and **Outline Distance** (e.g. 2, 2) in the Inspector.
3. The script finds the button’s **Image** and any **Text** (or other Graphic) children, adds Unity’s **Outline** component if missing, and **enables** the outline with your glow color on hover; on exit it **disables** the outline. So you get an outline glow on both the button shape and the label, with no fill change.

Use this **instead of** MainMenuButtonGlow if you want outline-only.

### C) Outline for a glow-like edge (manual)

- Select the **Button** (or the **Image** inside it).
- **Add Component** → **Outline** (UI).
- Set **Effect Color** to your glow color (e.g. cyan/gold); **Effect Distance** (1, 1) or (2, 2).
- Optional: **disable** Outline by default and **enable** it only on hover (see C).

### C) Outline for a glow-like edge (manual)

- Add a script that on **PointerEnter** slightly increases the button Image’s **color** (e.g. multiply by 1.2) or enables an extra **Image** (a glow sprite behind the button).
- Example: **MainMenuButtonGlow.cs** — on `IPointerEnterHandler` / `IPointerExitHandler` set `Image.color` or set active a child “Glow” image.

If you want, we can add a **MainMenuButtonGlow.cs** that:
- On hover: sets the button’s Image color to a brighter “glow” color (or enables a child glow Image).
- On exit: restores normal color (or disables glow).

---

## Step 5: Hook up button actions

Unity’s Button **On Click** list only lets you call **methods on a component**. You can’t drag “SceneManager” or “Application” into it. Use the **MainMenuController** script instead.

### 5a. Add the MainMenuController

1. In the **MainMenu** scene, select your **Canvas** (or create an empty GameObject and name it **MenuController**).
2. **Add Component** → search **Main Menu Controller** → add **Main Menu Controller** (`DungeonGame.UI.MainMenuController`).
3. In the Inspector, set **First Scene Name** to your first scene (e.g. **Town**). That’s the scene that loads when you click Play.

### 5b. Wire the Play button

1. Select **PlayButton** in the Hierarchy.
2. In the Inspector, find the **Button** component and the **On Click ()** section at the bottom.
3. Click the **+** button under **On Click ()** to add a new listener.
4. In the new row:
   - **None (Object)** → drag the **Canvas** (or the GameObject that has **MainMenuController**) from the Hierarchy into this slot.
   - **No Function** dropdown → click it → choose **DungeonGame.UI** → **MainMenuController** → **LoadFirstScene ()**.
5. Leave the second parameter empty. When you click Play, it will load the scene you set in **First Scene Name**.

### 5c. Wire the Quit button

1. Select **QuitButton** in the Hierarchy.
2. In the **Button** component, **On Click ()** → click **+**.
3. Drag the **same** GameObject that has **MainMenuController** (Canvas or MenuController) into **None (Object)**.
4. **No Function** → **DungeonGame.UI** → **MainMenuController** → **Quit ()**.
5. In the Editor, **Quit** will stop Play mode. In a **build**, it will close the game.

### 5d. Optional: load a different scene from another button

If you have a “Settings” or “Credits” button that loads a different scene:

1. Add a listener on that button’s **On Click ()**.
2. Drag the **MainMenuController** GameObject into the object field.
3. Choose **MainMenuController** → **LoadScene (string)**.
4. A **string** box appears: type the exact scene name (e.g. **Settings**, **Credits**). That scene must be in **Build Settings** (File → Build Settings → Add Open Scenes).

### Build Settings

Your scenes must be in **Build Settings** or LoadScene won’t find them:

- **File** → **Build Settings**.
- Drag your **MainMenu**, **Town**, and any other scenes from the Project window into **Scenes In Build**, or click **Add Open Scenes**.
- **MainMenu** should be index **0** if you want it to be the first scene that runs.

---

## Quick checklist

| Item | What to do |
|------|------------|
| Canvas | UI → Canvas; Screen Space Overlay (or Camera); Scaler 1920×1080 |
| Buttons | UI → Button - TextMeshPro; anchor left; stack vertically |
| Position | Left third of screen (e.g. Left 80, Width 280, stack with Pos Y) |
| Glow | Color Tint (Highlighted) and/or Outline and/or small hover script |
| Play | On Click → LoadScene "Town" (or your scene name) |
| Quit | On Click → Application.Quit |

---

## Optional: MainMenuButtonGlow script

If you want a script that only does “hover = glow” (brighten or show glow image), we can add **MainMenuButtonGlow.cs** and you attach it to each button. Say if you want that and I’ll add it.
