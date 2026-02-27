# Joining via IP (port forwarding)

To let a friend join your game over the internet (not on the same LAN), you need port forwarding and the host must listen on all interfaces.

## Steam open and hosting

Having **Steam open and running is required** when you use the Steam "Host" button (lobby creation, invites, lobby list). It does not cause conflicts. When you host via Steam we still set the host to listen on `0.0.0.0` if you use UnityTransport, so port forwarding and direct-IP join work the same. For full Steam-based connections (no port forwarding), use FacepunchTransport on the NetworkManager; then all game traffic goes through Steam.

## What we changed in code

- **Host** now calls `SetConnectionData("0.0.0.0", port, "0.0.0.0")` so the server listens on **all network interfaces**, not only localhost. That allows connections from your public IP through the router.
- **Client** still uses the IP and port you type (your friend enters **your public IP** and **7777**).

## Checklist for the host (you)

1. **Port forwarding on your router**
   - Protocol: **UDP** (Unity Transport uses UDP).
   - External port: **7777** (or whatever you use).
   - Internal IP: your PC's local IP (e.g. 192.168.1.x). Find it: `ipconfig` (Windows) or router admin.
   - Internal port: **7777** (same as external unless you want to remap).

2. **Windows Firewall**
   - Allow inbound **UDP 7777** for your game executable (or "Unity" when testing in Editor).
   - Or temporarily disable firewall to test, then add a rule.

3. **Your public IP**
   - Tell your friend the **public** IP (e.g. from [whatismyip.com](https://whatismyip.com)), not 192.168.x.x.
   - If your public IP changes (typical for home connections), they'll need the new one each time unless you use a static IP or DDNS.

## Checklist for the friend (client)

1. In the game, open **Join** and enter:
   - **Address:** your public IP (e.g. `98.123.45.67`).
   - **Port:** `7777`.
2. Click **Join** (or press Enter).

## Verifying the host is listening on all interfaces

- **In Unity Inspector:** Select the GameObject that has the **Network Manager**. On the same object you should see a **Unity Transport** component. Expand **Connection Data**; you should see **Address**, **Port**, and **Server Listen Address**. For external joins, **Server Listen Address** should be `0.0.0.0` (the code sets this when you host from the menu or bootstrap).
- **At runtime:** When you start as host, check the Console for a log like:  
  `[Lobby] Host transport: listen 0.0.0.0:7777 (Address=0.0.0.0)` or `[Net] Host transport: listen 0.0.0.0:7777`.  
  If you see `(null)` or `127.0.0.1` for the listen address, the transport is not being set for that code path (e.g. hosting from a different scene/button).
- **netstat:** After hosting, run `netstat -an | findstr 7777` (Windows). You should see a line with `0.0.0.0:7777` or `[::]:7777` for UDP. If it only shows `127.0.0.1:7777`, the app is still binding to localhost.

## If it still doesn't connect

- **Host:** Confirm the game is actually listening on 7777 (e.g. `netstat -an | findstr 7777` on Windows; you should see UDP 0.0.0.0:7777 or your local IP:7777).
- **Router:** Ensure the port-forward rule is **UDP** and points to the correct internal IP.
- **Same LAN:** If your friend is on the same Wi‑Fi/network, they can try your **local** IP (192.168.x.x) and 7777 instead of the public IP.
- **Steam:** If you're using Steam + Facepunch transport, you don't need port forwarding; friends join via Steam. This doc is for **direct IP** (Unity Transport) only.
