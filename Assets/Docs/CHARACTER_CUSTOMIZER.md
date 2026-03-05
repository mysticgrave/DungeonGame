# Character Customizer — Manual Piece Selection

Use **CharacterCustomizer** instead of **CharacterRandomizer** when you want to select specific character pieces rather than randomize.

## Setup

1. **Remove** the `CharacterRandomizer` component from your character (or disable it).
2. **Add** the `Character Customizer` component (`DungeonGame.Character.CharacterCustomizer`).
3. Ensure the character has the standard Polygon Fantasy Hero hierarchy (e.g. `Male_Head_All_Elements`, `All_01_Hair`, etc.). The base modular character prefab from the asset pack has this structure.

## How It Works

- **Indices**: Each slot (head, torso, hair, etc.) uses a 0-based index. `0` = first option in that category.
- **Apply at runtime**: Selection is applied in `Start()`. In Play mode, use the **Apply Selection** button in the Inspector to refresh after changing values.
- **Gender**: Set **Gender** to Male or Female to switch body/head sets.

## Slots Overview

| Category   | Inspector field       | Notes                                             |
|-----------|------------------------|---------------------------------------------------|
| Head      | headIndex              | Requires `useHeadWithElements` true               |
| Eyebrows  | eyebrowIndex           | Only when head has elements                       |
| Facial hair| facialHairIndex        | Male only; 0 is usually clean-shaven               |
| Hair style / helmet | headCovering + hairIndex | Base Hair / No Facial Hair: hair. No Hair: helmet (0–13). |
| Body      | torsoIndex, hipsIndex  | Often start at 1 (0 may be naked base)            |
| Arms/hands| armUpperRightIndex, etc.| Left/right can differ                            |
| Legs      | legRightIndex, legLeftIndex | Left/right can differ                         |
| Attachments| chestAttachmentIndex, etc.| Use -1 for “none”                             |

## Tips

- **Finding indices**: Enter Play mode, tweak indices in the Inspector, and click **Apply Selection** to preview. Start with 0 and increase to cycle through options.
- **Attachments**: Use `-1` for any attachment slot to leave it empty.
- **Colors**: Enable **Apply Colors** and set **Primary**, **Secondary**, **Skin**, and **Hair** if you want to override the material. Leave **Material Override** null to use the character’s existing material.
- **Head with no elements**: Set `useHeadWithElements = false` to use `headNoElements` (simplified head, no separate eyebrow/facial-hair slots).

## Compatibility

Works with the same character structure as **CharacterRandomizer** (Polygon Fantasy Hero Characters). If your character uses a different pack or hierarchy, the script will log a warning and disable itself.
