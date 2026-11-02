using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Workbench
{
    /// <summary>Минимальный PNG-писатель (RGBA 8 бит, без фильтров). Нужен только верстаку.</summary>
    internal static class Png
    {
        public static void Write(string path, int width, int height, byte[] rgbaTopDown)
        {
            using var file = File.Create(path);
            file.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

            var header = new byte[13];
            WriteBE(header, 0, (uint)width);
            WriteBE(header, 4, (uint)height);
            header[8] = 8;  // бит на канал
            header[9] = 6;  // RGBA
            Chunk(file, "IHDR", header);

            // Каждая строка: байт фильтра (0 = без фильтра) + пиксели.
            var raw = new byte[(width * 4 + 1) * height];
            for (int y = 0; y < height; y++)
                Buffer.BlockCopy(rgbaTopDown, y * width * 4, raw, y * (width * 4 + 1) + 1, width * 4);

            using (var compressed = new MemoryStream())
            {
                using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
                    z.Write(raw, 0, raw.Length);
                Chunk(file, "IDAT", compressed.ToArray());
            }

            Chunk(file, "IEND", Array.Empty<byte>());
        }

        private static void Chunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4];
            WriteBE(len, 0, (uint)data.Length);
            s.Write(len);
            var typeBytes = Encoding.ASCII.GetBytes(type);
            s.Write(typeBytes);
            s.Write(data);
            uint crc = Crc(typeBytes, 0xFFFFFFFFu);
            crc = Crc(data, crc) ^ 0xFFFFFFFFu;
            var crcBytes = new byte[4];
            WriteBE(crcBytes, 0, crc);
            s.Write(crcBytes);
        }

        private static readonly uint[] table = BuildTable();

        private static uint[] BuildTable()
        {
            var t = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                t[n] = c;
            }
            return t;
        }

        private static uint Crc(byte[] data, uint crc)
        {
            foreach (var b in data) crc = table[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc;
        }

        private static void WriteBE(byte[] buf, int offset, uint v)
        {
            buf[offset] = (byte)(v >> 24);
            buf[offset + 1] = (byte)(v >> 16);
            buf[offset + 2] = (byte)(v >> 8);
            buf[offset + 3] = (byte)v;
        }
    }
}
