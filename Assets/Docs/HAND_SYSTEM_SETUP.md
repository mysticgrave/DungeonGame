# Two-Hand Item System Setup

The HandSystem lets players hold items in left and right hands. Items can be one-handed or two-handed. Pickup uses **ItemRegistry** (all holdable items), not WeaponRegistry (shop only).

---

## 1. ItemRegistry (Required)

**ItemRegistry** is the source of truth for what can be picked up. If an item's config is not in ItemRegistry, pickup will silently fail.

### Setup

1. In your **Town** scene (or a persistent bootstrap scene), create an empty GameObject.
2. Add the **Item Registry** component (`DungeonGame.Items.ItemRegistry`).
3. In the **Items** list, add **every** holdable item:
   - All weapons (swords, bows, etc.) — same configs as WeaponRegistry
   - Torches, potions, shields, wands — any non-weapon you want pickable

**Order matters**: Indexes are synced over the network. Don't reorder after release.

### WeaponRegistry vs ItemRegistry

| Registry       | Purpose                          | Contains                    |
|----------------|----------------------------------|-----------------------------|
| **WeaponRegistry** | Shop UI, MetaProgression, buy/equip | Weapons only                |
| **ItemRegistry**   | HandSystem pickup, hold, drop       | All holdable items (weapons + torches, potions, etc.) |

For weapons: add the same config to **both** registries. For torches/potions: add only to **ItemRegistry**.

---

## 2. WorldItem Prefabs

Each pickable item in the world needs:

1. **WorldItem** component
2. **NetworkObject** (for networking)
3. **Rigidbody** (for physics when dropped)
4. **Item Config** assigned — must be a config that exists in **ItemRegistry**

### Common Pickup Failures

- **Config not in ItemRegistry** — Add the config to ItemRegistry's Items list.
- **pickupLayers** — HandSystem uses a raycast. Default is "Everything" (all layers). If you restricted it, ensure WorldItems are on a hit layer.
- **Camera rig null** — HandSystem needs `LocalPlayerCameraRig`; the ray uses the camera forward. Ensure the Player has this component.

---

## 3. Non-Weapon Items (Torches, Potions, etc.)

Use **WeaponConfig** for all items. For non-weapons:

1. **Create** → **DungeonGame** → **Item Config (Holdable)** — creates a config with `attackType = None`.
2. Set **weaponId** (e.g. `torch`, `potion_health`) — used for ItemRegistry lookup.
3. Set **grip** — OneHanded or TwoHanded.
4. Assign **worldPrefab** and **heldVisualPrefab**.
5. Set **attackType** = **None** — no combat; use action can still play an animation.
6. Add this config to **ItemRegistry**.

---

## 4. Scene Checklist

- [ ] **ItemRegistry** exists in Town (or persistent scene) with all holdable items
- [ ] **WeaponRegistry** has weapons for the shop (can overlap with ItemRegistry)
- [ ] WorldItem prefabs have their **Item Config** in ItemRegistry
- [ ] Player has **HandSystem** with **leftHandBone** and **rightHandBone** assigned
- [ ] Player has **LocalPlayerCameraRig** (for pickup raycast)

---

## 5. Input

- **LMB** — Left hand: pickup (if empty) or use
- **RMB** — Right hand: pickup (if empty) or use
- **Hold G + LMB/RMB** — Drop/throw item
- **F** — Interact (e.g. light torches)
