using System.IO;
using ArenaShooter.Core.Net;
using NUnit.Framework;

namespace ArenaShooter.Core.Tests
{
    public class NetProtocolTests
    {
        private static T RoundTrip<T>(T message) where T : NetMessage
        {
            var decoded = NetMessage.Read(message.ToBytes());
            Assert.IsInstanceOf<T>(decoded);
            return (T)decoded;
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(-1)]
        [TestCase(127)]
        [TestCase(-300)]
        [TestCase(int.MaxValue)]
        [TestCase(int.MinValue)]
        public void VarintsRoundTrip(int value)
        {
            var w = new NetWriter();
            w.WriteInt(value);
            Assert.AreEqual(value, new NetReader(w.ToArray()).ReadInt());
        }

        [Test]
        public void SmallIntsAreOneByte()
        {
            var w = new NetWriter();
            w.WriteInt(-5);
            Assert.AreEqual(1, w.Length);
        }

        [Test]
        public void HelloAndWelcomeRoundTrip()
        {
            var hello = RoundTrip(new HelloMsg { Name = "Pvt. Brandon 🚀" });
            Assert.AreEqual("Pvt. Brandon 🚀", hello.Name);
            Assert.AreEqual(NetProtocol.Version, hello.Version);

            var welcome = RoundTrip(new WelcomeMsg
            {
                YourId = 42,
                Setup = new MatchSetup { map = MapChoice.CrashSite, botCount = 4, difficulty = Difficulty.Hard, scoreLimit = 15, timeLimitMinutes = 0 },
            });
            Assert.AreEqual(42, welcome.YourId);
            Assert.AreEqual(4, welcome.Setup.botCount);
            Assert.AreEqual(Difficulty.Hard, welcome.Setup.difficulty);
            Assert.AreEqual(15, welcome.Setup.scoreLimit);
            Assert.IsFalse(welcome.Setup.HasTimeLimit);
        }

        [Test]
        public void PlayerStateRoundTrip()
        {
            var msg = new PlayerStateMsg { Life = 3, Position = new V3(1.5f, 2f, -30f), Yaw = 270f, Pitch = -12f, ActiveSlot = 1, Crouched = true };
            msg.Slots.Add(new SlotState { Weapon = WeaponIds.Rifle, Magazine = 30, Reserve = 0 });
            msg.Slots.Add(new SlotState { Weapon = WeaponIds.Sniper, Magazine = 3, Reserve = 8 });
            var back = RoundTrip(msg);
            Assert.AreEqual(3, back.Life);
            Assert.AreEqual(new V3(1.5f, 2f, -30f), back.Position);
            Assert.AreEqual(270f, back.Yaw);
            Assert.IsTrue(back.Crouched);
            Assert.AreEqual(2, back.Slots.Count);
            Assert.AreEqual(WeaponIds.Sniper, back.Slots[1].Weapon);
            Assert.AreEqual(8, back.Slots[1].Reserve);
        }

        [Test]
        public void SnapshotRoundTripAndFitsInOnePacket()
        {
            var snap = new SnapshotMsg { Time = 123.456, HasTimeLimit = true, TimeRemaining = 530f, ScoreLimit = 25, PadHasWeapon = true, WinnerId = NetProtocol.NoId };
            for (int i = 0; i < 8; i++)
            {
                snap.Combatants.Add(new CombatantSnap { Id = i + 1, Position = new V3(i, 0, -i), Yaw = i * 40f, Alive = i != 3, Health = 100f - i, Weapon = WeaponIds.Rifle, RespawnIn = i == 3 ? 2.5f : 0f, Crouched = i == 5, Grenades = (byte)(i % 5) });
                snap.Scores.Add(new ScoreSnap { Id = i + 1, Kills = i * 3, Deaths = i, Suicides = i % 2 });
            }
            snap.Pickups.Add(new PickupSnap { NetId = 7, Weapon = WeaponIds.Sniper, Position = new V3(0, 4, 0), Magazine = 4, Reserve = 8 });
            for (int i = 0; i < 6; i++) snap.GrenadePickups.Add(i % 2 == 0);

            byte[] bytes = snap.ToBytes();
            Assert.Less(bytes.Length, 1100, "stays under one unreliable packet (no fragmentation)");

            var back = RoundTrip(snap);
            Assert.AreEqual(123.456, back.Time, 1e-9);
            Assert.AreEqual(8, back.Combatants.Count);
            Assert.IsFalse(back.Combatants[3].Alive);
            Assert.AreEqual(2.5f, back.Combatants[3].RespawnIn);
            Assert.AreEqual(21, back.Scores[7].Kills);
            Assert.AreEqual(7, back.Pickups[0].NetId);
            Assert.AreEqual(NetProtocol.NoId, back.WinnerId);
            Assert.IsTrue(back.Combatants[5].Crouched);
            Assert.AreEqual(4, back.Combatants[4].Grenades);
            CollectionAssert.AreEqual(new[] { true, false, true, false, true, false }, back.GrenadePickups);
        }

