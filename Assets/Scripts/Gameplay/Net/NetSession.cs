using System;
using System.Collections.Generic;
using ArenaShooter.Core;
using ArenaShooter.Core.Net;
using UnityEngine;

namespace ArenaShooter.Gameplay.Net
{
    public enum NetRole
    {
        Offline,
        Host,
        Client,
    }

    /// <summary>
    /// Online play over Steam (GDD 9.1). The host runs the real match exactly like offline (bots,
    /// health, pickups, score) and streams it to clients. Each client moves its own grunt locally,
    /// reports its shots and pickup requests, and shows everyone else from host snapshots.
    /// </summary>
    public class NetSession : MonoBehaviour
    {
        public static NetSession Current { get; private set; }
        public static bool IsOnline => Current != null && Current.Role != NetRole.Offline;
        public static bool IsClient => Current != null && Current.Role == NetRole.Client;
        public static bool IsHost => Current != null && Current.Role == NetRole.Host;

        public NetRole Role { get; private set; }
        public SteamLobby Lobby { get; private set; }
        /// <summary>What's happening, for the menus ("Joining...", errors).</summary>
        public string Status { get; private set; } = "";
        public int PlayerCount
        {
            get
            {
                if (Role == NetRole.Host) return _players.Count + 1;
                int humans = 0;
                foreach (var e in _roster)
                    if (!e.IsBot) humans++;
                return humans;
            }
        }

        /// <summary>Raised for short on-screen messages ("Sgt. X joined").</summary>
        public static event Action<string> Notice;

        private ArenaBootstrap _flow;
        private SteamP2PTransport _transport;
        private readonly NetWriter _writer = new NetWriter(1024);

        // ---- host
        private sealed class RemotePlayer
        {
            public ulong Peer;
            public string Name;
            public Combatant Combatant;
            public float LastShotAt = -10f;
        }

        private readonly Dictionary<ulong, RemotePlayer> _players = new Dictionary<ulong, RemotePlayer>();
        private MatchManager _match;
        private Combatant _hostPlayer;
        private MatchSetup _setup;
        private float _snapshotTimer;
        private readonly Dictionary<Combatant, int> _lives = new Dictionary<Combatant, int>();
        private static readonly Color[] PlayerColors =
        {
            new Color(0.96f, 0.96f, 0.98f), new Color(0.2f, 0.95f, 0.75f), new Color(1f, 0.45f, 0.9f), new Color(0.6f, 0.45f, 1f),
            new Color(1f, 0.62f, 0.35f), new Color(0.45f, 0.8f, 1f), new Color(0.85f, 1f, 0.35f), new Color(1f, 0.35f, 0.45f),
        };

        // ---- client
        private ulong _hostPeer;
        private int _localId = NetProtocol.NoId;
        private Combatant _local;
        private int _localLife;
        private float _stateTimer;
        private readonly ClockSync _clock = new ClockSync();
        private readonly List<RosterEntry> _roster = new List<RosterEntry>();
        private readonly Dictionary<int, Combatant> _proxies = new Dictionary<int, Combatant>();
        private readonly List<byte[]> _pendingUntilWorld = new List<byte[]>();
        private bool _worldReady;
        private float _joinStartedAt;
        private const float JoinTimeout = 20f;

        public static NetSession Create(ArenaBootstrap flow)
        {
            if (Current != null) return Current;
            var go = new GameObject("NetSession");
            DontDestroyOnLoad(go);
            var session = go.AddComponent<NetSession>();
            session._flow = flow;
            Current = session;
            return session;
        }

        // ================================================================= lifecycle

        /// <summary>Creates a friends-only lobby and starts listening. The match itself starts after.</summary>
        public void BeginHosting(MatchSetup setup, Action<bool, string> done)
        {
            Shutdown(null);
            _setup = setup.Clone();
            Status = "Creating Steam lobby...";
            Lobby = new SteamLobby();
            Lobby.Host((ok, error) =>
            {
                if (!ok)
                {
                    Status = error;
                    Shutdown(null);
                    done(false, error);
                    return;
                }
                _transport = new SteamP2PTransport();
                _transport.Received += OnHostReceived;
                _transport.Disconnected += (peer, reason) => RemovePlayer(peer, reason);
                _transport.StartHost();
                Role = NetRole.Host;
                Status = "Hosting";
                done(true, null);
            });
        }

