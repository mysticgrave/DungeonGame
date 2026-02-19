# Spire Torches (MVP)

## Goal
- Spire floors can be full darkness.
- Wall torches exist along walls.
- Each spire seed deterministically decides which torches start lit.
- Players can light unlit torches (networked) by pressing E.

## Scripts added
- `DungeonGame.Spire.WallTorch`
- `DungeonGame.Spire.TorchSeeder` (attach to the same object as `SpireSeed`)
- `DungeonGame.Player.TorchInteractor` (attach to Player prefab)

## Setup steps
### 1) Spire seed + seeder
In `Spire_Layer` scene:
- On your `Spire` GameObject (with `NetworkObject` + `SpireSeed`)
- Add component: `TorchSeeder`

### 2) Create a WallTorch prefab
Create `Assets/Prefabs/WallTorch.prefab`:
- Root: empty
- Add `NetworkObject`
- Add `WallTorch`
- Add a child `Point Light` (assign to WallTorch.torchLight)
- Optional: flame particle system (assign to flameVfx)
- Add a collider (for raycast hit) on root or child (e.g. BoxCollider)

Place multiple WallTorch instances in the `Spire_Layer` scene.

### 3) Player interaction
On `Player.prefab` root add:
- `TorchInteractor`

Press **E** while looking at a torch to light it.

## Notes
- For MVP, torches are always allow-lit (no item cost yet).
- Later: require tinder/oil, make lighting noisy, and/or allow snuffing.
