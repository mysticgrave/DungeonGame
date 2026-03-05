#if !DISABLESTEAMWORKS
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Steamworks;
using Steamworks.Data;
using Unity.Netcode;
using UnityEngine;

namespace DungeonGame.Core
{
    /// <summary>
    /// NGO NetworkTransport using Facepunch.Steamworks (Steam Networking Sockets).
    /// All traffic goes through Steam's relay — no port forwarding needed.
    ///
    /// Setup:
    ///   1. Install Facepunch.Steamworks DLLs in Assets/Plugins/
    ///   2. Add this component to the NetworkManager GameObject
    ///   3. Set it as the Network Transport in NetworkManager
    ///   4. Set targetSteamId on the client before calling StartClient()
    /// </summary>
    public class FacepunchTransport : NetworkTransport
    {
        [Tooltip("Steam ID of the host to connect to (set at runtime by SteamLobbyManager).")]
        public ulong targetSteamId;

        public override ulong ServerClientId => 0;

        private Server _server;
        private Client _client;

        private readonly Queue<TransportEvent> _eventQueue = new();

        private struct TransportEvent
        {
            public NetworkEvent Type;
            public ulong ClientId;
            public ArraySegment<byte> Payload;
        }

        public override void Initialize(NetworkManager networkManager = null)
        {
            SteamNetworkingUtils.InitRelayNetworkAccess();
        }

        public override bool StartServer()
        {
            Server.Transport = this;
            _server = SteamNetworkingSockets.CreateRelaySocket<Server>();
            Debug.Log("[FacepunchTransport] Server started via Steam Relay.");
            return true;
        }

        public override bool StartClient()
        {
            if (targetSteamId == 0)
            {
                Debug.LogError("[FacepunchTransport] targetSteamId not set. Cannot connect.");
                return false;
            }

            Client.Transport = this;
            _client = SteamNetworkingSockets.ConnectRelay<Client>(targetSteamId);
            Debug.Log($"[FacepunchTransport] Client connecting to Steam ID {targetSteamId}");
            return true;
        }

        public override void Shutdown()
        {
            _server?.Stop();
            _server = null;
            _client?.Disconnect();
            _client = null;
            _eventQueue.Clear();
#if UNITY_EDITOR
            Debug.Log($"[FacepunchTransport] Shutdown.\nCall stack:\n{Environment.StackTrace}");
#else
            Debug.Log("[FacepunchTransport] Shutdown.");
#endif
        }

        public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery delivery)
        {
            var sendType = DeliveryToSendType(delivery);
            byte[] data = new byte[payload.Count];
            Array.Copy(payload.Array!, payload.Offset, data, 0, payload.Count);

            if (_server != null)
            {
                _server.SendToClient(clientId, data, sendType);
            }
            else if (_client != null)
            {
                var result = _client.Connection.SendMessage(data, sendType);
                if (result != Result.OK)
                    Debug.LogWarning($"[FacepunchTransport] CLIENT Send failed: {result} (bytes={data.Length}, delivery={delivery})");
            }
        }

        public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
        {
            // CRITICAL: Pump Steam callbacks BEFORE receiving messages.
            // NGO calls PollEvent during EarlyUpdate, but SteamManager.Update()
            // (which calls RunCallbacks) runs later in Update. Without this,
            // Steam's internal packet routing hasn't processed incoming data yet
            // and ReceiveMessagesOnPollGroup/Connection returns 0 messages.
            if (SteamClient.IsValid)
                SteamClient.RunCallbacks();

            _server?.Poll();
            _client?.Poll();

            if (_eventQueue.Count > 0)
            {
                var ev = _eventQueue.Dequeue();
                clientId = ev.ClientId;
                payload = ev.Payload;
                receiveTime = Time.realtimeSinceStartup;
                return ev.Type;
            }

            clientId = 0;
            payload = default;
            receiveTime = Time.realtimeSinceStartup;
            return NetworkEvent.Nothing;
        }

        public override void DisconnectRemoteClient(ulong clientId)
        {
            Debug.Log($"[FacepunchTransport] SERVER: DisconnectRemoteClient(clientId={clientId}) — Netcode requested disconnect.");
            _server?.DisconnectClient(clientId);
        }

        public override void DisconnectLocalClient()
        {
            _client?.Disconnect();
            _client = null;
        }

        public override ulong GetCurrentRtt(ulong clientId) => 0;

        internal void EnqueueEvent(NetworkEvent type, ulong clientId, byte[] data = null)
        {
            _eventQueue.Enqueue(new TransportEvent
            {
                Type = type,
                ClientId = clientId,
                Payload = data != null ? new ArraySegment<byte>(data) : default,
            });
        }

