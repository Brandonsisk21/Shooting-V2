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
        public float respawnDelay = 3f;
        [Tooltip("Seconds the results show before a new match starts.")]
        public float restartDelay = 10f;

        public static MatchManager Current { get; private set; }

        public MatchScore Score { get; private set; }
        public IReadOnlyList<KillEvent> KillFeed => _feed;
        public bool IsOver => Score.IsOver;
        public float RestartCountdown => IsOver ? Mathf.Max(0f, _restartAt - Time.time) : 0f;
        public SniperSpawnPad SniperPad { get; set; }

        public event Action<KillEvent> Killed;

        private readonly List<SpawnPoint> _spawns = new List<SpawnPoint>();
        private readonly List<Combatant> _combatants = new List<Combatant>();
        private readonly Dictionary<Combatant, float> _respawnAt = new Dictionary<Combatant, float>();
        private readonly List<KillEvent> _feed = new List<KillEvent>();
        private readonly System.Random _random = new System.Random();
        private float _restartAt;

        private void Awake()
        {
            Current = this;
        }

        /// <summary>Call once, right after adding the component, before adding combatants.</summary>
        public void Initialize(int limit)
        {
            scoreLimit = limit;
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
            combatant.Health.Died += (_, source) => OnDied(combatant, source);
        }

        /// <summary>Places everyone at spawns. Call after all combatants are added.</summary>
        public void BeginMatch()
        {
            Score.Reset();
            _feed.Clear();
            _respawnAt.Clear();
            foreach (var pickup in FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None))
                Destroy(pickup.gameObject);
            if (SniperPad != null) SniperPad.ResetTimer();
            foreach (var c in _combatants) Respawn(c);
        }

        /// <summary>Seconds until this combatant respawns (0 if alive).</summary>
        public float RespawnCountdown(Combatant combatant) =>
            _respawnAt.TryGetValue(combatant, out float at) ? Mathf.Max(0f, at - Time.time) : 0f;

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

            if (!wasOver && Score.IsOver) _restartAt = Time.time + restartDelay;
        }

        private void Update()
        {
            if (IsOver)
            {
                if (Time.time >= _restartAt) BeginMatch();
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
