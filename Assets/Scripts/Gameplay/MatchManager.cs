using System;
using System.Collections.Generic;
using ArenaShooter.Core;
using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>A line in the kill feed.</summary>
    public readonly struct KillEvent
    {
        public readonly Combatant Killer; // null for suicide
        public readonly Combatant Victim;
        public readonly string WeaponId;
        public readonly bool Headshot;
        public readonly float Time;

        public KillEvent(Combatant killer, Combatant victim, string weaponId, bool headshot, float time)
        {
            Killer = killer;
            Victim = victim;
            WeaponId = weaponId;
            Headshot = headshot;
            Time = time;
        }
    }

    /// <summary>
    /// Runs a Free-for-All match (GDD 4): registers combatants, credits kills, drops weapons on
    /// death, respawns everyone at the safest spawn, and restarts after someone hits the score limit.
    /// </summary>
    public class MatchManager : MonoBehaviour
    {
        public int scoreLimit = 25;
        [Tooltip("Match length in seconds; 0 = no time limit.")]
        public float timeLimit = 600f;
        public float respawnDelay = 3f;
        [Tooltip("Seconds the results show before a new match starts.")]
        public float restartDelay = 10f;

        public static MatchManager Current { get; private set; }

        public MatchScore Score { get; private set; }
        public IReadOnlyList<KillEvent> KillFeed => _feed;
        public bool IsOver => Score.IsOver;
        public float RestartCountdown => IsMirror ? _mirrorRestartIn : IsOver ? Mathf.Max(0f, _restartAt - Time.time) : 0f;
        public bool HasTimeLimit => IsMirror ? _mirrorHasTimeLimit : timeLimit > 0f;
        /// <summary>Seconds left on the match clock (frozen once the match is over).</summary>
        public float TimeRemaining => IsMirror ? _mirrorTimeRemaining
            : HasTimeLimit ? Mathf.Max(0f, timeLimit - ((IsOver ? _endedAt : Time.time) - _startedAt)) : 0f;
        public SniperSpawnPad SniperPad { get; set; }
        public IReadOnlyList<Combatant> Combatants => _combatants;

        /// <summary>
        /// Online client: this manager only mirrors the host's match (scores, clock, feed,
        /// respawn timers) for the HUD; it never kills, spawns or scores anything itself.
        /// </summary>
        public bool IsMirror { get; private set; }

        public event Action<KillEvent> Killed;
        /// <summary>A new match started (scores reset, everyone respawned).</summary>
        public event Action MatchBegan;

        private readonly List<SpawnPoint> _spawns = new List<SpawnPoint>();
        private readonly List<Combatant> _combatants = new List<Combatant>();
        private readonly Dictionary<Combatant, float> _respawnAt = new Dictionary<Combatant, float>();
        private readonly List<KillEvent> _feed = new List<KillEvent>();
        private readonly System.Random _random = new System.Random();
        private float _restartAt;
        private float _startedAt;
        private float _endedAt;
        private bool _mirrorHasTimeLimit;
        private float _mirrorTimeRemaining, _mirrorRestartIn;
        private readonly Dictionary<int, float> _mirrorRespawn = new Dictionary<int, float>();

        private void Awake()
        {
            Current = this;
        }

        /// <summary>Call once, right after adding the component, before adding combatants.</summary>
        public void Initialize(int limit, float timeLimitSeconds)
        {
            scoreLimit = limit;
            timeLimit = timeLimitSeconds;
            Score = new MatchScore(scoreLimit);
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        public void SetSpawns(IEnumerable<SpawnPoint> spawns)
        {
            _spawns.Clear();
            _spawns.AddRange(spawns);
        }

        public void AddCombatant(Combatant combatant)
        {
            _combatants.Add(combatant);
            Score.Register(combatant.Id, combatant.displayName);
            if (!IsMirror) combatant.Health.Died += (_, source) => OnDied(combatant, source);
        }

        /// <summary>Host: someone joined mid-match. Adds them and spawns them right away.</summary>
        public void AddLateCombatant(Combatant combatant)
        {
            AddCombatant(combatant);
            Respawn(combatant);
        }

        /// <summary>Someone left (or a bot made room for a player).</summary>
        public void RemoveCombatant(Combatant combatant)
        {
            _combatants.Remove(combatant);
            _respawnAt.Remove(combatant);
            Score.Unregister(combatant.Id);
        }

        // ------------------------------------------------------------ online client mirror

        public void InitializeMirror(int limit)
        {
            IsMirror = true;
            Initialize(limit, 0f);
        }

        /// <summary>Online client: copy the host's match state from a snapshot.</summary>
        public void ApplySnapshot(ArenaShooter.Core.Net.SnapshotMsg snap)
        {
            _mirrorHasTimeLimit = snap.HasTimeLimit;
            _mirrorTimeRemaining = snap.TimeRemaining;
            _mirrorRestartIn = snap.RestartIn;
            foreach (var s in snap.Scores) Score.Mirror(s.Id, s.Kills, s.Deaths, s.Suicides);
            Score.MirrorResult(snap.IsOver, snap.IsDraw, snap.WinnerId);
            _mirrorRespawn.Clear();
            foreach (var c in snap.Combatants)
                if (!c.Alive) _mirrorRespawn[c.Id] = c.RespawnIn;
        }

        /// <summary>Online client: a kill happened on the host.</summary>
        public void MirrorKill(KillEvent kill)
        {
            _feed.Add(kill);
            if (_feed.Count > 20) _feed.RemoveAt(0);
            Killed?.Invoke(kill);
        }

        /// <summary>Online client: the host started a new match.</summary>
        public void MirrorMatchBegan()
        {
            _feed.Clear();
            Score.Reset();
            MatchBegan?.Invoke();
        }

        /// <summary>Places everyone at spawns. Call after all combatants are added.</summary>
        public void BeginMatch()
        {
            Score.Reset();
            _startedAt = Time.time;
            _feed.Clear();
            _respawnAt.Clear();
            foreach (var pickup in FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None))
                Destroy(pickup.gameObject);
            if (SniperPad != null) SniperPad.ResetTimer();
            Grenade.ClearAll();
            GrenadePickup.ResetAll();
            foreach (var c in _combatants) Respawn(c);
            MatchBegan?.Invoke();
        }

        /// <summary>Seconds until this combatant respawns (0 if alive).</summary>
        public float RespawnCountdown(Combatant combatant)
        {
            if (IsMirror) return _mirrorRespawn.TryGetValue(combatant.Id, out float left) ? left : 0f;
            return _respawnAt.TryGetValue(combatant, out float at) ? Mathf.Max(0f, at - Time.time) : 0f;
        }

        private void OnDied(Combatant victim, DamageSource source)
        {
            victim.Weapons.DropOnDeath();
            _respawnAt[victim] = Time.time + respawnDelay;

            Combatant killer = source.Instigator != null && source.Instigator != victim ? source.Instigator : null;
            bool wasOver = Score.IsOver;
            Score.RecordDeath(victim.Id, killer != null ? killer.Id : (int?)null);

            var kill = new KillEvent(killer, victim, source.WeaponId, source.Zone == HitZone.Head, Time.time);
            _feed.Add(kill);
            if (_feed.Count > 20) _feed.RemoveAt(0);
            Killed?.Invoke(kill);

            if (!wasOver && Score.IsOver) OnMatchEnded();
        }

        private void OnMatchEnded()
        {
            _endedAt = Time.time;
            _restartAt = Time.time + restartDelay;
        }

        private void Update()
        {
            if (IsMirror) return;
            if (IsOver)
            {
                if (Time.time >= _restartAt) BeginMatch();
                return;
            }

            if (HasTimeLimit && Time.time - _startedAt >= timeLimit)
            {
                Score.EndByTime();
                OnMatchEnded();
                return;
            }

            if (_respawnAt.Count == 0) return;
            // Iterate a copy: Respawn changes the dictionary.
            foreach (var entry in new List<KeyValuePair<Combatant, float>>(_respawnAt))
                if (entry.Key != null && Time.time >= entry.Value) Respawn(entry.Key);
        }

        private void Respawn(Combatant combatant)
        {
            _respawnAt.Remove(combatant);
            if (_spawns.Count == 0)
            {
                combatant.Respawn(combatant.transform.position, combatant.transform.rotation);
                return;
            }

            var candidates = new List<GroundPoint>(_spawns.Count);
            foreach (var s in _spawns) candidates.Add(new GroundPoint(s.transform.position.x, s.transform.position.z));
            var enemies = new List<GroundPoint>();
            foreach (var other in _combatants)
                if (other != combatant && other.IsAlive)
                    enemies.Add(new GroundPoint(other.transform.position.x, other.transform.position.z));

            int index = SpawnSelector.Pick(candidates, enemies, _random);
            combatant.Respawn(_spawns[index].transform.position, _spawns[index].transform.rotation);
        }
    }
}
