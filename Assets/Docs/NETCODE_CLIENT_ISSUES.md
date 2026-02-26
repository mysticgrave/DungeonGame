# Client Can't Move / Host Invisible + "Receive queue is full"

## What you see

- Joining player can't move and doesn't see the host when the host is in front of them.
- Console: **"Receive queue is full, some packets could be dropped, consider increase its size (128)."**
- Console: **"Messages were received for a trigger of type NetworkTransformMessage associated with id (1), but the NetworkObject was not received within the timeout period"**

## Cause

The transport's **receive packet queue** (default often 128) fills up. When it's full, packets are dropped. If the **spawn** packet for a NetworkObject is dropped, the client never creates that object. So:

- If the **client's own player** spawn packet is dropped → client has no player object → `IsOwner` is never true → **can't move**.
- If the **host's player** spawn packet is dropped → client never gets that NetworkObject → **host is invisible**.

The "NetworkTransformMessage ... but the NetworkObject was not received" warning means the client got transform updates for an object before (or without) getting the object's spawn message—again due to drops or ordering.

## Fixes

### 1. Increase the receive queue (automatic)

The project uses **UnityTransportQueueFix**: it runs from **NetworkBootstrap** and **LobbyMenuController** and tries to set the transport's packet queue size to **512** via reflection. If you see a log `[Transport] Set MaxPacketQueueSize = 512` (or similar), the fix was applied.

- **NetworkManager** in the Main Menu (or Town) can also have the **UnityTransportQueueFix** component. Add it and set **Max Packet Queue Size** to **512** in the Inspector if the script exposes it.

### 2. Increase the queue in the Unity Inspector (if your UTP version allows)

1. Select the GameObject with **NetworkManager**.
2. Find the **Unity Transport** component.
3. If there is a **Max Packet Queue Size** (or **Receive Queue Size**) field, set it to **256** or **512**.

### 3. Checklist

- **Player prefab** is assigned in NetworkManager → **Player Prefab**.
- **Player prefab** is also in **Network Prefabs** (add it to the list if it’s not there).
- Player prefab has **NetworkObject** and **NetworkTransform**; **FirstPersonMotor** only runs when `IsOwner`, so the client must own their spawned player.

After increasing the queue size, rebuild and test again. The "Receive queue is full" warning and the missing player / invisible host issues should go away.
