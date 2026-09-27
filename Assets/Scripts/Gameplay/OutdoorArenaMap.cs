using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// First real map (GDD 3.1): "Crash Site", a Midship-inspired FFA arena for 4–8 players with
    /// 180° rotational symmetry, dressed as an alien planet where two dropships crash-landed
    /// (GDD 5.1). Decorations never change collision: the gray-box layout is the gameplay.
    ///
    /// Playable area is 44 m (x) by 64 m (z), ringed by cliffs. North (+z) is the Red dropship base and
    /// south (-z) the Blue one; everything on one half has a rotated twin on the other.
    ///
    /// Elevation tiers:
    ///   0  ground (y 0): open center field, trees and boulders, tunnels under both bases
    ///   1  y 2 to 3.5: center platform with the sniper, side ridges, base decks
    ///   2  y 6.5: small overlook atop each base, sightline to the center
    /// </summary>
    public static class OutdoorArenaMap
    {
        public const string DisplayName = "Crash Site";

        private const float HalfWidth = 22f;   // x
        private const float HalfLength = 32f; // z

        public static MapInfo Build(Transform root)
        {
            var map = new MapInfo
            {
                Name = DisplayName,
                Root = root,
                PlayArea = new Bounds(new Vector3(0f, 5f, 0f), new Vector3(HalfWidth * 2f, 16f, HalfLength * 2f)),
                HasBots = true,
            };
            var b = new SymmetricBuilder(root);

            BuildGround(root);
            map.SniperPad = BuildCenter(b, root);
            BuildBases(b);
            BuildSideRidges(b);
            BuildGroundCover(b);
            BuildBoundary(b, root);
            BuildDropships(b);
            SkyDecor.Build(b);

            // 8 spawns (4 per half), all facing the center. GDD 3.2: ~6 for 4–8 players, expandable to 8.
            map.Spawns.AddRange(b.Spawn("Spawn_BaseDeck", new Vector3(0f, 3.5f, 26.5f)));
            map.Spawns.AddRange(b.Spawn("Spawn_BaseLanding", new Vector3(-11f, 3.5f, 26f)));
            map.Spawns.AddRange(b.Spawn("Spawn_CornerEast", new Vector3(18f, 0f, 26f)));
            map.Spawns.AddRange(b.Spawn("Spawn_CornerWest", new Vector3(-18f, 0f, 20f)));

            // Grenade pickups (GDD 2.2.1): in front of each base, on each ridge, and in the open field.
            b.GrenadeSpawn(new Vector3(0f, 0f, 17f));
            b.GrenadeSpawn(new Vector3(18f, 2f, -6f));
            b.GrenadeSpawn(new Vector3(7f, 0f, 7f));

            return map;
        }

        private static void BuildGround(Transform root)
        {
            GrayBox.Box("Ground", new Vector3(0f, -0.5f, 0f), new Vector3(90f, 1f, 110f), OutdoorPalette.Grass, root);
            // Sand path from base to base through the center, as a visual guide line only.
            GrayBox.Visual(PrimitiveType.Cube, "Path", root, new Vector3(0f, 0.01f, 0f), new Vector3(4f, 0.02f, 44f), OutdoorPalette.Sand);
        }

        /// <summary>
        /// 10 x 10 m platform, 3 m high, with the sniper pad on top. Reachable by two ramps (north-east,
        /// south-west) and by jumping up boulder steps (east, west), so no single approach dominates.
        /// </summary>
        private static SniperSpawnPad BuildCenter(SymmetricBuilder b, Transform root)
        {
            const float top = 3f;
            GrayBox.Box("CenterPlatform", new Vector3(0f, top / 2f, 0f), new Vector3(10f, top, 10f), OutdoorPalette.Stone, root);
            foreach (var corner in new[] { new Vector3(4.6f, 0f, 4.6f), new Vector3(-4.6f, 0f, 4.6f) })
                b.Box("CenterPost", corner + Vector3.up * (top + 1f), new Vector3(0.6f, 2f, 0.6f), OutdoorPalette.StoneDark);

            b.Ramp("CenterRamp", new Vector3(2.5f, 0f, 13f), new Vector3(2.5f, top, 5f), 3.5f, OutdoorPalette.Stone);

            // Boulder steps: ground -> 1.2 m -> 2.2 m -> 3 m platform (each rise is under the 1.3 m jump).
            b.Box("CenterStepLow", new Vector3(8f, 0.6f, 1f), new Vector3(2f, 1.2f, 3f), OutdoorPalette.Rock); // flat tops: you stand on these
            b.Box("CenterStepHigh", new Vector3(6f, 1.1f, 1.5f), new Vector3(2f, 2.2f, 2f), OutdoorPalette.RockLight);

            // Landing-pad dressing: glowing ring around the pad and beacon lights on the corner posts.
            GrayBox.GlowVisual(PrimitiveType.Cylinder, "PadRing", root, new Vector3(0f, top + 0.015f, 0f), new Vector3(3.4f, 0.01f, 3.4f), OutdoorPalette.Glow);
            GrayBox.Visual(PrimitiveType.Cylinder, "PadRingInner", root, new Vector3(0f, top + 0.02f, 0f), new Vector3(2.9f, 0.01f, 2.9f), OutdoorPalette.StoneDark);
            foreach (var corner in new[] { new Vector3(4.6f, 0f, 4.6f), new Vector3(-4.6f, 0f, 4.6f) })
                b.Visual(PrimitiveType.Sphere, "Beacon", corner + Vector3.up * (top + 2.15f), Vector3.one * 0.35f, OutdoorPalette.EngineGlow, glow: true);

            var pad = GrayBox.Box("SniperPad", new Vector3(0f, top + 0.05f, 0f), new Vector3(1.6f, 0.1f, 1.6f), OutdoorPalette.SniperPad, root);
            return pad.AddComponent<SniperSpawnPad>();
        }

        /// <summary>
        /// Each base: an 18 x 8 m deck at 3.5 m on pillars (a covered tunnel underneath), side
        /// landings reached by ramps, a back wall, and a small tier-2 overlook at 6.5 m.
        /// </summary>
        private static void BuildBases(SymmetricBuilder b)
        {
            const float deck = 3.5f, over = 6.5f;
            var red = OutdoorPalette.RedAccent;
            var blue = OutdoorPalette.BlueAccent;

            b.Box("BaseDeck", new Vector3(0f, deck - 0.25f, 24f), new Vector3(18f, 0.5f, 8f), OutdoorPalette.Stone);
            b.Box("BaseBackWall", new Vector3(0f, over / 2f, 28.5f), new Vector3(26f, over, 1f), OutdoorPalette.StoneDark);
            foreach (float x in new[] { -8.5f, -3f, 3f, 8.5f })
                b.Box("BasePillar", new Vector3(x, (deck - 0.5f) / 2f, 20.5f), new Vector3(0.8f, deck - 0.5f, 0.8f), OutdoorPalette.StoneDark);

            // Railings along the front edge with a gap in the middle to drop down.
            // 1.3 m: hides a crouched grunt, lets you stand up to shoot over.
            b.Box("BaseRailWest", new Vector3(-6.5f, deck + 0.65f, 20.2f), new Vector3(5f, 1.3f, 0.5f), red, blue);
            b.Box("BaseRailEast", new Vector3(6.5f, deck + 0.65f, 20.2f), new Vector3(5f, 1.3f, 0.5f), red, blue);

            // Side landings + ramps down to the field.
            foreach (float x in new[] { -11f, 11f })
            {
                b.Box("BaseLanding", new Vector3(x, deck - 0.25f, 25f), new Vector3(4f, 0.5f, 6f), OutdoorPalette.Stone);
                b.Box("BaseLandingPillar", new Vector3(x, (deck - 0.5f) / 2f, 22.5f), new Vector3(0.8f, deck - 0.5f, 0.8f), OutdoorPalette.StoneDark);
                b.Ramp("BaseRamp", new Vector3(x, 0f, 14f), new Vector3(x, deck, 22f), 3f, OutdoorPalette.Stone);
            }

            // Tier-2 overlook, reached by a ramp from the deck (east side on Red, west on Blue).
            b.Box("Overlook", new Vector3(0f, over - 0.25f, 26f), new Vector3(6f, 0.5f, 4f), red, blue);
            foreach (float x in new[] { -2.7f, 2.7f })
                b.Box("OverlookPillar", new Vector3(x, (deck + over - 0.5f) / 2f, 24.3f), new Vector3(0.5f, over - deck - 0.5f, 0.5f), OutdoorPalette.StoneDark);
            b.Ramp("OverlookRamp", new Vector3(8f, deck, 26f), new Vector3(3f, over, 26f), 2f, OutdoorPalette.Stone);

            // Crates in the tunnel under the deck.
            b.Crate("TunnelCrate", new Vector3(5f, 0.75f, 24f), 1.5f, 15f);
            b.Crate("TunnelCrate2", new Vector3(-4f, 0.75f, 25.5f), 1.5f, -10f);

            // Team stripe along the deck's front edge and glowing portholes on the hull wall.
            b.Visual(PrimitiveType.Cube, "DeckStripe", new Vector3(0f, deck - 0.3f, 19.98f), new Vector3(18f, 0.22f, 0.04f), red, blue);
            foreach (float x in new[] { -10f, -6.5f, 6.5f, 10f })
                b.Visual(PrimitiveType.Sphere, "Porthole", new Vector3(x, 5.3f, 27.98f), new Vector3(1.1f, 1.1f, 0.05f), OutdoorPalette.Window, glow: true);
        }

        /// <summary>Raised rock ledges along both long sides: a flanking route with partial cover.</summary>
        private static void BuildSideRidges(SymmetricBuilder b)
        {
            const float top = 2f;
            b.Box("Ridge", new Vector3(18f, top / 2f, 0f), new Vector3(8f, top, 20f), OutdoorPalette.RockLight);
            b.Ramp("RidgeRampNorth", new Vector3(18f, 0f, 17f), new Vector3(18f, top, 10f), 4f, OutdoorPalette.RockLight);
            b.Ramp("RidgeRampSouth", new Vector3(18f, 0f, -17f), new Vector3(18f, top, -10f), 4f, OutdoorPalette.RockLight);
            b.Box("RidgeWallNorth", new Vector3(14.35f, top + 0.65f, 5f), new Vector3(0.5f, 1.3f, 5f), OutdoorPalette.Rock);
            b.Box("RidgeWallSouth", new Vector3(14.35f, top + 0.65f, -4f), new Vector3(0.5f, 1.3f, 5f), OutdoorPalette.Rock);
        }

        private static void BuildGroundCover(SymmetricBuilder b)
        {
            b.Tree("Tree_Field", new Vector3(10f, 0f, 7f), 4f, 1.6f);
            b.Tree("Tree_BaseWest", new Vector3(-6.5f, 0f, 17f), 4.5f, 1.6f);
            b.Tree("Tree_Corner", new Vector3(-16f, 0f, -20f), 4.2f, 1.8f);

            // Cover sized to the two poses (GDD 3.1): ~1.3 m hides you crouched, 2.3 m+ hides you standing.
            b.RockBox("Rock_NearRamp", new Vector3(7.5f, 1.2f, -13.5f), new Vector3(4.2f, 2.4f, 2.4f), OutdoorPalette.Rock, 20f);
            b.RockBox("Rock_Field", new Vector3(-11f, 1.2f, 3.5f), new Vector3(3.4f, 2.4f, 3.4f), OutdoorPalette.RockLight, 35f);
            b.RockBox("Rock_Lane", new Vector3(12.5f, 0.7f, -9f), new Vector3(2.6f, 1.4f, 4.2f), OutdoorPalette.Rock, 10f);
            b.RockBox("Rock_Pillar", new Vector3(-13f, 1.3f, -12f), new Vector3(2.4f, 2.6f, 2.4f), OutdoorPalette.RockLight, 15f);

            b.Box("LowWall", new Vector3(-2.5f, 0.65f, 10f), new Vector3(4.6f, 1.3f, 0.8f), OutdoorPalette.StoneDark);

            // Crate stack between center and base: full standing cover on the way in.
            b.Crate("FieldCrate", new Vector3(6.5f, 0.7f, 15.5f), 1.4f, 12f);
            b.Crate("FieldCrateTop", new Vector3(6.6f, 1.9f, 15.4f), 1.0f, 40f);
            b.Crate("FieldCrateSide", new Vector3(5.2f, 0.5f, 16.3f), 1.0f, -8f);
        }

        /// <summary>
        /// A crashed dropship lying behind each base's back wall (outside the play space): hull,
        /// cockpit bubble, tail fins, glowing engines and a team stripe. Visual only.
        /// </summary>
        private static void BuildDropships(SymmetricBuilder b)
        {
            var red = OutdoorPalette.RedAccent;
            var blue = OutdoorPalette.BlueAccent;
            if (ModelLibrary.Has("dropship"))
            {
                // Nose pointing along -x, tipped nose-down and rolled onto a wing: crash-landed.
                b.ModelPair("dropship", new Vector3(1f, 4.2f, 34.5f), new Vector3(7f, -90f, 9f), 0.8f, red, blue);
                return;
            }
            // Lying along x, nose down and slightly buried, the top showing over the 6.5 m wall.
            b.Visual(PrimitiveType.Capsule, "ShipHull", new Vector3(1f, 5f, 32f), new Vector3(7f, 11f, 6.5f), OutdoorPalette.Stone, new Vector3(0f, 0f, 84f));
            b.Visual(PrimitiveType.Capsule, "ShipStripe", new Vector3(1f, 5.05f, 32f), new Vector3(7.1f, 3f, 6.6f), red, blue, new Vector3(0f, 0f, 84f));
            b.Visual(PrimitiveType.Sphere, "ShipCockpit", new Vector3(-8f, 7.2f, 31f), new Vector3(3.2f, 2.2f, 2.6f), OutdoorPalette.Window, glow: true);
            b.Visual(PrimitiveType.Cube, "ShipFinTop", new Vector3(11f, 10.3f, 32f), new Vector3(3.2f, 4f, 0.4f), red, blue, new Vector3(0f, 0f, -25f));
            b.Visual(PrimitiveType.Cube, "ShipFinSide", new Vector3(11.5f, 6.8f, 29.5f), new Vector3(3f, 0.4f, 3.5f), red, blue, new Vector3(20f, 0f, -10f));
            foreach (float z in new[] { 30.6f, 33.4f })
            {
                b.Visual(PrimitiveType.Cylinder, "ShipEngine", new Vector3(13f, 6f, z), new Vector3(1.8f, 1.2f, 1.8f), OutdoorPalette.StoneDark, new Vector3(0f, 0f, 84f));
                b.Visual(PrimitiveType.Cylinder, "ShipEngineGlow", new Vector3(14.25f, 6.15f, z), new Vector3(1.4f, 0.05f, 1.4f), OutdoorPalette.EngineGlow, new Vector3(0f, 0f, 84f), glow: true);
            }
        }

        /// <summary>Cliffs ring the arena (visual + solid), backed by tall invisible walls, with hills beyond.</summary>
        private static void BuildBoundary(SymmetricBuilder b, Transform root)
        {
            // Invisible walls stop players climbing out.
            GrayBox.Blocker("Bound_East", new Vector3(HalfWidth + 0.5f, 20f, 0f), new Vector3(1f, 42f, HalfLength * 2f + 2f), root);
            GrayBox.Blocker("Bound_West", new Vector3(-HalfWidth - 0.5f, 20f, 0f), new Vector3(1f, 42f, HalfLength * 2f + 2f), root);
            GrayBox.Blocker("Bound_North", new Vector3(0f, 20f, HalfLength + 0.5f), new Vector3(HalfWidth * 2f + 2f, 42f, 1f), root);
            GrayBox.Blocker("Bound_South", new Vector3(0f, 20f, -HalfLength - 0.5f), new Vector3(HalfWidth * 2f + 2f, 42f, 1f), root);

            // Seeded so the cliffs look the same every run.
            var rng = new System.Random(1234);
            float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);

            for (float z = -HalfLength - 2f; z <= HalfLength + 2f; z += 4.5f)
            {
                float h = Range(7f, 13f);
                b.RockBox("Cliff_Side", new Vector3(HalfWidth + 4f + Range(0f, 1.5f), h / 2f, z), new Vector3(Range(5f, 7f), h, Range(4.5f, 6f)),
                    rng.Next(2) == 0 ? OutdoorPalette.Cliff : OutdoorPalette.CliffLight, Range(-12f, 12f));
            }
            for (float x = 0f; x <= HalfWidth + 4f; x += 4.5f)
            {
                // Only x >= 0 here: the twin covers the other side of the far end.
                float h = Range(8f, 14f);
                b.RockBox("Cliff_End", new Vector3(x, h / 2f, HalfLength + 3f + Range(0f, 1.5f)), new Vector3(Range(4.5f, 6f), h, Range(5f, 7f)),
                    rng.Next(2) == 0 ? OutdoorPalette.Cliff : OutdoorPalette.CliffLight, Range(-12f, 12f));
                if (x > 0f)
                {
                    b.RockBox("Cliff_End", new Vector3(-x, h / 2f, HalfLength + 3.5f), new Vector3(Range(4.5f, 6f), h, Range(5f, 7f)),
                        OutdoorPalette.Cliff, Range(-12f, 12f));
                }
            }

            // Distant rolling hills, softened by fog.
            for (int i = 0; i < 6; i++)
            {
                float angle = i * 30f + Range(-8f, 8f);
                float distance = Range(85f, 120f);
                Vector3 pos = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance;
                float h = Range(14f, 28f);
                b.RockBox("Hill", new Vector3(pos.x, h / 2f - 2f, pos.z), new Vector3(Range(30f, 50f), h, Range(25f, 40f)), OutdoorPalette.Hills, angle);
            }
            GrayBox.Box("FarGround", new Vector3(0f, -0.6f, 0f), new Vector3(320f, 1f, 320f), OutdoorPalette.Hills, root);
        }
    }
}
