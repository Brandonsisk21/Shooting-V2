using System.Collections.Generic;
using System.IO;

namespace ArenaShooter.Core.Net
{
    /// <summary>
    /// Space Grunts online protocol (GDD 9.1). The host runs the real match (bots, health, pickups,
    /// score); clients move their own grunt locally, report shots and pickups, and render everyone
    /// else from snapshots. Every message starts with a <see cref="MsgType"/> byte.
    /// </summary>
    public enum MsgType : byte
    {
        // client -> host
        Hello = 1,
        PlayerState = 2,
        ShotReport = 3,
        PickupRequest = 4,
        GrenadeThrow = 5,

        // host -> client
        Welcome = 20,
        Roster = 21,
        Snapshot = 22,
        ShotEvent = 23,
        DamageEvent = 24,
        KillEvent = 25,
        RespawnEvent = 26,
        PickupGranted = 27,
        MatchReset = 28,
        Goodbye = 29,
        GrenadeSpawned = 30,
        GrenadeExploded = 31,
    }

    public static class NetProtocol
    {
        /// <summary>Bump when the wire format changes; host rejects mismatched clients.</summary>
        public const byte Version = 2; // v2: crouch, grenades
        public const float SnapshotRate = 20f;
        public const float PlayerStateRate = 30f;
        public const int NoId = -1;
    }

    /// <summary>Weapons as bytes on the wire.</summary>
    public static class WeaponIds
    {
        public const byte None = 255;
        public const byte Rifle = 0;
        public const byte Sniper = 1;
        /// <summary>Only used for damage/kill events (grenades aren't a held weapon).</summary>
        public const byte Grenade = 2;

        public static byte ToByte(string id) => id == "rifle" ? Rifle : id == "sniper" ? Sniper : id == "grenade" ? Grenade : None;

        public static string ToId(byte b) => b == Rifle ? "rifle" : b == Sniper ? "sniper" : b == Grenade ? "grenade" : null;

        public static WeaponStats Stats(byte b) => b == Sniper ? WeaponStats.Sniper() : b == Rifle ? WeaponStats.Rifle() : null;
    }

    public abstract class NetMessage
    {
        public abstract MsgType Type { get; }
        protected abstract void WriteBody(NetWriter w);

        public void Write(NetWriter w)
        {
            w.WriteByte((byte)Type);
            WriteBody(w);
        }

        public byte[] ToBytes()
        {
            var w = new NetWriter();
            Write(w);
            return w.ToArray();
        }

        /// <summary>Decodes one message. Throws <see cref="InvalidDataException"/> on bad data.</summary>
        public static NetMessage Read(byte[] data, int offset = 0, int count = -1)
        {
            var r = new NetReader(data, offset, count);
            var type = (MsgType)r.ReadByte();
            switch (type)
            {
                case MsgType.Hello: return HelloMsg.ReadBody(r);
                case MsgType.PlayerState: return PlayerStateMsg.ReadBody(r);
                case MsgType.ShotReport: return ShotReportMsg.ReadBody(r);
                case MsgType.PickupRequest: return new PickupRequestMsg { PickupId = r.ReadInt() };
                case MsgType.GrenadeThrow: return new GrenadeThrowMsg { From = r.ReadV3(), Velocity = r.ReadV3() };
                case MsgType.GrenadeSpawned: return new GrenadeSpawnedMsg { Id = r.ReadInt(), ThrowerId = r.ReadInt(), From = r.ReadV3(), Velocity = r.ReadV3() };
                case MsgType.GrenadeExploded: return new GrenadeExplodedMsg { Id = r.ReadInt(), Position = r.ReadV3() };
                case MsgType.Welcome: return WelcomeMsg.ReadBody(r);
                case MsgType.Roster: return RosterMsg.ReadBody(r);
                case MsgType.Snapshot: return SnapshotMsg.ReadBody(r);
                case MsgType.ShotEvent: return ShotEventMsg.ReadBody(r);
                case MsgType.DamageEvent: return DamageEventMsg.ReadBody(r);
                case MsgType.KillEvent: return KillEventMsg.ReadBody(r);
                case MsgType.RespawnEvent: return RespawnEventMsg.ReadBody(r);
                case MsgType.PickupGranted: return PickupGrantedMsg.ReadBody(r);
                case MsgType.MatchReset: return new MatchResetMsg();
                case MsgType.Goodbye: return new GoodbyeMsg { Reason = r.ReadString() };
                default: throw new InvalidDataException($"Unknown message type {(byte)type}.");
            }
        }
    }