        [Test]
        public void EventsRoundTrip()
        {
            var shot = RoundTrip(new ShotReportMsg { Weapon = WeaponIds.Sniper, From = new V3(0, 1.6f, 0), To = new V3(0, 1.6f, 40), TargetId = 5, Zone = HitZone.Head });
            Assert.AreEqual(5, shot.TargetId);
            Assert.AreEqual(HitZone.Head, shot.Zone);

            var dmg = RoundTrip(new DamageEventMsg { VictimId = 2, AttackerId = 5, Amount = 100f, Zone = HitZone.Head, Killed = true, Weapon = WeaponIds.Sniper });
            Assert.IsTrue(dmg.Killed);
            Assert.AreEqual(5, dmg.AttackerId);

            var kill = RoundTrip(new KillEventMsg { KillerId = NetProtocol.NoId, VictimId = 2 });
            Assert.AreEqual(NetProtocol.NoId, kill.KillerId);

            var granted = RoundTrip(new PickupGrantedMsg { PickupId = 9, Outcome = PickupOutcome.AmmoTaken, Weapon = WeaponIds.Sniper, AmmoTaken = 6 });
            Assert.AreEqual(PickupOutcome.AmmoTaken, granted.Outcome);
            Assert.AreEqual(6, granted.AmmoTaken);

            var roster = new RosterMsg();
            roster.Entries.Add(new RosterEntry { Id = 1, Name = "You", R = 1, G = 1, B = 1, Owner = 76561198000000001UL });
            roster.Entries.Add(new RosterEntry { Id = 2, Name = "Pvt. Pickles", IsBot = true });
            var rb = RoundTrip(roster);
            Assert.AreEqual(76561198000000001UL, rb.Entries[0].Owner);
            Assert.IsTrue(rb.Entries[1].IsBot);

            Assert.AreEqual("host left", RoundTrip(new GoodbyeMsg { Reason = "host left" }).Reason);

            var thrown = RoundTrip(new GrenadeThrowMsg { From = new V3(1, 2, 3), Velocity = new V3(0, 3, 16) });
            Assert.AreEqual(new V3(0, 3, 16), thrown.Velocity);
            var spawned = RoundTrip(new GrenadeSpawnedMsg { Id = 12, ThrowerId = 3, From = new V3(1, 2, 3), Velocity = new V3(4, 5, 6) });
            Assert.AreEqual(3, spawned.ThrowerId);
            var boom = RoundTrip(new GrenadeExplodedMsg { Id = 12, Position = new V3(9, 0, 9) });
            Assert.AreEqual(12, boom.Id);
            Assert.IsInstanceOf<MatchResetMsg>(NetMessage.Read(new MatchResetMsg().ToBytes()));
        }

        [Test]
        public void MalformedDataIsRejected()
        {
            byte[] bytes = new SnapshotMsg().ToBytes();
            Assert.Throws<InvalidDataException>(() => NetMessage.Read(bytes, 0, bytes.Length - 3));
            Assert.Throws<InvalidDataException>(() => NetMessage.Read(new byte[] { 200 }));

            var w = new NetWriter();
            w.WriteByte((byte)MsgType.Roster);
            w.WriteUInt(100000); // absurd list length
            Assert.Throws<InvalidDataException>(() => NetMessage.Read(w.ToArray()));
        }

        [Test]
        public void WeaponIdsMapBothWays()
        {
            Assert.AreEqual("rifle", WeaponIds.ToId(WeaponIds.ToByte("rifle")));
            Assert.AreEqual("sniper", WeaponIds.ToId(WeaponIds.ToByte("sniper")));
            Assert.AreEqual(WeaponIds.None, WeaponIds.ToByte("banana"));
            Assert.AreEqual("grenade", WeaponIds.ToId(WeaponIds.ToByte("grenade")));
            Assert.IsNull(WeaponIds.Stats(WeaponIds.Grenade), "grenades aren't a held weapon");
            Assert.AreEqual(100f, WeaponIds.Stats(WeaponIds.Sniper).headDamage);
        }
    }

    public class InterpolationTests
    {
        private static Pose P(double t, float x, float yaw = 0f) => new Pose { Time = t, Position = new V3(x, 0, 0), Yaw = yaw };

        [Test]
        public void InterpolatesBetweenSnapshots()
        {
            var buffer = new InterpolationBuffer();
            buffer.Add(P(1.0, 0f));
            buffer.Add(P(1.1, 10f));
            Assert.IsTrue(buffer.TrySample(1.05, out var pose));
            Assert.AreEqual(5f, pose.Position.X, 1e-4f);
        }

        [Test]
        public void ExtrapolatesBrieflyThenHolds()
        {
            var buffer = new InterpolationBuffer { MaxExtrapolation = 0.1 };
            buffer.Add(P(1.0, 0f));
            buffer.Add(P(1.1, 1f));
            buffer.TrySample(1.15, out var ahead);
            Assert.AreEqual(1.5f, ahead.Position.X, 1e-3f);
            buffer.TrySample(5.0, out var held);
            Assert.AreEqual(2f, held.Position.X, 1e-3f, "stops 0.1 s past the last pose");
        }

        [Test]
        public void IgnoresOutOfOrderPoses()
        {
            var buffer = new InterpolationBuffer();
            buffer.Add(P(1.0, 0f));
            buffer.Add(P(1.2, 2f));
            buffer.Add(P(1.1, 99f));
            Assert.AreEqual(2, buffer.Count);
        }

        [Test]
        public void YawTakesTheShortWayAround()
        {
            Assert.AreEqual(360f, InterpolationBuffer.LerpAngle(350f, 10f, 0.5f), 1e-3f);
            Assert.AreEqual(-10f + 0f, InterpolationBuffer.LerpAngle(10f, -30f, 0.5f), 1e-3f);
        }

        [Test]
        public void ClockSyncTracksHostTimeAndIgnoresLateSpikes()
        {
            var clock = new ClockSync();
            clock.OnHostTime(100.0, 10.0); // host is 90 s ahead
            Assert.AreEqual(100.5, clock.HostTime(10.5), 1e-6);
            clock.OnHostTime(100.05, 10.3); // a very late packet: offset barely moves
            Assert.AreEqual(90.0, clock.HostTime(0) , 0.02);
        }
    }
}
