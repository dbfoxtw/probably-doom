// Probably Doom
// Copyright (C) 2026 dbfoxtw
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.

using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace ProbablyDoom.Verify
{
    /// <summary>離線測試輸出的檔案：PNG（畫面）與 WAV（音樂）。只給離線測試用，最簡單的寫法。</summary>
    internal static class Images
    {
        /// <summary>
        /// 把引擎的畫面緩衝（RGBA32、直行優先：(x, y) 在 4 * (x * height + y)）存成一般的 PNG（寬 width、高 height，左上角是原點）。
        /// 轉成列優先是為了讓存出來的圖和 Doom 的畫面方向一樣，主線打開來看方向與顏色對不對。
        /// </summary>
        public static void SaveFrame(string path, byte[] columnMajorRgba, int width, int height)
        {
            var rgb = new byte[width * height * 3];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var src = 4 * (x * height + y);
                    var dst = 3 * (y * width + x);
                    rgb[dst] = columnMajorRgba[src];
                    rgb[dst + 1] = columnMajorRgba[src + 1];
                    rgb[dst + 2] = columnMajorRgba[src + 2];
                }
            }
            SavePng(path, rgb, width, height);
        }

        static void SavePng(string path, byte[] rgb, int width, int height)
        {
            // 每列前面一個 filter byte（0＝不過濾）
            var raw = new byte[(width * 3 + 1) * height];
            for (var y = 0; y < height; y++)
            {
                Buffer.BlockCopy(rgb, y * width * 3, raw, y * (width * 3 + 1) + 1, width * 3);
            }

            byte[] compressed;
            using (var ms = new MemoryStream())
            {
                using (var z = new ZLibStream(ms, CompressionLevel.Optimal, true))
                {
                    z.Write(raw, 0, raw.Length);
                }
                compressed = ms.ToArray();
            }

            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            fs.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
            var ihdr = new byte[13];
            WriteBE(ihdr, 0, (uint)width);
            WriteBE(ihdr, 4, (uint)height);
            ihdr[8] = 8; // 每個色彩分量 8 bit
            ihdr[9] = 2; // RGB
            WriteChunk(fs, "IHDR", ihdr);
            WriteChunk(fs, "IDAT", compressed);
            WriteChunk(fs, "IEND", Array.Empty<byte>());
        }

        static void WriteChunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4];
            WriteBE(len, 0, (uint)data.Length);
            s.Write(len);
            var typeBytes = Encoding.ASCII.GetBytes(type);
            s.Write(typeBytes);
            s.Write(data);
            var crc = Crc32(typeBytes, 0xFFFFFFFFu);
            crc = Crc32(data, crc) ^ 0xFFFFFFFFu;
            var crcBytes = new byte[4];
            WriteBE(crcBytes, 0, crc);
            s.Write(crcBytes);
        }

        static uint[] _crcTable;

        static uint Crc32(byte[] data, uint crc)
        {
            if (_crcTable == null)
            {
                _crcTable = new uint[256];
                for (uint n = 0; n < 256; n++)
                {
                    var c = n;
                    for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                    _crcTable[n] = c;
                }
            }
            foreach (var b in data) crc = _crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc;
        }

        static void WriteBE(byte[] b, int o, uint v)
        {
            b[o] = (byte)(v >> 24);
            b[o + 1] = (byte)(v >> 16);
            b[o + 2] = (byte)(v >> 8);
            b[o + 3] = (byte)v;
        }

        /// <summary>左右交錯的 float（-1～1）存成 16 bit 立體聲 WAV。</summary>
        public static void SaveWav(string path, float[] interleaved, int sampleRate)
        {
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var w = new BinaryWriter(fs);
            var dataBytes = interleaved.Length * 2;
            w.Write(Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + dataBytes);
            w.Write(Encoding.ASCII.GetBytes("WAVE"));
            w.Write(Encoding.ASCII.GetBytes("fmt "));
            w.Write(16);
            w.Write((short)1);          // PCM
            w.Write((short)2);          // 立體聲
            w.Write(sampleRate);
            w.Write(sampleRate * 4);    // 每秒的 bytes
            w.Write((short)4);          // 每個取樣框的 bytes
            w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data"));
            w.Write(dataBytes);
            foreach (var f in interleaved)
            {
                w.Write((short)Math.Clamp((int)(f * 32767), short.MinValue, short.MaxValue));
            }
        }
    }
}