    // ============================================================== client -> host

    public sealed class HelloMsg : NetMessage
    {
        public byte Version = NetProtocol.Version;
        public string Name = "";
        public override MsgType Type => MsgType.Hello;

        protected override void WriteBody(NetWriter w)
        {
            w.WriteByte(Version);
            w.WriteString(Name);
        }

        internal static HelloMsg ReadBody(NetReader r) => new HelloMsg { Version = r.ReadByte(), Name = r.ReadString() };
    }

    public struct SlotState
    {
        public byte Weapon;
        public byte Magazine;
        public byte Reserve;
    }

    /// <summary>The client's own grunt: where it is, where it looks, what it carries.</summary>
    public sealed class PlayerStateMsg : NetMessage
    {
        /// <summary>Which life this state belongs to; the host ignores states from before a respawn.</summary>
        public int Life;
        public V3 Position;
        public float Yaw, Pitch;
        public byte ActiveSlot;
        public bool Crouched;
        public List<SlotState> Slots = new List<SlotState>();
        public override MsgType Type => MsgType.PlayerState;

        protected override void WriteBody(NetWriter w)
        {
            w.WriteInt(Life);
            w.WriteV3(Position);
            w.WriteFloat(Yaw);
            w.WriteFloat(Pitch);
            w.WriteByte(ActiveSlot);
            w.WriteBool(Crouched);
            w.WriteUInt((uint)Slots.Count);
            foreach (var s in Slots)
            {
                w.WriteByte(s.Weapon);
                w.WriteByte(s.Magazine);
                w.WriteByte(s.Reserve);
            }
        }

        internal static PlayerStateMsg ReadBody(NetReader r)
        {
            var m = new PlayerStateMsg { Life = r.ReadInt(), Position = r.ReadV3(), Yaw = r.ReadFloat(), Pitch = r.ReadFloat(), ActiveSlot = r.ReadByte(), Crouched = r.ReadBool() };
            int n = r.ReadCount();
            for (int i = 0; i < n; i++) m.Slots.Add(new SlotState { Weapon = r.ReadByte(), Magazine = r.ReadByte(), Reserve = r.ReadByte() });
            return m;
        }
    }

    /// <summary>A client's shot and what it hit on the client's screen (the host validates it).</summary>
    public sealed class ShotReportMsg : NetMessage
    {
        public byte Weapon;
        public V3 From, To;
        public int TargetId = NetProtocol.NoId;
        public HitZone Zone;
        public override MsgType Type => MsgType.ShotReport;

        protected override void WriteBody(NetWriter w)
        {
            w.WriteByte(Weapon);
            w.WriteV3(From);
            w.WriteV3(To);
            w.WriteInt(TargetId);
            w.WriteByte((byte)Zone);
        }

        internal static ShotReportMsg ReadBody(NetReader r) =>
            new ShotReportMsg { Weapon = r.ReadByte(), From = r.ReadV3(), To = r.ReadV3(), TargetId = r.ReadInt(), Zone = (HitZone)(r.ReadByte() & 1) };
    }

    public sealed class PickupRequestMsg : NetMessage
    {
        public int PickupId;
        public override MsgType Type => MsgType.PickupRequest;
        protected override void WriteBody(NetWriter w) => w.WriteInt(PickupId);
    }

    // ============================================================== host -> client

    public sealed class WelcomeMsg : NetMessage
    {
        public byte Version = NetProtocol.Version;
        public int YourId;
        public MatchSetup Setup = new MatchSetup();
        public override MsgType Type => MsgType.Welcome;

        protected override void WriteBody(NetWriter w)
        {
            w.WriteByte(Version);
            w.WriteInt(YourId);
            w.WriteByte((byte)Setup.map);
            w.WriteByte((byte)Setup.botCount);
            w.WriteByte((byte)Setup.difficulty);
            w.WriteInt(Setup.scoreLimit);
            w.WriteInt(Setup.timeLimitMinutes);
        }

