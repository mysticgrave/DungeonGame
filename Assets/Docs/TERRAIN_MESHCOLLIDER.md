# "MeshCollider is not supported on terrain"

Unity shows this when a **Terrain** GameObject has a **Mesh Collider** component. Terrain must use only **Terrain Collider** for physics.

## Fix

**Option A – In the Editor**

1. Select the **Terrain** object in the Hierarchy (e.g. in the Town scene).
2. In the Inspector, find the **Mesh Collider** component.
3. Right‑click it → **Remove Component**.

**Option B – Menu**

1. In the menu bar: **Tools → DungeonGame → Remove MeshCollider From Terrain**.
2. This removes MeshCollider from every Terrain in the current scene and logs what was removed.

After that, the message should stop. Keep only the **Terrain** and **Terrain Collider** components on the Terrain object.
