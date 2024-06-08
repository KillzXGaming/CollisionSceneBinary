using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace CollisionSceneBinaryTool
{
    public readonly ref struct TemporarySeekHandle
    {
        private readonly Stream Stream;
        private readonly long RetPos;

        public TemporarySeekHandle(Stream stream, long retpos)
        {
            this.Stream = stream;
            this.RetPos = retpos;
        }

        public readonly void Dispose()
        {
            Stream.Seek(RetPos, SeekOrigin.Begin);
        }
    }

    public static class IOExtension
    {
        public static TemporarySeekHandle TemporarySeek(this Stream stream, long offset, SeekOrigin origin)
        {
            long ret = stream.Position;
            stream.Seek(offset, origin);
            return new TemporarySeekHandle(stream, ret);
        }


        public static void Write(this BinaryWriter writer, float[] value)
        {
            for (int i = 0; i < value.Length; i++)
                writer.Write(value[i]);
        }

        public static void Write(this BinaryWriter writer, Vector3 value)
        {
            writer.Write(value.X);
            writer.Write(value.Y);
            writer.Write(value.Z);
        }

        public static void AlignBytes(this BinaryWriter writer, int align, byte pad_val = 0)
        {
            var startPos = writer.BaseStream.Position;
            long position = writer.Seek((int)(-writer.BaseStream.Position % align + align) % align, SeekOrigin.Current);

            writer.Seek((int)startPos, System.IO.SeekOrigin.Begin);
            while (writer.BaseStream.Position != position)
            {
                writer.Write((byte)pad_val);
            }
        }

        public static void WriteFixedString(this BinaryWriter writer, string name, int length)
        {
            byte[] string_buffer = Encoding.UTF8.GetBytes(name);
            if (string_buffer.Length > length)
                throw new Exception($"Input string too long {name}! Expected {length}");

            writer.Write(string_buffer);
            writer.Write(new byte[length - string_buffer.Length]);
        }

        public static string ReadZeroTerminatedString(this BinaryReader reader, int maxLength = int.MaxValue)
        {
            long start = reader.BaseStream.Position;
            int size = 0;

            // Read until we hit the end of the stream (-1) or a zero
            while (reader.ReadByte() - 1 > 0 && size < maxLength)
            {
                size++;
            }

            reader.BaseStream.Position = start;
            string text = Encoding.UTF8.GetString(reader.ReadBytes(size), 0, size);
            reader.BaseStream.Position++; // Skip the null byte
            return text;
        }

        public static sbyte[] ReadSbytes(this BinaryReader reader, int count)
        {
            sbyte[] values = new sbyte[count];
            for (int i = 0; i < count; i++)
                values[i] = reader.ReadSByte();
            return values;
        }

        public static bool[] ReadBooleans(this BinaryReader reader, int count)
        {
            bool[] values = new bool[count];
            for (int i = 0; i < count; i++)
                values[i] = reader.ReadBoolean();
            return values;
        }

        public static float[] ReadSingles(this BinaryReader reader, int count)
        {
            float[] values = new float[count];
            for (int i = 0; i < count; i++)
                values[i] = reader.ReadSingle();
            return values;
        }

        public static ushort[] ReadUInt16s(this BinaryReader reader, int count)
        {
            ushort[] values = new ushort[count];
            for (int i = 0; i < count; i++)
                values[i] = reader.ReadUInt16();
            return values;
        }

        public static int[] ReadInt32s(this BinaryReader reader, int count)
        {
            int[] values = new int[count];
            for (int i = 0; i < count; i++)
                values[i] = reader.ReadInt32();
            return values;
        }

        public static uint[] ReadUInt32s(this BinaryReader reader, int count)
        {
            uint[] values = new uint[count];
            for (int i = 0; i < count; i++)
                values[i] = reader.ReadUInt32();
            return values;
        }

        public static long[] ReadInt64s(this BinaryReader reader, int count)
        {
            long[] values = new long[count];
            for (int i = 0; i < count; i++)
                values[i] = reader.ReadInt64();
            return values;
        }

        public static ulong[] ReadUInt64s(this BinaryReader reader, int count)
        {
            ulong[] values = new ulong[count];
            for (int i = 0; i < count; i++)
                values[i] = reader.ReadUInt64();
            return values;
        }
    }
}