        /// <summary>Host: the match world is built; start streaming it.</summary>
        public void AttachHost(MatchManager match, Combatant hostPlayer)
        {
            _match = match;
            _hostPlayer = hostPlayer;
            _hostPlayer.NetOwner = SteamService.LocalId;
            _match.Killed += OnHostKill;
            _match.MatchBegan += () => Broadcast(new MatchResetMsg(), true);
            WeaponHolder.AnyShotFired += OnHostShotFired;
            foreach (var c in _match.Combatants) WatchCombatant(c);
        }

        public void BeginJoining(ulong lobbyId, Action<string> failed)
        {
            Shutdown(null);
            Status = "Joining your friend's game...";
            Lobby = new SteamLobby();
            Lobby.Join(lobbyId, (ok, hostId, error) =>
            {
                if (!ok)
                {
                    Status = error;
                    Shutdown(null);
                    failed(error);
                    return;
                }
                Status = "Connecting to host...";
                _joinStartedAt = Time.unscaledTime;
                _hostPeer = hostId;
                _transport = new SteamP2PTransport();
                _transport.Received += OnClientReceived;
                _transport.Connected += _ => Send(_hostPeer, new HelloMsg { Name = SteamService.LocalName }, true);
                _transport.Disconnected += (_, reason) => LeaveToMenu("Lost connection to the host. " + reason);
                _transport.Connect(hostId);
                Role = NetRole.Client;
            });
        }

        /// <summary>Client: the world for the host's match is built; start playing.</summary>
        public void AttachClient(MatchManager match, Combatant localPlayer)
        {
            _match = match;
            _local = localPlayer;
            _local.AssignId(_localId);
            _local.NetOwner = SteamService.LocalId;
            _match.AddCombatant(_local);
            _local.Weapons.ShotReported += OnLocalShot;
            _local.Weapons.PickupRequested += p => Send(_hostPeer, new PickupRequestMsg { PickupId = p.NetId }, true);
            _local.Health.Died += (_, __) => _local.Weapons.DiscardOnDeath();
            _local.SetPresent(false); // wait for the host to spawn us
            _worldReady = true;
            ApplyRoster();
            foreach (var bytes in _pendingUntilWorld) HandleClientMessage(bytes, bytes.Length);
            _pendingUntilWorld.Clear();
            Status = "Playing online";
        }

        /// <summary>Leave the game (host: ends it for everyone).</summary>
        public void Shutdown(string reason)
        {
            try
            {
                if (Role == NetRole.Host) Broadcast(new GoodbyeMsg { Reason = reason ?? "The host left the game." }, true);
                else if (Role == NetRole.Client && _transport != null) Send(_hostPeer, new GoodbyeMsg { Reason = "left" }, true);
            }
            catch (Exception) { /* Steam already gone */ }

            WeaponHolder.AnyShotFired -= OnHostShotFired;
            try
            {
                _transport?.Dispose();
                Lobby?.Dispose();
            }
            catch (Exception e)
            {
                // Steam may already be shut down when quitting; nothing left to close then.
                Debug.Log("[Net] Shutdown: " + e.Message);
            }
            _transport = null;
            Lobby = null;
            Role = NetRole.Offline;
            _players.Clear();
            _proxies.Clear();
            _roster.Clear();
            _lives.Clear();
            _pendingUntilWorld.Clear();
            _worldReady = false;
            _localId = NetProtocol.NoId;
            _match = null;
            _local = null;
        }

        private void OnDestroy()
        {
            Shutdown(null);
            if (Current == this) Current = null;
        }

        private void OnApplicationQuit() => Shutdown(null);

        private void Update()
        {
            if (!SteamService.IsReady) return;
            _transport?.Poll();
            if (Role == NetRole.Host && _match != null) HostTick();
            else if (Role == NetRole.Client && _worldReady) ClientTick();
            else if (Role == NetRole.Client && _transport != null && _localId == NetProtocol.NoId && Time.unscaledTime - _joinStartedAt > JoinTimeout)
                LeaveToMenu("Couldn't reach your friend's game. Check that you're both online in Steam and try again.");
        }

        // ================================================================= host

