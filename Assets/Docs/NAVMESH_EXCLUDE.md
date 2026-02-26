# Excluding Objects From NavMesh Baking

To stop certain objects (decorations, props, ceilings, etc.) from being used when the NavMesh is baked:

## Option A: Use the "IgnoreNavMesh" tag (easiest)

1. **Edit → Project Settings → Tags and Layers** → add a tag named **`IgnoreNavMesh`** if it doesn’t exist.
2. Select any GameObject you want excluded (e.g. planes, pillars, furniture).
3. In the Inspector, set **Tag** to **IgnoreNavMesh**.

**NavMeshBakeOnLayout** (on your Spire generator) automatically moves tagged objects onto the IgnoreNavMesh layer and excludes that layer when building the NavMesh. No manual layer or NavMesh Surface setup needed.

## Option B: Use the IgnoreNavMesh layer

1. **Edit → Project Settings → Tags and Layers**
2. Under **Layers**, pick an empty **User Layer** (e.g. Layer 8) and name it **`IgnoreNavMesh`**.
3. Assign that **Layer** to any GameObject you want excluded.

**NavMeshBakeOnLayout** automatically excludes the **IgnoreNavMesh** layer when baking, so those objects are never included even if the NavMesh Surface Inspector still has “Everything” selected.

## Optional: Editor script

**Tools → DungeonGame → Set Layer "IgnoreNavMesh" On Selected** assigns the IgnoreNavMesh layer to the selected GameObjects (useful if you prefer layers over tags).
