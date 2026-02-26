# Main Menu: 3D Carved Wall + Dragon Vignette

## Goal

- **Left ~1/3**: Buttons **carved into a wall** (3D). On hover they **glow like magic**.
- **Right ~2/3**: **3D vignette** — dragon sleeping on gold in a dungeon room.

Everything is one **3D scene** viewed by a single camera (no 2D overlay for the vignette).

---

## 1. Scene layout

- **One scene** (e.g. `MainMenu`) with one **Camera**.
- **Left third**: A **wall** (plane or custom mesh) with **recessed “carved” areas** where each button lives. The wall can be dungeon stone (brick, rock material).
- **Right two-thirds**: **Dungeon room** set: floor, walls, dim lighting, **gold pile** (props or mesh), **dragon** (model) in a sleeping pose.
- **Camera**: Positioned so the **left third** of the view is the wall and the **right two-thirds** are the dragon room. No need for the camera to move unless you want a subtle idle animation.

Think of it as one room: the player is standing in front of a stone wall (with carved buttons) and to their right is the dragon hoard.

---

## 2. Carved buttons (left wall)

### Option A: 3D meshes (most “carved” look)

- Model a **wall** with **inset panels** (recessed quads or simple boxes) where each button sits.
- Each **button** = a **quad or panel** mesh inside the recess, with a **material** that can **glow** (emissive).
- Use **colliders** (BoxCollider) on each button and **raycasting** from the camera (mouse) to detect hover/click.
- **Hover** → increase **emissive** color/intensity on that button’s material (or swap to a “glow” material) for the magic effect.
- **Click** → run your button action (load scene, open settings, etc.).

### Option B: World-space UI on a wall

- A **Canvas** in **World Space** (not Screen Space) parented to the wall.
- **RectTransform** sized/positioned so the canvas sits in the **left third** of the view, flat on or slightly in front of the wall.
- Buttons = **UI Button** (Image + Text) with a **material** that has **emissive** (e.g. Unlit with emission, or URP/HDRP material with Emission).
- Use **EventSystem** + **Physics Raycaster** (if you use 3D colliders on the buttons) or **Graphic Raycaster** (if the canvas is in front of the camera). For “carved into wall” you’ll often use **Option A** or a **hybrid**: 3D collider for hit testing, UI only for labels.

### Recommended: Option A + labels

- **3D** recessed panels with **emissive materials** for the magic glow.
- Optional: **World-space TextMeshPro** (or small UI labels) in front of each panel for “Play”, “Settings”, “Quit”.
- Script: **CarvedButtonGlow** (or similar) on each button: **OnMouseEnter** / **OnMouseExit** (or raycast from camera) to drive emissive intensity.

---

## 3. Magic glow on hover

- **Material**: Use a material with **Emission** (URP: **Emission** checkbox + color; Built-in: **Emission** in Standard).
- **Default**: Emission color at (0,0,0) or very dim.
- **Hover**: Lerp or set emission to a **magic color** (e.g. cyan, blue, or gold) and **increase intensity** (e.g. 0 → 1 or 2).
- **Implementation**: In a script on each button, on hover (raycast hit or OnMouseEnter):
  - Set `material.SetColor("_EmissionColor", color * intensity)` and optionally `material.EnableKeyword("_EMISSION")`.
  - On hover exit, set emission back to dim/zero.
- Optional: **Point light** or **spot** per button that turns on when hovered for extra glow.

---

## 4. Dragon + gold + dungeon (right 2/3)

- **Environment**: Dungeon room (walls, floor, maybe pillars). Dark, atmospheric lighting.
- **Gold**: Pile of coins/treasure (asset or simple proxy meshes) in the center-right.
- **Dragon**: Place a **dragon model** in a **sleeping** pose on or beside the gold. If your model doesn’t have a sleep animation, use a **static pose** or a very slow idle (e.g. breathing).
- **Lighting**: Dim ambient; one or two **point/spot lights** (e.g. from torches or a subtle glow from the gold) so the dragon and gold read clearly. Avoid washing out the left wall.

---

## 5. Implementation checklist

| Step | Task |
|------|------|
| 1 | Create **MainMenu** scene; add **Camera** and position so left 1/3 = wall, right 2/3 = room. |
| 2 | **Wall**: Plane or custom mesh; material = dungeon stone. Add **recessed panels** (model or quads) for each button. |
| 3 | **Button meshes**: One quad/mesh per button inside the recesses; assign **emissive material** (emission off or dim by default). |
| 4 | **Colliders**: Add **BoxCollider** (or MeshCollider) to each button; ensure **layer** is raycastable. |
| 5 | **Hover/click**: **Camera** raycast (from mouse) each frame; on hit with a button, set that button’s emission to “glow”; on click, invoke action. Or use **OnMouseEnter** / **OnMouseExit** / **OnMouseDown** on a small script on each button. |
| 6 | **Dragon + room**: Place dungeon room, gold pile, dragon; light the scene. |
| 7 | **Button actions**: On click, load scene (e.g. Town), open settings, or quit — same as a normal menu, but triggered from 3D hit. |

---

## 6. CarvedButtonGlow script

Use **CarvedButtonGlow** (`Assets/Scripts/UI/CarvedButtonGlow.cs`) on each 3D button:

- Add a **Collider** (BoxCollider) to the button mesh and **CarvedButtonGlow** to the same GameObject (or assign **Target Renderer** to a child with the mesh).
- Set **Glow Color** and **Glow Intensity** for the magic look; the script lerps emission on hover.
- In **On Click**, add listeners (e.g. **SceneManager.LoadScene** for "Town", or a small method that calls **Application.Quit()**).

The script uses **OnMouseEnter** / **OnMouseExit** / **OnMouseDown**, so the camera’s default raycast will hit the collider. Make sure the button’s **Layer** is included in the camera’s culling and that no UI is blocking (the script skips click if the pointer is over UI).

---

## 7. Summary

- **One 3D scene**: wall (left) + dragon room (right).
- **Carved buttons** = 3D panels with **emissive materials**; **hover** = raise emission for magic glow; **click** = raycast + invoke menu action.
- **Right side** = pure 3D set dressing (dragon on gold, dungeon); no 2D UI needed for the vignette.

If you tell me whether you prefer **Option A (full 3D buttons)** or **Option B (world-space UI)** and your render pipeline (Built-in / URP / HDRP), I can add a **CarvedButtonGlow** (or **CarvedButton**) script next and where to put it in the scene.