        private void HostTick()
        {
            _snapshotTimer -= Time.unscaledDeltaTime;
            if (_snapshotTimer > 0f || _players.Count == 0) return;
            _snapshotTimer = 1f / NetProtocol.SnapshotRate;
            Broadcast(BuildSnapshot(), false);
        }

        private SnapshotMsg BuildSnapshot()
        {
            var snap = new SnapshotMsg
            {
                Time = Time.timeAsDouble,
                HasTimeLimit = _match.HasTimeLimit,
                TimeRemaining = _match.TimeRemaining,
                IsOver = _match.IsOver,
                IsDraw = _match.Score.IsDraw,
                WinnerId = _match.Score.Winner != null ? _match.Score.Winner.Id : NetProtocol.NoId,
                RestartIn = _match.RestartCountdown,
                ScoreLimit = _match.Score.ScoreLimit,
            };
            if (_match.SniperPad != null)
            {
                snap.PadHasWeapon = _match.SniperPad.HasWeaponOnPad;
                snap.PadTimeLeft = _match.SniperPad.TimeUntilNextSpawn;
            }
            foreach (var c in _match.Combatants)
            {
                if (c == null) continue;
                var t = c.transform;
                snap.Combatants.Add(new CombatantSnap
                {
                    Id = c.Id,
                    Position = ToV3(t.position),
                    Yaw = t.eulerAngles.y,
                    Pitch = c.Eyes != null ? Mathf.DeltaAngle(0f, c.Eyes.localEulerAngles.x) : 0f,
                    Alive = c.IsAlive,
                    Health = c.Health.Current,
                    Weapon = c.Weapons.Loadout.Active != null ? WeaponIds.ToByte(c.Weapons.Loadout.Active.Stats.id) : WeaponIds.None,
                    RespawnIn = _match.RespawnCountdown(c),
                });
            }
            foreach (var e in _match.Score.Entries)
                snap.Scores.Add(new ScoreSnap { Id = e.Id, Kills = e.Kills, Deaths = e.Deaths, Suicides = e.Suicides });
            foreach (var p in WeaponPickup.All)
            {
                if (p == null || p.Weapon == null) continue;
                snap.Pickups.Add(new PickupSnap
                {
                    NetId = p.NetId, Weapon = WeaponIds.ToByte(p.Weapon.Stats.id), Position = ToV3(p.transform.position),
                    Magazine = (byte)Mathf.Clamp(p.Weapon.Magazine, 0, 255), Reserve = (byte)Mathf.Clamp(p.Weapon.Reserve, 0, 255),
                });
            }
            return snap;
        }

        private void OnHostReceived(ulong peer, byte[] data, int length)
        {
            NetMessage msg;
            try { msg = NetMessage.Read(data, 0, length); }
            catch (Exception e)
            {
                Debug.LogWarning($"[Net] Bad message from {peer}: {e.Message}");
                return;
            }

            if (msg is HelloMsg hello)
            {
                OnHello(peer, hello);
                return;
            }
            if (!_players.TryGetValue(peer, out var player) || player.Combatant == null) return;

            switch (msg)
            {
                case PlayerStateMsg state:
                    OnPlayerState(player, state);
                    break;
                case ShotReportMsg shot:
                    OnShotReport(player, shot);
                    break;
                case PickupRequestMsg pickup:
                    OnPickupRequest(player, pickup);
                    break;
                case GoodbyeMsg _:
                    RemovePlayer(peer, "left the game");
                    break;
            }
        }

