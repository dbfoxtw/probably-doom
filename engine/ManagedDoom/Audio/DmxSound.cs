//
// Probably Doom 修改（2026-11-12）：從上游的 Silk/SilkSound.cs 搬出解 WAD 音效 lump（DMX 格式）的部分
// （GetSamples、ContainsDmxPadding、GetAmplitude），平台層 Silk/ 整個不編，播放改由宿主做（Unity 的 AudioClip）。
// 解碼的算法照搬；回傳值從 Span<byte> 改成 DmxSound 物件（取樣率、8 bit 取樣、換成 -1～1 的 float），宿主比較好用。
//
// Copyright (C) 1993-1996 Id Software, Inc.
// Copyright (C) 2019-2020 Nobuaki Tanaka
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
//



using System;

namespace ManagedDoom.Audio
{
    /// <summary>
    /// WAD 裡的一個音效（DS* lump，DMX 格式：檔頭 8 bytes＝格式 3、取樣率、取樣數，接著 8 bit 無號單聲道的取樣）。
    /// </summary>
    public sealed class DmxSound
    {
        private int sampleRate;
        private byte[] samples;
        private float amplitude;

        private DmxSound(int sampleRate, byte[] samples, float amplitude)
        {
            this.sampleRate = sampleRate;
            this.samples = samples;
            this.amplitude = amplitude;
        }

        /// <summary>
        /// 讀 WAD 裡名叫 name（例如 "DSPISTOL"）的音效。沒有這個 lump、或內容是空的（上游也跳過）時回傳 null。
        /// </summary>
        public static DmxSound FromWad(Wad wad, string name)
        {
            var lump = wad.GetLumpNumber(name);
            if (lump == -1)
            {
                return null;
            }

            return FromLump(wad.ReadLump(lump));
        }

        /// <summary>解一個 DMX 音效 lump 的內容；太短或沒有取樣時回傳 null。</summary>
        public static DmxSound FromLump(byte[] data)
        {
            if (data.Length < 8)
            {
                return null;
            }

            var sampleRate = BitConverter.ToUInt16(data, 2);
            var sampleCount = BitConverter.ToInt32(data, 4);

            var offset = 8;
            if (ContainsDmxPadding(data))
            {
                offset += 16;
                sampleCount -= 32;
            }

            // 上游沒檢查檔頭寫的取樣數超過 lump 長度的情況（Span 會丟例外）；這裡截到 lump 的長度
            sampleCount = Math.Min(sampleCount, data.Length - offset);

            if (sampleCount <= 0)
            {
                return null;
            }

            var samples = new byte[sampleCount];
            Array.Copy(data, offset, samples, 0, sampleCount);

            return new DmxSound(sampleRate, samples, GetAmplitude(samples, sampleRate, sampleCount));
        }

        // Check if the data contains pad bytes.
        // If the first and last 16 samples are the same,
        // the data should contain pad bytes.
        // https://doomwiki.org/wiki/Sound
        private static bool ContainsDmxPadding(byte[] data)
        {
            var sampleCount = BitConverter.ToInt32(data, 4);
            if (sampleCount < 32 || 8 + sampleCount > data.Length)
            {
                return false;
            }
            else
            {
                var first = data[8];
                for (var i = 1; i < 16; i++)
                {
                    if (data[8 + i] != first)
                    {
                        return false;
                    }
                }

                var last = data[8 + sampleCount - 1];
                for (var i = 1; i < 16; i++)
                {
                    if (data[8 + sampleCount - i - 1] != last)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static float GetAmplitude(byte[] samples, int sampleRate, int sampleCount)
        {
            var max = 0;
            if (sampleCount > 0)
            {
                var count = Math.Min(sampleRate / 5, sampleCount);
                for (var t = 0; t < count; t++)
                {
                    var a = samples[t] - 128;
                    if (a < 0)
                    {
                        a = -a;
                    }
                    if (a > max)
                    {
                        max = a;
                    }
                }
            }
            return (float)max / 128;
        }

        /// <summary>換成 -1～1 的 float（Unity 的 AudioClip.SetData 要的格式）。</summary>
        public float[] ToFloat()
        {
            var result = new float[samples.Length];
            for (var i = 0; i < samples.Length; i++)
            {
                result[i] = (samples[i] - 128) / 128F;
            }
            return result;
        }

        /// <summary>取樣率（Hz），原版的音效多半是 11025。</summary>
        public int SampleRate => sampleRate;

        /// <summary>8 bit 無號的取樣（128 是靜音），已去掉 DMX 的前後填充。</summary>
        public byte[] Samples => samples;

        /// <summary>開頭 0.2 秒的最大振幅（0～1）：上游用來算同時播很多音效時誰優先。</summary>
        public float Amplitude => amplitude;

        /// <summary>長度（秒）。</summary>
        public double Duration => (double)samples.Length / sampleRate;
    }
}