        private static SendType DeliveryToSendType(NetworkDelivery delivery)
        {
            return delivery switch
            {
                NetworkDelivery.Unreliable => SendType.Unreliable,
                NetworkDelivery.UnreliableSequenced => SendType.Unreliable,
                NetworkDelivery.Reliable => SendType.Reliable,
                NetworkDelivery.ReliableSequenced => SendType.Reliable,
                NetworkDelivery.ReliableFragmentedSequenced => SendType.Reliable,
                _ => SendType.Reliable,
            };
        }

        // ===== Server (Host) =====

        private class Server : SocketManager
        {
            internal static FacepunchTransport Transport;

            private readonly Dictionary<uint, ulong> _connToClient = new();
            private readonly Dictionary<ulong, Connection> _clientToConn = new();
            private ulong _nextClientId = 1;

            public void Stop()
            {
                foreach (var kvp in _clientToConn)
                    kvp.Value.Close();

                _connToClient.Clear();
                _clientToConn.Clear();
                Socket.Close();
            }

            public void Poll()
            {
                try
                {
                    Receive(256);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[FacepunchTransport] Server.Poll error: {ex}");
                }
            }

            public void SendToClient(ulong clientId, byte[] data, SendType sendType)
            {
                if (_clientToConn.TryGetValue(clientId, out var conn))
                    conn.SendMessage(data, sendType);
            }

            public void DisconnectClient(ulong clientId)
            {
                if (_clientToConn.TryGetValue(clientId, out var conn))
                {
                    conn.Close();
                    var connId = conn.Id;
                    _clientToConn.Remove(clientId);
                    _connToClient.Remove(connId);
                }
            }

            public override void OnConnecting(Connection connection, ConnectionInfo info)
            {
                base.OnConnecting(connection, info);
            }

            public override void OnConnected(Connection connection, ConnectionInfo info)
            {
                base.OnConnected(connection, info); // Required: base registers connection for message routing (Facepunch #467, #529)
                ulong clientId = _nextClientId++;
                _connToClient[connection.Id] = clientId;
                _clientToConn[clientId] = connection;
                Transport?.EnqueueEvent(NetworkEvent.Connect, clientId);
                Debug.Log($"[FacepunchTransport] Client {clientId} connected (Steam: {info.Identity.SteamId})");
            }

            public override void OnDisconnected(Connection connection, ConnectionInfo info)
            {
                base.OnDisconnected(connection, info);
                if (_connToClient.TryGetValue(connection.Id, out ulong clientId))
                {
                    _connToClient.Remove(connection.Id);
                    _clientToConn.Remove(clientId);
                    Transport?.EnqueueEvent(NetworkEvent.Disconnect, clientId);
                    Debug.Log($"[FacepunchTransport] Client {clientId} disconnected.");
                }
            }

            public override void OnMessage(Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel)
            {
                base.OnMessage(connection, identity, data, size, messageNum, recvTime, channel); // Required for message pipeline
                if (!_connToClient.TryGetValue(connection.Id, out ulong clientId))
                {
                    Debug.LogWarning($"[FacepunchTransport] SERVER: OnMessage from unknown connection {connection.Id}, size={size} — dropped.");
                    return;
                }
                Debug.Log($"[FacepunchTransport] SERVER: Received message from client {clientId}, size={size}.");
                byte[] managed = new byte[size];
                Marshal.Copy(data, managed, 0, size);
                Transport?.EnqueueEvent(NetworkEvent.Data, clientId, managed);
            }
        }

        // ===== Client =====

        private class Client : ConnectionManager
        {
            internal static FacepunchTransport Transport;

            public void Poll()
            {
                try
                {
                    Receive(256);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[FacepunchTransport] Client.Poll error: {ex}");
                }
            }

            public void Disconnect()
            {
                Connection.Close();
            }

            public override void OnConnected(ConnectionInfo info)
            {
                base.OnConnected(info);
                Transport?.EnqueueEvent(NetworkEvent.Connect, Transport.ServerClientId);
                Debug.Log("[FacepunchTransport] CLIENT: Connected to host (Steam relay). Netcode will process Connect next.");
            }

            public override void OnDisconnected(ConnectionInfo info)
            {
                base.OnDisconnected(info);
                Transport?.EnqueueEvent(NetworkEvent.Disconnect, Transport.ServerClientId);
                Debug.Log($"[FacepunchTransport] CLIENT: Disconnected from host. State={info.State} EndReason={info.EndReason}");
            }

            public override void OnConnecting(ConnectionInfo info)
            {
                base.OnConnecting(info);
                Debug.Log("[FacepunchTransport] CLIENT: Connecting via Steam relay...");
            }

            public override void OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
            {
                base.OnMessage(data, size, messageNum, recvTime, channel);
                byte[] managed = new byte[size];
                Marshal.Copy(data, managed, 0, size);
                Transport?.EnqueueEvent(NetworkEvent.Data, Transport.ServerClientId, managed);
            }
        }
    }
}
#endif