        private void OnHello(ulong peer, HelloMsg hello)
        {
            if (_players.ContainsKey(peer) || _match == null) return;
            if (hello.Version != NetProtocol.Version)
            {
                Send(peer, new GoodbyeMsg { Reason = "Different game version. Make sure you both have the same build." }, true);
                _transport.Kick(peer, "version mismatch");
                return;
            }
            if (_players.Count + 1 >= SteamLobby.MaxPlayers)
            {
                Send(peer, new GoodbyeMsg { Reason = "The game is full." }, true);
                _transport.Kick(peer, "full");
                return;
            }

            // Keep the match at 8 or fewer: a bot makes room for each player.
            if (_match.Combatants.Count >= SteamLobby.MaxPlayers)
            {
                var bot = LastBot();
                if (bot != null)
                {
                    _match.RemoveCombatant(bot);
                    Destroy(bot.gameObject);
                }
            }

            string name = string.IsNullOrWhiteSpace(hello.Name) ? "Grunt" : hello.Name.Trim();
            if (name.Length > 24) name = name.Substring(0, 24);
            var color = PlayerColors[(_players.Count + 1) % PlayerColors.Length];
            var combatant = CombatantFactory.CreateBody(name, color, _match.transform, networkDriven: true);
            combatant.NetOwner = peer;
            var player = new RemotePlayer { Peer = peer, Name = name, Combatant = combatant };
            _players[peer] = player;

            Send(peer, new WelcomeMsg { YourId = combatant.Id, Setup = _setup }, true);
            WatchCombatant(combatant);
            _match.AddLateCombatant(combatant); // spawns them -> RespawnEvent
            BroadcastRoster();
            Notice?.Invoke(name + " joined");
        }

        private void RemovePlayer(ulong peer, string reason)
        {
            if (!_players.TryGetValue(peer, out var player)) return;
            _players.Remove(peer);
            if (player.Combatant != null)
            {
                _match?.RemoveCombatant(player.Combatant);
                Destroy(player.Combatant.gameObject);
            }
            BroadcastRoster();
            Notice?.Invoke(player.Name + " left");
        }

        private void OnPlayerState(RemotePlayer player, PlayerStateMsg state)
        {
            var c = player.Combatant;
            if (state.Life != LifeOf(c) || !c.IsAlive) return; // stale: from before their last respawn
            var proxy = c.GetComponent<NetProxy>();
            proxy.AddPose(Time.timeAsDouble, ToVector(state.Position), state.Yaw, Mathf.Clamp(state.Pitch, -89f, 89f));
            c.Weapons.ApplyMirrorLoadout(state.Slots, state.ActiveSlot);
        }

        private void OnShotReport(RemotePlayer player, ShotReportMsg shot)
        {
            var shooter = player.Combatant;
            string weaponId = WeaponIds.ToId(shot.Weapon);
            var stats = WeaponIds.Stats(shot.Weapon);
            if (!shooter.IsAlive || stats == null || shooter.Weapons.Loadout.Find(weaponId) == null) return;
            if (Time.time - player.LastShotAt < stats.fireInterval * 0.6f) return; // faster than the gun can fire
            player.LastShotAt = Time.time;

            shooter.Weapons.PlayRemoteShot(ToVector(shot.From), ToVector(shot.To), weaponId); // effects + relay to others

            if (shot.TargetId == NetProtocol.NoId) return;
            var target = Combatant.Find(shot.TargetId);
            if (target == null || target == shooter || !target.IsAlive) return;
            if (Vector3.Distance(ToVector(shot.From), target.transform.position) > stats.range + 3f) return;
            target.Health.TakeDamage(stats.DamageFor(shot.Zone), new DamageSource(shooter, weaponId, shot.Zone));
        }

        private void OnPickupRequest(RemotePlayer player, PickupRequestMsg request)
        {
            var c = player.Combatant;
            var pickup = WeaponPickup.Find(request.PickupId);
            if (pickup == null || pickup.Weapon == null || !c.IsAlive) return;
            if (Vector3.Distance(pickup.transform.position, c.transform.position) > c.Weapons.pickupRadius + 2f) return;

            var weapon = pickup.Weapon;
            int before = weapon.Magazine + weapon.Reserve;
            var result = c.Weapons.PickupFrom(pickup);
            if (result.Outcome == PickupOutcome.None) return;
            Send(player.Peer, new PickupGrantedMsg
            {
                PickupId = request.PickupId,
                Outcome = result.Outcome,
                Weapon = WeaponIds.ToByte(weapon.Stats.id),
                Magazine = (byte)weapon.Magazine,
                Reserve = (byte)weapon.Reserve,
                AmmoTaken = result.Outcome == PickupOutcome.AmmoTaken ? before - (weapon.Magazine + weapon.Reserve) : 0,
            }, true);
        }

