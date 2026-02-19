# Next Setup Steps (MVP)

## 0) Install packages
This repo adds Netcode in `Packages/manifest.json`:
- `com.unity.netcode.gameobjects`

Open the project in Unity and let it resolve packages.

## 1) Create scenes
Create 2 scenes (Unity Editor):
- `Assets/Scenes/Town.unity`
- `Assets/Scenes/Spire_Layer.unity`

For now you can keep SampleScene or delete it later.

## 2) Add NetworkBootstrap
In *both* scenes, add an empty GameObject `Bootstrap` and attach:
- `DungeonGame.Core.NetworkBootstrap`

Play mode hotkeys:
- F1 = Host
- F2 = Client
- F3 = Shutdown

## 3) Add SpireSeed (Spire scene)
In `Spire_Layer` scene:
- Create GameObject `Spire`
- Add `NetworkObject`
- Add script `DungeonGame.Core.SpireSeed`

When host starts, it generates a seed and syncs it to clients.

## 4) Player prefab (later)
Next step is creating a Player prefab with:
- `NetworkObject`
- (optional) `NetworkTransform`
- 3rd person controller

Then assign it on `NetworkManager` (Player Prefab).
