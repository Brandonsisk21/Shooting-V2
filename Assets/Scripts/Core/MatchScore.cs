using System;
using System.Collections.Generic;

namespace ArenaShooter.Core
{
    /// <summary>
    /// Free-for-all scoring (GDD 4.1): +1 per kill, -1 per suicide (Halo rule). The first combatant
    /// to reach <see cref="ScoreLimit"/> wins; if time runs out first, the leader wins, or it's a
    /// draw when the top score is shared.
    /// </summary>
    public sealed class MatchScore
    {
        public sealed class Entry
        {
            public int Id { get; }
            public string Name { get; }
            public int Kills { get; internal set; }
            public int Deaths { get; internal set; }
            public int Suicides { get; internal set; }
            public int Score => Kills - Suicides;

            internal Entry(int id, string name)
            {
                Id = id;
                Name = name;
            }
        }

        public int ScoreLimit { get; }
        public Entry Winner { get; private set; }
        /// <summary>Time ran out with the top score shared.</summary>
        public bool IsDraw { get; private set; }
        public bool IsOver => Winner != null || IsDraw;
        public IReadOnlyCollection<Entry> Entries => _entries.Values;

        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();

        public MatchScore(int scoreLimit)
        {
            if (scoreLimit <= 0) throw new ArgumentOutOfRangeException(nameof(scoreLimit));
            ScoreLimit = scoreLimit;
        }

        public Entry Register(int id, string name)
        {
            if (!_entries.TryGetValue(id, out var entry))
            {
                entry = new Entry(id, name);
                _entries[id] = entry;
            }
            return entry;
        }

        public Entry Get(int id) => _entries.TryGetValue(id, out var entry) ? entry : null;

        /// <summary>Records a death. A null or self <paramref name="killerId"/> counts as a suicide.</summary>
        public void RecordDeath(int victimId, int? killerId)
        {
            if (IsOver) return;
            var victim = Get(victimId);
            if (victim == null) return;

            victim.Deaths++;
            var killer = killerId.HasValue && killerId.Value != victimId ? Get(killerId.Value) : null;
            if (killer == null)
            {
                victim.Suicides++;
                return;
            }

            killer.Kills++;
            if (killer.Score >= ScoreLimit) Winner = killer;
        }

        /// <summary>Ends the match on the clock: the sole leader wins, a shared lead is a draw.</summary>
        public void EndByTime()
        {
            if (IsOver || _entries.Count == 0) return;
            var ranked = Ranked();
            if (ranked.Count > 1 && ranked[0].Score == ranked[1].Score) IsDraw = true;
            else Winner = ranked[0];
        }

        /// <summary>Highest score first; ties broken by more kills, then fewer deaths.</summary>
        public List<Entry> Ranked()
        {
            var list = new List<Entry>(_entries.Values);
            list.Sort((a, b) =>
            {
                int c = b.Score.CompareTo(a.Score);
                if (c != 0) return c;
                c = b.Kills.CompareTo(a.Kills);
                return c != 0 ? c : a.Deaths.CompareTo(b.Deaths);
            });
            return list;
        }

        public void Reset()
        {
            Winner = null;
            IsDraw = false;
            foreach (var e in _entries.Values)
            {
                e.Kills = 0;
                e.Deaths = 0;
                e.Suicides = 0;
            }
        }
    }
}