        private void WatchCombatant(Combatant c)
        {
            c.Health.Damaged += (h, result, source) =>
            {
                if (Role != NetRole.Host) return;
                Broadcast(new DamageEventMsg
                {
                    VictimId = c.Id,
                    AttackerId = source.Instigator != null ? source.Instigator.Id : NetProtocol.NoId,
                    Amount = result.Applied,
                    Zone = source.Zone,
                    Killed = result.Killed,
                    Weapon = WeaponIds.ToByte(source.WeaponId),
                    Point = ToV3(c.ChestCenter != null ? c.ChestCenter.position : c.transform.position),
                }, true);
            };
            c.Respawned += _ =>
            {
                if (Role != NetRole.Host) return;
                int life = LifeOf(c) + 1;
                _lives[c] = life;
                var driver = c.GetComponent<NetProxy>();
                if (driver != null) driver.Snap(c.transform.position, c.transform.eulerAngles.y);
                Broadcast(new RespawnEventMsg { Id = c.Id, Life = life, Position = ToV3(c.transform.position), Yaw = c.transform.eulerAngles.y }, true);
            };
        }

        private int LifeOf(Combatant c) => _lives.TryGetValue(c, out int life) ? life : 0;

        private void OnHostKill(KillEvent kill)
        {
            Broadcast(new KillEventMsg
            {
                KillerId = kill.Killer != null ? kill.Killer.Id : NetProtocol.NoId,
                VictimId = kill.Victim.Id,
                Weapon = WeaponIds.ToByte(kill.WeaponId),
                Headshot = kill.Headshot,
            }, true);
        }

        private void OnHostShotFired(Combatant shooter, Vector3 from, Vector3 to, string weaponId)
        {
            if (Role != NetRole.Host || shooter == null) return;
            Broadcast(new ShotEventMsg { ShooterId = shooter.Id, Weapon = WeaponIds.ToByte(weaponId), From = ToV3(from), To = ToV3(to) }, true);
        }

        private void BroadcastRoster()
        {
            var roster = new RosterMsg();
            foreach (var c in _match.Combatants)
            {
                if (c == null) continue;
                roster.Entries.Add(new RosterEntry
                {
                    Id = c.Id, Name = c.displayName, R = c.color.r, G = c.color.g, B = c.color.b,
                    IsBot = c.GetComponent<BotController>() != null, Owner = c.NetOwner,
                });
            }
            Broadcast(roster, true);
        }

        private Combatant LastBot()
        {
            for (int i = _match.Combatants.Count - 1; i >= 0; i--)
                if (_match.Combatants[i] != null && _match.Combatants[i].GetComponent<BotController>() != null) return _match.Combatants[i];
            return null;
        }

        // ================================================================= client

        private void ClientTick()
        {
            if (_local == null) return;
            _stateTimer -= Time.unscaledDeltaTime;
            if (_stateTimer > 0f) return;
            _stateTimer = 1f / NetProtocol.PlayerStateRate;

            var loadout = _local.Weapons.Loadout;
            var state = new PlayerStateMsg
            {
                Life = _localLife,
                Position = ToV3(_local.transform.position),
                Yaw = _local.transform.eulerAngles.y,
                Pitch = _local.Eyes != null ? Mathf.DeltaAngle(0f, _local.Eyes.localEulerAngles.x) : 0f,
                ActiveSlot = (byte)loadout.ActiveIndex,
            };
            foreach (var w in loadout.Slots)
                state.Slots.Add(new SlotState { Weapon = WeaponIds.ToByte(w.Stats.id), Magazine = (byte)w.Magazine, Reserve = (byte)w.Reserve });
            Send(_hostPeer, state, false);
        }

        private void OnClientReceived(ulong peer, byte[] data, int length)
        {
            if (peer != _hostPeer) return;
            if (!_worldReady)
            {
                // Before the world exists we can only act on Welcome/Roster/Goodbye; keep the rest.
                var type = (MsgType)data[0];
                if (type != MsgType.Welcome && type != MsgType.Roster && type != MsgType.Goodbye)
                {
                    if (type != MsgType.Snapshot && _pendingUntilWorld.Count < 256)
                    {
                        var copy = new byte[length];
                        Array.Copy(data, copy, length);
                        _pendingUntilWorld.Add(copy);
                    }
                    return;
                }
            }
            HandleClientMessage(data, length);
        }