        internal static WelcomeMsg ReadBody(NetReader r)
        {
            var m = new WelcomeMsg { Version = r.ReadByte(), YourId = r.ReadInt() };
            m.Setup = new MatchSetup
            {
                map = (MapChoice)r.ReadByte(),
                botCount = r.ReadByte(),
                difficulty = (Difficulty)r.ReadByte(),
                scoreLimit = r.ReadInt(),
                timeLimitMinutes = r.ReadInt(),
            };
            return m;
        }
    }

    public struct RosterEntry
    {
        public int Id;
        public string Name;
        public float R, G, B;
        public bool IsBot;
        /// <summary>Steam ID of the human playing this grunt (0 for bots).</summary>
        public ulong Owner;
    }

    /// <summary>Everyone in the match. Sent on join and whenever someone joins or leaves.</summary>
    public sealed class RosterMsg : NetMessage
    {
        public List<RosterEntry> Entries = new List<RosterEntry>();
        public override MsgType Type => MsgType.Roster;

        protected override void WriteBody(NetWriter w)
        {
            w.WriteUInt((uint)Entries.Count);
            foreach (var e in Entries)
            {
                w.WriteInt(e.Id);
                w.WriteString(e.Name);
                w.WriteFloat(e.R);
                w.WriteFloat(e.G);
                w.WriteFloat(e.B);
                w.WriteBool(e.IsBot);
                w.WriteULong(e.Owner);
            }
        }

        internal static RosterMsg ReadBody(NetReader r)
        {
            var m = new RosterMsg();
            int n = r.ReadCount();
            for (int i = 0; i < n; i++)
                m.Entries.Add(new RosterEntry
                {
                    Id = r.ReadInt(), Name = r.ReadString(), R = r.ReadFloat(), G = r.ReadFloat(), B = r.ReadFloat(),
                    IsBot = r.ReadBool(), Owner = r.ReadULong(),
                });
            return m;
        }
    }

    public struct CombatantSnap
    {
        public int Id;
        public V3 Position;
        public float Yaw, Pitch;
        public bool Alive;
        public float Health;
        public byte Weapon;
        public float RespawnIn;
        public bool Crouched;
        public byte Grenades;
    }

    public struct ScoreSnap
    {
        public int Id;
        public int Kills, Deaths, Suicides;
    }

    public struct PickupSnap
    {
        public int NetId;
        public byte Weapon;
        public V3 Position;
        public byte Magazine, Reserve;
    }

    /// <summary>The world as the host sees it, ~20 times a second (sent unreliably).</summary>
    public sealed class SnapshotMsg : NetMessage
    {
        public double Time;
        public bool HasTimeLimit;
        public float TimeRemaining;
        public bool IsOver;
        public bool IsDraw;
        public int WinnerId = NetProtocol.NoId;
        public float RestartIn;
        public int ScoreLimit;
        public bool PadHasWeapon;
        public float PadTimeLeft;
        public List<CombatantSnap> Combatants = new List<CombatantSnap>();
        public List<ScoreSnap> Scores = new List<ScoreSnap>();
        public List<PickupSnap> Pickups = new List<PickupSnap>();
        /// <summary>Whether each map grenade pickup is there, in map build order.</summary>
        public List<bool> GrenadePickups = new List<bool>();
        public override MsgType Type => MsgType.Snapshot;

