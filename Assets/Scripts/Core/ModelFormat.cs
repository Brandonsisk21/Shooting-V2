using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ArenaShooter.Core
{
    public sealed class ModelSubmesh
    {
        /// <summary>Material name resolved by the game (e.g. "Team", "Glass", "Color:ff8f33").</summary>
        public string Material;
        public int[] Indices;
    }

    public sealed class ModelPart
    {
        public string Name;
        /// <summary>Joint position in model space; <see cref="Positions"/> are relative to it.</summary>
        public float[] Pivot = new float[3];
        /// <summary>xyz triples.</summary>
        public float[] Positions;
        /// <summary>xyz triples, unit length.</summary>
        public float[] Normals;
        public readonly List<ModelSubmesh> Submeshes = new List<ModelSubmesh>();

        public int VertexCount => Positions.Length / 3;
    }

    public sealed class ModelData
    {
        public readonly List<ModelPart> Parts = new List<ModelPart>();
        /// <summary>Named points in model space (e.g. "muzzle", "gunMount").</summary>
        public readonly Dictionary<string, float[]> Markers = new Dictionary<string, float[]>();

        public ModelPart Part(string name) => Parts.Find(p => p.Name == name);

        /// <summary>Axis-aligned bounds of all parts in model space: min xyz, max xyz.</summary>
        public void GetBounds(out float[] min, out float[] max)
        {
            min = new[] { float.MaxValue, float.MaxValue, float.MaxValue };
            max = new[] { float.MinValue, float.MinValue, float.MinValue };
            foreach (var p in Parts)
                for (int i = 0; i < p.Positions.Length; i += 3)
                    for (int k = 0; k < 3; k++)
                    {
                        float v = p.Positions[i + k] + p.Pivot[k];
                        if (v < min[k]) min[k] = v;
                        if (v > max[k]) max[k] = v;
                    }
        }
    }

    /// <summary>
    /// Reads the "SGM1" model files written by Tools/ModelGen (Space Grunts' procedural 3D models).
    /// Engine-free so it can be unit tested; the game turns the result into Unity meshes.
    /// </summary>
    public static class ModelFormat
    {
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("SGM1");

        public static ModelData Parse(byte[] bytes)
        {
            using var reader = new BinaryReader(new MemoryStream(bytes));
            var magic = reader.ReadBytes(4);
            for (int i = 0; i < 4; i++)
                if (magic.Length < 4 || magic[i] != Magic[i]) throw new InvalidDataException("Not an SGM1 model file.");

            var model = new ModelData();
            int partCount = reader.ReadInt32();
            for (int p = 0; p < partCount; p++)
            {
                var part = new ModelPart { Name = ReadString(reader), Pivot = ReadFloats(reader, 3) };
                int vc = reader.ReadInt32();
                part.Positions = ReadFloats(reader, vc * 3);
                part.Normals = ReadFloats(reader, vc * 3);
                int subCount = reader.ReadInt32();
                for (int s = 0; s < subCount; s++)
                {
                    var sub = new ModelSubmesh { Material = ReadString(reader) };
                    int ic = reader.ReadInt32();
                    sub.Indices = new int[ic];
                    for (int i = 0; i < ic; i++)
                    {
                        int index = reader.ReadInt32();
                        if (index < 0 || index >= vc) throw new InvalidDataException($"Index {index} out of range in part {part.Name}.");
                        sub.Indices[i] = index;
                    }
                    part.Submeshes.Add(sub);
                }
                model.Parts.Add(part);
            }

            int markerCount = reader.ReadInt32();
            for (int m = 0; m < markerCount; m++)
                model.Markers[ReadString(reader)] = ReadFloats(reader, 3);
            return model;
        }

        private static string ReadString(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            return Encoding.UTF8.GetString(reader.ReadBytes(length));
        }

        private static float[] ReadFloats(BinaryReader reader, int count)
        {
            var values = new float[count];
            for (int i = 0; i < count; i++) values[i] = reader.ReadSingle();
            return values;
        }
    }
}
