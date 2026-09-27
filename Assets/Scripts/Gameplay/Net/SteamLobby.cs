using System;
using System.Collections.Generic;
using Steamworks;

namespace ArenaShooter.Gameplay.Net
{
    /// <summary>A Steam friend shown in the Join / Invite lists.</summary>
    public struct FriendEntry
    {
        public ulong Id;
        public string Name;
        /// <summary>Their Space Grunts lobby, or 0 if they aren't hosting one.</summary>
        public ulong Lobby;
    }

    /// <summary>
    /// Friends-only Steam lobbies: hosting, joining, invites (overlay or in-game list), accepting
    /// invites from Steam, and finding friends who are hosting. The lobby only brings players
    /// together; gameplay traffic goes over <see cref="SteamP2PTransport"/> to the lobby owner.
    /// </summary>
    public sealed class SteamLobby : IDisposable
    {
        public const string GameKey = "game";
        public const string GameValue = "spacegrunts";
        public const string VersionKey = "version";
        public const int MaxPlayers = 8;

        public ulong CurrentLobby { get; private set; }
        public bool IsHost { get; private set; }

        /// <summary>Someone accepted an invite (or clicked "Join Game" in Steam) while we're running.</summary>
        public static event Action<ulong> JoinRequested;

        private readonly CallResult<LobbyCreated_t> _created = CallResult<LobbyCreated_t>.Create();
        private readonly CallResult<LobbyEnter_t> _entered = CallResult<LobbyEnter_t>.Create();
        private static Callback<GameLobbyJoinRequested_t> _joinRequest;
        private static readonly HashSet<ulong> VerifiedLobbies = new HashSet<ulong>();
        private static Callback<LobbyDataUpdate_t> _lobbyData;

        /// <summary>Listen for invites; call once after Steam starts.</summary>
        public static void RegisterInviteListener()
        {
            if (!SteamService.IsReady || _joinRequest != null) return;
            _joinRequest = Callback<GameLobbyJoinRequested_t>.Create(r => JoinRequested?.Invoke(r.m_steamIDLobby.m_SteamID));
            _lobbyData = Callback<LobbyDataUpdate_t>.Create(r =>
            {
                var lobby = new CSteamID(r.m_ulSteamIDLobby);
                if (SteamMatchmaking.GetLobbyData(lobby, GameKey) == GameValue) VerifiedLobbies.Add(r.m_ulSteamIDLobby);
            });
        }

        /// <summary>A lobby ID passed on the command line when Steam launched us to join a friend.</summary>
        public static ulong LobbyFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "+connect_lobby" && ulong.TryParse(args[i + 1], out ulong id)) return id;
            return 0;
        }

        public void Host(Action<bool, string> done)
        {
            var call = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, MaxPlayers);
            _created.Set(call, (result, ioFailure) =>
            {
                if (ioFailure || result.m_eResult != EResult.k_EResultOK)
                {
                    done(false, "Couldn't create a Steam lobby (" + result.m_eResult + ").");
                    return;
                }
                CurrentLobby = result.m_ulSteamIDLobby;
                IsHost = true;
                var lobby = new CSteamID(CurrentLobby);
                SteamMatchmaking.SetLobbyData(lobby, GameKey, GameValue);
                SteamMatchmaking.SetLobbyData(lobby, VersionKey, ArenaShooter.Core.Net.NetProtocol.Version.ToString());
                SteamMatchmaking.SetLobbyJoinable(lobby, true);
                done(true, null);
            });
        }

        /// <summary>Enters a lobby and reports the host's Steam ID (the lobby owner).</summary>
        public void Join(ulong lobbyId, Action<bool, ulong, string> done)
        {
            var call = SteamMatchmaking.JoinLobby(new CSteamID(lobbyId));
            _entered.Set(call, (result, ioFailure) =>
            {
                if (ioFailure || result.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
                {
                    done(false, 0, "Couldn't join that game (it may be full or closed).");
                    return;
                }
                var lobby = new CSteamID(result.m_ulSteamIDLobby);
                if (SteamMatchmaking.GetLobbyData(lobby, GameKey) != GameValue)
                {
                    SteamMatchmaking.LeaveLobby(lobby);
                    done(false, 0, "That isn't a Space Grunts game.");
                    return;
                }
                if (SteamMatchmaking.GetLobbyData(lobby, VersionKey) != ArenaShooter.Core.Net.NetProtocol.Version.ToString())
                {
                    SteamMatchmaking.LeaveLobby(lobby);
                    done(false, 0, "Your friend has a different version of the game. Make sure you both have the same build.");
                    return;
                }
                CurrentLobby = result.m_ulSteamIDLobby;
                IsHost = false;
                done(true, SteamMatchmaking.GetLobbyOwner(lobby).m_SteamID, null);
            });
        }

        public void OpenInviteOverlay()
        {
            if (CurrentLobby != 0) SteamFriends.ActivateGameOverlayInviteDialog(new CSteamID(CurrentLobby));
        }

        public bool Invite(ulong friend) => CurrentLobby != 0 && SteamMatchmaking.InviteUserToLobby(new CSteamID(CurrentLobby), new CSteamID(friend));

        public void Leave()
        {
            if (CurrentLobby != 0) SteamMatchmaking.LeaveLobby(new CSteamID(CurrentLobby));
            CurrentLobby = 0;
            IsHost = false;
        }

        public void Dispose()
        {
            Leave();
            _created.Dispose();
            _entered.Dispose();
        }

        /// <summary>Online friends (for the in-game invite list), with their lobby if they're hosting Space Grunts.</summary>
        public static List<FriendEntry> Friends(bool onlyHosting)
        {
            var list = new List<FriendEntry>();
            if (!SteamService.IsReady) return list;
            uint ourApp = SteamUtils.GetAppID().m_AppId;
            int count = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
            for (int i = 0; i < count; i++)
            {
                var id = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
                if (SteamFriends.GetFriendPersonaState(id) == EPersonaState.k_EPersonaStateOffline) continue;

                ulong lobby = 0;
                if (SteamFriends.GetFriendGamePlayed(id, out FriendGameInfo_t game) && game.m_gameID.AppID().m_AppId == ourApp && game.m_steamIDLobby.IsValid())
                {
                    // App 480 is shared by many test games: only list lobbies tagged as ours.
                    ulong candidate = game.m_steamIDLobby.m_SteamID;
                    if (VerifiedLobbies.Contains(candidate)) lobby = candidate;
                    else SteamMatchmaking.RequestLobbyData(game.m_steamIDLobby);
                }
                if (onlyHosting && lobby == 0) continue;
                list.Add(new FriendEntry { Id = id.m_SteamID, Name = SteamFriends.GetFriendPersonaName(id), Lobby = lobby });
            }
            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return list;
        }
    }
}
