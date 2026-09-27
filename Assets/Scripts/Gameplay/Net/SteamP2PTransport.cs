using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Steamworks;
using UnityEngine;

namespace ArenaShooter.Gameplay.Net
{
    /// <summary>
    /// Message transport over Steam Networking Sockets (peer-to-peer through Valve's relays, so no
    /// port forwarding). Peers are identified by Steam ID. The host listens; clients connect to it.
    /// </summary>
    public sealed class SteamP2PTransport : IDisposable
    {
        public event Action<ulong> Connected;
        public event Action<ulong, string> Disconnected;
        /// <summary>peer, buffer, length (buffer is reused: copy what you keep).</summary>
        public event Action<ulong, byte[], int> Received;

        private const int MaxMessagesPerPoll = 128;

        private readonly Dictionary<uint, ulong> _peerByConnection = new Dictionary<uint, ulong>();
        private readonly Dictionary<ulong, HSteamNetConnection> _connectionByPeer = new Dictionary<ulong, HSteamNetConnection>();
        private readonly IntPtr[] _messages = new IntPtr[MaxMessagesPerPoll];
        private byte[] _receiveBuffer = new byte[4096];
        private Callback<SteamNetConnectionStatusChangedCallback_t> _statusCallback;
        private HSteamListenSocket _listen = HSteamListenSocket.Invalid;
        private HSteamNetPollGroup _pollGroup = HSteamNetPollGroup.Invalid;
        private bool _isHost;

        public bool IsHost => _isHost;
        public IEnumerable<ulong> Peers => _connectionByPeer.Keys;

        public void StartHost()
        {
            _isHost = true;
            _statusCallback = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnStatusChanged);
            _listen = SteamNetworkingSockets.CreateListenSocketP2P(0, 0, null);
            _pollGroup = SteamNetworkingSockets.CreatePollGroup();
        }

        public void Connect(ulong hostSteamId)
        {
            _isHost = false;
            _statusCallback = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnStatusChanged);
            var identity = new SteamNetworkingIdentity();
            identity.SetSteamID(new CSteamID(hostSteamId));
            var conn = SteamNetworkingSockets.ConnectP2P(ref identity, 0, 0, null);
            Track(conn, hostSteamId);
        }

        public void Send(ulong peer, byte[] data, int length, bool reliable)
        {
            if (!_connectionByPeer.TryGetValue(peer, out var conn)) return;
            int flags = reliable ? Constants.k_nSteamNetworkingSend_Reliable : Constants.k_nSteamNetworkingSend_UnreliableNoNagle;
            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                SteamNetworkingSockets.SendMessageToConnection(conn, handle.AddrOfPinnedObject(), (uint)length, flags, out _);
            }
            finally
            {
                handle.Free();
            }
        }

        public void SendToAll(byte[] data, int length, bool reliable, ulong except = 0)
        {
            foreach (var peer in new List<ulong>(_connectionByPeer.Keys))
                if (peer != except) Send(peer, data, length, reliable);
        }

        public void Poll()
        {
            if (_isHost)
            {
                if (_pollGroup == HSteamNetPollGroup.Invalid) return;
                Drain(SteamNetworkingSockets.ReceiveMessagesOnPollGroup(_pollGroup, _messages, MaxMessagesPerPoll));
            }
            else
            {
                foreach (var conn in new List<HSteamNetConnection>(_connectionByPeer.Values))
                    Drain(SteamNetworkingSockets.ReceiveMessagesOnConnection(conn, _messages, MaxMessagesPerPoll));
            }
        }

        public void Kick(ulong peer, string reason)
        {
            if (!_connectionByPeer.TryGetValue(peer, out var conn)) return;
            SteamNetworkingSockets.CloseConnection(conn, 0, reason, true);
            Untrack(conn);
        }

        public void Dispose()
        {
            foreach (var conn in new List<HSteamNetConnection>(_connectionByPeer.Values))
                SteamNetworkingSockets.CloseConnection(conn, 0, "Shutting down", true);
            _connectionByPeer.Clear();
            _peerByConnection.Clear();
            if (_listen != HSteamListenSocket.Invalid) SteamNetworkingSockets.CloseListenSocket(_listen);
            if (_pollGroup != HSteamNetPollGroup.Invalid) SteamNetworkingSockets.DestroyPollGroup(_pollGroup);
            _listen = HSteamListenSocket.Invalid;
            _pollGroup = HSteamNetPollGroup.Invalid;
            _statusCallback?.Dispose();
            _statusCallback = null;
        }

        private void Drain(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var msg = SteamNetworkingMessage_t.FromIntPtr(_messages[i]);
                try
                {
                    if (!_peerByConnection.TryGetValue(msg.m_conn.m_HSteamNetConnection, out ulong peer)) continue;
                    if (msg.m_cbSize > _receiveBuffer.Length) _receiveBuffer = new byte[msg.m_cbSize];
                    Marshal.Copy(msg.m_pData, _receiveBuffer, 0, msg.m_cbSize);
                    Received?.Invoke(peer, _receiveBuffer, msg.m_cbSize);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Net] Dropped a bad message: " + e.Message);
                }
                finally
                {
                    SteamNetworkingMessage_t.Release(_messages[i]);
                }
            }
        }

        private void OnStatusChanged(SteamNetConnectionStatusChangedCallback_t status)
        {
            var conn = status.m_hConn;
            var info = status.m_info;
            ulong peer = info.m_identityRemote.GetSteamID().m_SteamID;

            switch (info.m_eState)
            {
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                    // Incoming connection to our listen socket: accept it (friends-only lobby gates who finds us).
                    if (_isHost && info.m_hListenSocket == _listen)
                    {
                        if (SteamNetworkingSockets.AcceptConnection(conn) == EResult.k_EResultOK)
                        {
                            SteamNetworkingSockets.SetConnectionPollGroup(conn, _pollGroup);
                            Track(conn, peer);
                        }
                        else SteamNetworkingSockets.CloseConnection(conn, 0, "Accept failed", false);
                    }
                    break;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                    if (_peerByConnection.ContainsKey(conn.m_HSteamNetConnection)) Connected?.Invoke(peer);
                    break;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                    bool known = _peerByConnection.ContainsKey(conn.m_HSteamNetConnection);
                    SteamNetworkingSockets.CloseConnection(conn, 0, null, false);
                    if (known)
                    {
                        Untrack(conn);
                        string reason = info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer
                            ? "Connection closed." : "Connection lost.";
                        Disconnected?.Invoke(peer, reason);
                    }
                    break;
            }
        }

        private void Track(HSteamNetConnection conn, ulong peer)
        {
            _peerByConnection[conn.m_HSteamNetConnection] = peer;
            _connectionByPeer[peer] = conn;
        }

        private void Untrack(HSteamNetConnection conn)
        {
            if (_peerByConnection.TryGetValue(conn.m_HSteamNetConnection, out ulong peer))
            {
                _peerByConnection.Remove(conn.m_HSteamNetConnection);
                _connectionByPeer.Remove(peer);
            }
        }
    }
}
