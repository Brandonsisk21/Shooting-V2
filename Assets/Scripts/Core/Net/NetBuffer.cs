using System;
using System.IO;
using System.Text;

namespace ArenaShooter.Core.Net
{
    /// <summary>Growable byte writer for network messages (little endian, varints for ints).</summary>
    public sealed class NetWriter
    {
        private byte[] _data;

        public int Length { get; private set; }
        public byte[] Buffer => _data;

        public NetWriter(int capacity = 256)
        {
            _data = new byte[Math.Max(16, capacity)];
        }

        public void Reset() => Length = 0;

        public byte[] ToArray()
        {
            var copy = new byte[Length];
            Array.Copy(_data, copy, Length);
            return copy;
        }

        public void WriteByte(byte value)
        {
            Ensure(1);
            _data[Length++] = value;
        }

        public void WriteBool(bool value) => WriteByte(value ? (byte)1 : (byte)0);

        /// <summary>Unsigned LEB128 varint.</summary>
        public void WriteUInt(uint value)
        {
            while (value >= 0x80)
            {
                WriteByte((byte)(value | 0x80));
                value >>= 7;
            }
            WriteByte((byte)value);
        }

        /// <summary>ZigZag + varint: small negative numbers stay small.</summary>
        public void WriteInt(int value) => WriteUInt((uint)((value << 1) ^ (value >> 31)));

        public void WriteULong(ulong value)
        {
            Ensure(8);
            for (int i = 0; i < 8; i++) _data[Length++] = (byte)(value >> (8 * i));
        }

        public void WriteFloat(float value)
        {
            Ensure(4);
            var bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
            Array.Copy(bytes, 0, _data, Length, 4);
            Length += 4;
        }

        public void WriteDouble(double value)
        {
            Ensure(8);
            var bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
            Array.Copy(bytes, 0, _data, Length, 8);
            Length += 8;
        }

        public void WriteV3(V3 v)
        {
            WriteFloat(v.X);
            WriteFloat(v.Y);
            WriteFloat(v.Z);
        }

        public void WriteString(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? "");
            WriteUInt((uint)bytes.Length);
            Ensure(bytes.Length);
            Array.Copy(bytes, 0, _data, Length, bytes.Length);
            Length += bytes.Length;
        }

        private void Ensure(int extra)
        {
            if (Length + extra <= _data.Length) return;
            Array.Resize(ref _data, Math.Max(_data.Length * 2, Length + extra));
        }
    }

    /// <summary>Reader for <see cref="NetWriter"/> data. Throws <see cref="InvalidDataException"/> on malformed input.</summary>
    public sealed class NetReader
    {
        public const int MaxStringBytes = 256;
        public const int MaxListCount = 256;

        private readonly byte[] _data;
        private readonly int _end;
        private int _pos;

        public NetReader(byte[] data, int offset = 0, int count = -1)
        {
            _data = data;
            _pos = offset;
            _end = count < 0 ? data.Length : offset + count;
        }

        public int Remaining => _end - _pos;

        public byte ReadByte()
        {
            Need(1);
            return _data[_pos++];
        }

        public bool ReadBool() => ReadByte() != 0;

        public uint ReadUInt()
        {
            uint result = 0;
            for (int shift = 0; shift < 35; shift += 7)
            {
                byte b = ReadByte();
                result |= (uint)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) return result;
            }
            throw new InvalidDataException("Varint too long.");
        }

        public int ReadInt()
        {
            uint raw = ReadUInt();
            return (int)(raw >> 1) ^ -(int)(raw & 1);
        }

        public ulong ReadULong()
        {
            Need(8);
            ulong value = 0;
            for (int i = 0; i < 8; i++) value |= (ulong)_data[_pos++] << (8 * i);
            return value;
        }

        public float ReadFloat()
        {
            Need(4);
            float value;
            if (BitConverter.IsLittleEndian) value = BitConverter.ToSingle(_data, _pos);
            else
            {
                var tmp = new byte[4];
                Array.Copy(_data, _pos, tmp, 0, 4);
                Array.Reverse(tmp);
                value = BitConverter.ToSingle(tmp, 0);
            }
            _pos += 4;
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("Non-finite float.");
            return value;
        }

        public double ReadDouble()
        {
            Need(8);
            double value;
            if (BitConverter.IsLittleEndian) value = BitConverter.ToDouble(_data, _pos);
            else
            {
                var tmp = new byte[8];
                Array.Copy(_data, _pos, tmp, 0, 8);
                Array.Reverse(tmp);
                value = BitConverter.ToDouble(tmp, 0);
            }
            _pos += 8;
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new InvalidDataException("Non-finite double.");
            return value;
        }

        public V3 ReadV3() => new V3(ReadFloat(), ReadFloat(), ReadFloat());

        public string ReadString()
        {
            uint length = ReadUInt();
            if (length > MaxStringBytes) throw new InvalidDataException("String too long.");
            Need((int)length);
            string s = Encoding.UTF8.GetString(_data, _pos, (int)length);
            _pos += (int)length;
            return s;
        }

        /// <summary>A list length, bounded so a malicious packet can't make us allocate huge arrays.</summary>
        public int ReadCount()
        {
            uint count = ReadUInt();
            if (count > MaxListCount) throw new InvalidDataException("List too long.");
            return (int)count;
        }

        private void Need(int count)
        {
            if (count < 0 || _pos + count > _end) throw new InvalidDataException("Message truncated.");
        }
    }

    /// <summary>Engine-free 3D vector for network data.</summary>
    public readonly struct V3 : IEquatable<V3>
    {
        public readonly float X, Y, Z;

        public V3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static V3 Lerp(V3 a, V3 b, float t) => new V3(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);

        public float DistanceTo(V3 o)
        {
            float dx = X - o.X, dy = Y - o.Y, dz = Z - o.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public bool Equals(V3 o) => X == o.X && Y == o.Y && Z == o.Z;
        public override bool Equals(object obj) => obj is V3 o && Equals(o);
        public override int GetHashCode() => (X, Y, Z).GetHashCode();
        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}