        protected override void WriteBody(NetWriter w)
        {
            w.WriteDouble(Time);
            w.WriteBool(HasTimeLimit);
            w.WriteFloat(TimeRemaining);
            w.WriteBool(IsOver);
            w.WriteBool(IsDraw);
            w.WriteInt(WinnerId);
            w.WriteFloat(RestartIn);
            w.WriteInt(ScoreLimit);
            w.WriteBool(PadHasWeapon);
            w.WriteFloat(PadTimeLeft);
            w.WriteUInt((uint)Combatants.Count);
            foreach (var c in Combatants)
            {
                w.WriteInt(c.Id);
                w.WriteV3(c.Position);
                w.WriteFloat(c.Yaw);
                w.WriteFloat(c.Pitch);
                w.WriteBool(c.Alive);
                w.WriteFloat(c.Health);
                w.WriteByte(c.Weapon);
                w.WriteFloat(c.RespawnIn);
                w.WriteBool(c.Crouched);
                w.WriteByte(c.Grenades);
            }
            w.WriteUInt((uint)Scores.Count);
            foreach (var s in Scores)
            {
                w.WriteInt(s.Id);
                w.WriteInt(s.Kills);
                w.WriteInt(s.Deaths);
                w.WriteInt(s.Suicides);
            }
            w.WriteUInt((uint)Pickups.Count);
            foreach (var p in Pickups)
            {
                w.WriteInt(p.NetId);
                w.WriteByte(p.Weapon);
                w.WriteV3(p.Position);
                w.WriteByte(p.Magazine);
                w.WriteByte(p.Reserve);
            }
            w.WriteUInt((uint)GrenadePickups.Count);
            foreach (bool available in GrenadePickups) w.WriteBool(available);
        }

        internal static SnapshotMsg ReadBody(NetReader r)
        {
            var m = new SnapshotMsg
            {
                Time = r.ReadDouble(), HasTimeLimit = r.ReadBool(), TimeRemaining = r.ReadFloat(), IsOver = r.ReadBool(),
                IsDraw = r.ReadBool(), WinnerId = r.ReadInt(), RestartIn = r.ReadFloat(), ScoreLimit = r.ReadInt(),
                PadHasWeapon = r.ReadBool(), PadTimeLeft = r.ReadFloat(),
            };
            int n = r.ReadCount();
            for (int i = 0; i < n; i++)
                m.Combatants.Add(new CombatantSnap
                {
                    Id = r.ReadInt(), Position = r.ReadV3(), Yaw = r.ReadFloat(), Pitch = r.ReadFloat(), Alive = r.ReadBool(),
                    Health = r.ReadFloat(), Weapon = r.ReadByte(), RespawnIn = r.ReadFloat(),
                    Crouched = r.ReadBool(), Grenades = r.ReadByte(),
                });
            n = r.ReadCount();
            for (int i = 0; i < n; i++)
                m.Scores.Add(new ScoreSnap { Id = r.ReadInt(), Kills = r.ReadInt(), Deaths = r.ReadInt(), Suicides = r.ReadInt() });
            n = r.ReadCount();
            for (int i = 0; i < n; i++)
                m.Pickups.Add(new PickupSnap { NetId = r.ReadInt(), Weapon = r.ReadByte(), Position = r.ReadV3(), Magazine = r.ReadByte(), Reserve = r.ReadByte() });
            n = r.ReadCount();
            for (int i = 0; i < n; i++) m.GrenadePickups.Add(r.ReadBool());
            return m;
        }
    }

    /// <summary>Someone fired: draw the bolt and play the sound.</summary>
    public sealed class ShotEventMsg : NetMessage
    {
        public int ShooterId;
        public byte Weapon;
        public V3 From, To;
        public override MsgType Type => MsgType.ShotEvent;

        protected override void WriteBody(NetWriter w)
        {
            w.WriteInt(ShooterId);
            w.WriteByte(Weapon);
            w.WriteV3(From);
            w.WriteV3(To);
        }

        internal static ShotEventMsg ReadBody(NetReader r) =>
            new ShotEventMsg { ShooterId = r.ReadInt(), Weapon = r.ReadByte(), From = r.ReadV3(), To = r.ReadV3() };
    }

    public sealed class DamageEventMsg : NetMessage
    {
        public int VictimId;
        public int AttackerId = NetProtocol.NoId;
        public float Amount;
        public HitZone Zone;
        public bool Killed;
        public byte Weapon = WeaponIds.None;
        public V3 Point;
        public override MsgType Type => MsgType.DamageEvent;

        protected override void WriteBody(NetWriter w)
        {
            w.WriteInt(VictimId);
            w.WriteInt(AttackerId);
            w.WriteFloat(Amount);
            w.WriteByte((byte)Zone);
            w.WriteBool(Killed);
            w.WriteByte(Weapon);
            w.WriteV3(Point);
        }