        private void HandleClientMessage(byte[] data, int length)
        {
            NetMessage msg;
            try { msg = NetMessage.Read(data, 0, length); }
            catch (Exception e)
            {
                Debug.LogWarning("[Net] Bad message from host: " + e.Message);
                return;
            }

            switch (msg)
            {
                case WelcomeMsg welcome:
                    if (welcome.Version != NetProtocol.Version)
                    {
                        LeaveToMenu("Different game version from the host.");
                        return;
                    }
                    _localId = welcome.YourId;
                    Status = "Loading the match...";
                    _flow.StartClientMatch(welcome.Setup);
                    break;
                case RosterMsg roster:
                    _roster.Clear();
                    _roster.AddRange(roster.Entries);
                    if (_worldReady) ApplyRoster();
                    break;
                case SnapshotMsg snap:
                    ApplySnapshot(snap);
                    break;
                case ShotEventMsg shot:
                    if (shot.ShooterId != _localId && _proxies.TryGetValue(shot.ShooterId, out var shooter) && shooter != null)
                        shooter.Weapons.PlayRemoteShot(ToVector(shot.From), ToVector(shot.To), WeaponIds.ToId(shot.Weapon));
                    break;
                case DamageEventMsg dmg:
                    ApplyDamage(dmg);
                    break;
                case KillEventMsg kill:
                    var victim = Combatant.Find(kill.VictimId);
                    if (victim != null)
                        _match.MirrorKill(new KillEvent(Combatant.Find(kill.KillerId), victim, WeaponIds.ToId(kill.Weapon), kill.Headshot, Time.time));
                    break;
                case RespawnEventMsg respawn:
                    ApplyRespawn(respawn);
                    break;
                case PickupGrantedMsg granted:
                    _local.Weapons.ApplyGrantedPickup(granted.Outcome, WeaponIds.ToId(granted.Weapon), granted.Magazine, granted.Reserve, granted.AmmoTaken);
                    break;
                case MatchResetMsg _:
                    _match.MirrorMatchBegan();
                    break;
                case GoodbyeMsg bye:
                    LeaveToMenu(string.IsNullOrEmpty(bye.Reason) ? "The host ended the game." : bye.Reason);
                    break;
            }
        }

        private void ApplyRoster()
        {
            var seen = new HashSet<int>();
            foreach (var e in _roster)
            {
                seen.Add(e.Id);
                if (e.Id == _localId)
                {
                    _local.displayName = SteamService.LocalName;
                    continue;
                }
                if (_proxies.ContainsKey(e.Id)) continue;
                var proxy = CombatantFactory.CreateBody(e.Name, new Color(e.R, e.G, e.B), _match.transform, networkDriven: true);
                proxy.AssignId(e.Id);
                proxy.NetOwner = e.Owner;
                proxy.GetComponent<NetProxy>().Clock = () => _clock.HostTime(Time.timeAsDouble);
                proxy.SetPresent(false); // until the first snapshot places them
                _proxies[e.Id] = proxy;
                _match.AddCombatant(proxy);
                if (!e.IsBot && e.Owner != SteamService.LocalId) Notice?.Invoke(e.Name + " is here");
            }
            foreach (var id in new List<int>(_proxies.Keys))
            {
                if (seen.Contains(id)) continue;
                var gone = _proxies[id];
                _proxies.Remove(id);
                if (gone == null) continue;
                _match.RemoveCombatant(gone);
                Destroy(gone.gameObject);
            }
        }

        private void ApplySnapshot(SnapshotMsg snap)
        {
            _clock.OnHostTime(snap.Time, Time.timeAsDouble);
            _match.ApplySnapshot(snap);
            if (_match.SniperPad != null) _match.SniperPad.SetMirror(snap.PadHasWeapon, snap.PadTimeLeft);

            foreach (var c in snap.Combatants)
            {
                if (c.Id == _localId)
                {
                    // Host is the authority on our health; correct drift (regen timing etc.).
                    if (c.Alive && _local.IsAlive && Mathf.Abs(_local.Health.Current - c.Health) > 2f) _local.Health.SetCurrent(c.Health);
                    continue;
                }
                if (!_proxies.TryGetValue(c.Id, out var proxy) || proxy == null) continue;
                var driver = proxy.GetComponent<NetProxy>();
                if (c.Alive && !proxy.IsAlive)
                {
                    // Missed the respawn event (or joined mid-match): bring them back where they are.
                    proxy.Health.ResetHealth();
                    proxy.SetPresent(true);
                    driver.Snap(ToVector(c.Position), c.Yaw);
                }
                else if (c.Alive && proxy.IsAlive && !IsVisible(proxy))
                {
                    proxy.SetPresent(true);
                    driver.Snap(ToVector(c.Position), c.Yaw);
                }
                if (c.Alive) driver.AddPose(snap.Time, ToVector(c.Position), c.Yaw, c.Pitch);
                proxy.Health.SetCurrent(c.Alive ? c.Health : 0f);
                proxy.Weapons.SetDisplayedWeapon(WeaponIds.ToId(c.Weapon));
            }
            SyncPickups(snap);
        }

