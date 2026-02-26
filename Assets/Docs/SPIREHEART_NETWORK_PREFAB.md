# Adding Spire Heart to the Network Prefab List

Netcode for GameObjects only allows spawning prefabs that are in a **Network Prefabs List**. Spire Heart must be in that list even if you only **place it in the scene** (so the network knows the prefab).

---

## Step 1: Use the list asset (not the NetworkManager object)

In this project the list is stored in an **asset**:

1. In the **Project** window, go to **Assets**.
2. Click the asset named **DefaultNetworkPrefabs** (type: Network Prefabs List / ScriptableObject).
3. The **Inspector** will show something like **Default Network Prefabs** with a **List** of prefabs.

Do **not** try to add prefabs from the NetworkManager’s inspector unless it has its own inline list; this project uses the separate asset.

---

## Step 2: Add Spire Heart to the list

1. With **DefaultNetworkPrefabs** selected in the Project window, look at the Inspector.
2. Find the **List** (or **Prefabs**).
3. **Increase Size** by 1 (or click the **+** button if there is one).
4. A new **Element** slot will appear.
5. From the **Project** window, **drag the SpireHeart prefab** into that slot.

If the slot accepts the prefab, you’re done. Save (Ctrl+S).

---

## Step 3: If the slot doesn’t accept the prefab

- **Spire Heart must have a NetworkObject on its root.**  
  Open the SpireHeart prefab and ensure the **root** GameObject has the **Network Object** component. If it’s missing, Add Component → Network Object.

- **Drag the prefab, not the scene instance.**  
  Drag from **Project** (e.g. `Assets/Prefabs/SpireHeart.prefab`), not from the Hierarchy when a scene is open.

- **Try a different list.**  
  If the NetworkManager in the Town scene has a **Network Prefabs Lists** array, it might reference **DefaultNetworkPrefabs**. If you have another list asset there, add SpireHeart to that list instead (and ensure that list is what the NetworkManager uses).

- **Prefab must be saved.**  
  If SpireHeart was just created, save the prefab (and the scene if it’s placed) before adding to the list.

---

## Step 4: Confirm

1. Select **DefaultNetworkPrefabs** again.
2. In the Inspector, the list should show your **SpireHeart** prefab in one of the elements.
3. Enter Play Mode as host and load the Spire scene. If the heart is placed in the scene, it should sync. If you spawn it by code, spawning will only work if it’s in this list.

---

## If you still can’t add it

Some Unity/Netcode versions show the list in a different way:

- **NetworkManager** in the Hierarchy: select it and look for a section like **Network Prefabs** or **Spawnable Prefabs**. If there’s a single field that references **DefaultNetworkPrefabs**, then editing that asset (Steps 1–2) is correct. If there’s an inline list on the NetworkManager, add SpireHeart there.
- **Duplicate an existing entry**: In the list, duplicate an element that already has a prefab, then change the new element’s prefab reference to SpireHeart.

Once SpireHeart is in the list that your NetworkManager uses, it can be used in the Spire scene (placed or spawned).