        internal static DamageEventMsg ReadBody(NetReader r) => new DamageEventMsg
        {
            VictimId = r.ReadInt(), AttackerId = r.ReadInt(), Amount = r.ReadFloat(), Zone = (HitZone)(r.ReadByte() & 1),
            Killed = r.ReadBool(), Weapon = r.ReadByte(), Point = r.ReadV3(),
        };
    }

    public sealed class KillEventMsg : NetMessage
    {
        public int KillerId = NetProtocol.NoId;
        public int VictimId;
        public byte Weapon = WeaponIds.None;
        public bool Headshot;
        public override MsgType Type => MsgType.KillEvent;

        protected override void WriteBody(NetWriter w)
        {
            w.WriteInt(KillerId);
            w.WriteInt(VictimId);
            w.WriteByte(Weapon);
            w.WriteBool(Headshot);
        }

        internal static KillEventMsg ReadBody(NetReader r) =>
            new KillEventMsg { KillerId = r.ReadInt(), VictimId = r.ReadInt(), Weapon = r.ReadByte(), Headshot = r.ReadBool() };
    }

    public sealed class RespawnEventMsg : NetMessage
    {
        public int Id;
        public int Life;
        public V3 Position;
        public float Yaw;
        public override MsgType Type => MsgType.RespawnEvent;

        protected override void WriteBody(NetWriter w)
        {
            w.WriteInt(Id);
            w.WriteInt(Life);
            w.WriteV3(Position);
            w.WriteFloat(Yaw);
        }

        internal static RespawnEventMsg ReadBody(NetReader r) =>
            new RespawnEventMsg { Id = r.ReadInt(), Life = r.ReadInt(), Position = r.ReadV3(), Yaw = r.ReadFloat() };
    }

    public sealed class PickupGrantedMsg : NetMessage
    {
        public int PickupId;
        public PickupOutcome Outcome;
        public byte Weapon;
        public byte Magazine, Reserve;
        public int AmmoTaken;
        public override MsgType Type => MsgType.PickupGranted;

        protected override void WriteBody(NetWriter w)
        {
            w.WriteInt(PickupId);
            w.WriteByte((byte)Outcome);
            w.WriteByte(Weapon);
            w.WriteByte(Magazine);
            w.WriteByte(Reserve);
            w.WriteInt(AmmoTaken);
        }

        internal static PickupGrantedMsg ReadBody(NetReader r) => new PickupGrantedMsg
        {
            PickupId = r.ReadInt(), Outcome = (PickupOutcome)(r.ReadByte() & 3), Weapon = r.ReadByte(),
            Magazine = r.ReadByte(), Reserve = r.ReadByte(), AmmoTaken = r.ReadInt(),
        };
    }

    /// <summary>Client threw a grenade (the host checks their pouch and spawns the real one).</summary>
    public sealed class GrenadeThrowMsg : NetMessage
    {
        public V3 From, Velocity;
        public override MsgType Type => MsgType.GrenadeThrow;

        protected override void WriteBody(NetWriter w)
        {
            w.WriteV3(From);
            w.WriteV3(Velocity);
        }
    }

    /// <summary>A grenade was thrown: clients show it flying (it explodes when the host says so).</summary>
    public sealed class GrenadeSpawnedMsg : NetMessage
    {
        public int Id;
        public int ThrowerId = NetProtocol.NoId;
        public V3 From, Velocity;
        public override MsgType Type => MsgType.GrenadeSpawned;

        protected override void WriteBody(NetWriter w)
        {
            w.WriteInt(Id);
            w.WriteInt(ThrowerId);
            w.WriteV3(From);
            w.WriteV3(Velocity);
        }
    }

    public sealed class GrenadeExplodedMsg : NetMessage
    {
        public int Id;
        public V3 Position;
        public override MsgType Type => MsgType.GrenadeExploded;

        protected override void WriteBody(NetWriter w)
        {
            w.WriteInt(Id);
            w.WriteV3(Position);
        }
    }

    public sealed class MatchResetMsg : NetMessage
    {
        public override MsgType Type => MsgType.MatchReset;
        protected override void WriteBody(NetWriter w) { }
    }

    public sealed class GoodbyeMsg : NetMessage
    {
        public string Reason = "";
        public override MsgType Type => MsgType.Goodbye;
        protected override void WriteBody(NetWriter w) => w.WriteString(Reason);
    }
}