        private static bool IsVisible(Combatant c)
        {
            var hitbox = c.HeadCenter != null ? c.HeadCenter.GetComponent<Collider>() : null;
            return hitbox == null || hitbox.enabled;
        }

        private void SyncPickups(SnapshotMsg snap)
        {
            var alive = new HashSet<int>();
            foreach (var p in snap.Pickups)
            {
                alive.Add(p.NetId);
                var existing = WeaponPickup.Find(p.NetId);
                var stats = WeaponIds.Stats(p.Weapon);
                if (stats == null) continue;
                if (existing == null) WeaponPickup.CreateMirror(p.NetId, new WeaponState(stats, p.Magazine, p.Reserve), ToVector(p.Position));
                else existing.Weapon.SetAmmo(p.Magazine, p.Reserve);
            }
            foreach (var p in new List<WeaponPickup>(WeaponPickup.All))
                if (p != null && p.IsMirror && !alive.Contains(p.NetId)) p.Consume();
        }

        private void ApplyDamage(DamageEventMsg dmg)
        {
            var victim = dmg.VictimId == _localId ? _local : Combatant.Find(dmg.VictimId);
            if (victim == null || !victim.IsAlive) return;
            var source = new DamageSource(Combatant.Find(dmg.AttackerId), WeaponIds.ToId(dmg.Weapon), dmg.Zone);
            victim.Health.TakeDamage(dmg.Amount, source);
            if (dmg.Killed && victim.IsAlive) victim.Health.TakeDamage(victim.Health.Current + 1f, source); // stay in sync with the host
        }

        private void ApplyRespawn(RespawnEventMsg respawn)
        {
            var rotation = Quaternion.Euler(0f, respawn.Yaw, 0f);
            if (respawn.Id == _localId)
            {
                _localLife = respawn.Life;
                _local.Respawn(ToVector(respawn.Position), rotation);
                return;
            }
            if (!_proxies.TryGetValue(respawn.Id, out var proxy) || proxy == null) return;
            proxy.Health.ResetHealth();
            proxy.SetPresent(true);
            proxy.GetComponent<NetProxy>().Snap(ToVector(respawn.Position), respawn.Yaw);
        }

        private void OnLocalShot(ShotReport report)
        {
            var target = report.Target != null ? report.Target.GetComponent<Combatant>() : null;
            Send(_hostPeer, new ShotReportMsg
            {
                Weapon = WeaponIds.ToByte(report.WeaponId),
                From = ToV3(report.From),
                To = ToV3(report.To),
                TargetId = target != null ? target.Id : NetProtocol.NoId,
                Zone = report.Zone,
            }, true);
        }

        private void LeaveToMenu(string message)
        {
            if (Role == NetRole.Offline) return;
            Shutdown(null);
            _flow.ReturnToMenuWithMessage(message);
        }

        // ================================================================= helpers

        private void Send(ulong peer, NetMessage msg, bool reliable)
        {
            if (_transport == null) return;
            _writer.Reset();
            msg.Write(_writer);
            _transport.Send(peer, _writer.Buffer, _writer.Length, reliable);
        }

        private void Broadcast(NetMessage msg, bool reliable)
        {
            if (_transport == null) return;
            _writer.Reset();
            msg.Write(_writer);
            foreach (var peer in _players.Keys) _transport.Send(peer, _writer.Buffer, _writer.Length, reliable);
        }

        private static V3 ToV3(Vector3 v) => new V3(v.x, v.y, v.z);
        private static Vector3 ToVector(V3 v) => new Vector3(v.X, v.Y, v.Z);
    }
}
