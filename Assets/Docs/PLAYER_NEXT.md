# Player (MVP) — Local Camera + Movement

## Goal
Get a playable third-person character that works in Host + Client testing.

## Add to Player prefab
On the **root** of `Assets/Prefabs/Player.prefab`:
- `NetworkObject`
- `NetworkTransform`
  - IMPORTANT: set authority to **Owner/Client** (so the owning client can move it)
- `CharacterController`
- `DungeonGame.Player.ThirdPersonMotor`
- `DungeonGame.Player.LocalPlayerCameraRig`

Visuals:
- Add a Capsule mesh (or leave the root as a Capsule).

## Remove extra AudioListeners
- Your Town scene Main Camera likely has an AudioListener.
- Disable/remove the AudioListener on the Town camera.
- The player will spawn a local-only camera with the only AudioListener.

## Controls
- WASD: move
- Shift: sprint
- Space: jump
- Mouse: look

## Test
1) Start Host (F1)
2) Start Client in build (F2)
3) Both should be able to move their own character.
