//
// Probably Doom 修改（2026-11-12）：由上游的 Silk/SilkMusic.cs 改寫。上游用 DrippyAL（OpenAL）自己開音訊串流播，
// 這裡拿掉播放的部分，只留「選曲＋用 MeltySynth 合成」，由宿主（Unity 的音訊回呼）來要 PCM（pull 式）；
// 音色庫由宿主讀成 byte[] 交進來（上游照設定的檔名到執行檔旁找檔案）。選曲、換曲時重設合成器、音量的算法照上游。
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
using System.IO;
using MeltySynth;

namespace ManagedDoom.Audio
{
    /// <summary>
    /// Doom 的音樂：WAD 裡的 D_* lump（MUS 或 MIDI）用 MeltySynth＋音色庫（TimGM6mb.sf2）合成成 44100 Hz 立體聲。
    /// 引擎在遊戲的主執行緒呼叫 StartMusic（IMusic）；宿主在音訊執行緒呼叫 Render 要 PCM（Unity 的
    /// AudioClip PCMReaderCallback 或 OnAudioFilterRead）。兩邊只透過 volatile 的 reserved 交接解碼器，
    /// 合成器只在 Render 裡碰（同上游 SilkMusic 的 MusStream），所以不用鎖。
    /// </summary>
    public sealed class SynthMusic : IMusic
    {
        /// <summary>輸出的取樣率（Hz），固定 44100（同上游 MusDecoder.SampleRate）。</summary>
        public static readonly int SampleRate = MusDecoder.SampleRate;

        /// <summary>輸出的聲道數：立體聲，Render 寫的是左右交錯的取樣。</summary>
        public static readonly int ChannelCount = 2;

        private static readonly int blockLength = 2048;

        private Config config;
        private Wad wad;

        private Synthesizer synthesizer;

        private float[] left;
        private float[] right;

        private volatile IMusicDecoder current;
        private volatile IMusicDecoder reserved;

        private Bgm currentBgm;

        /// <param name="soundFont">音色庫 .sf2 的整個內容（宿主從 mod 資料夾讀）。</param>
        public SynthMusic(Config config, GameContent content, byte[] soundFont)
            : this(config, content, new SoundFont(new MemoryStream(soundFont, false)))
        {
        }

        public SynthMusic(Config config, GameContent content, SoundFont soundFont)
        {
            this.config = config;
            this.wad = content.Wad;

            config.audio_musicvolume = Math.Clamp(config.audio_musicvolume, 0, MaxVolume);

            var settings = new SynthesizerSettings(MusDecoder.SampleRate);
            settings.BlockSize = MusDecoder.BlockLength;
            settings.EnableReverbAndChorus = config.audio_musiceffect;
            synthesizer = new Synthesizer(soundFont, settings);

            left = new float[blockLength];
            right = new float[blockLength];

            currentBgm = Bgm.NONE;
        }

        public void StartMusic(Bgm bgm, bool loop)
        {
            if (bgm == currentBgm)
            {
                return;
            }

            var lump = "D_" + DoomInfo.BgmNames[(int)bgm].ToString().ToUpper();
            var data = wad.ReadLump(lump);
            reserved = MusicDecoders.Create(data, loop);

            currentBgm = bgm;
        }

        /// <summary>
        /// 合成 frameCount 個取樣框（每框左右各一個 float，-1～1），左右交錯寫進 destination[offset...]。
        /// 還沒選曲時寫靜音。音量照設定的 audio_musicvolume（0～15，同上游：最大音量時放大 2 倍再截在 -1～1）；
        /// 遊戲的音樂音量由宿主另外乘（AudioSource.volume）。
        /// </summary>
        public void Render(float[] destination, int offset, int frameCount)
        {
            if (reserved != current)
            {
                synthesizer.Reset();
                current = reserved;
            }

            var decoder = current;
            if (decoder == null)
            {
                Array.Clear(destination, offset, 2 * frameCount);
                return;
            }

            var a = 2.0F * config.audio_musicvolume / MaxVolume;

            var pos = offset;
            var remaining = frameCount;
            while (remaining > 0)
            {
                var count = Math.Min(remaining, blockLength);

                decoder.RenderWaveform(synthesizer, left.AsSpan(0, count), right.AsSpan(0, count));

                for (var t = 0; t < count; t++)
                {
                    destination[pos++] = Math.Clamp(a * left[t], -1F, 1F);
                    destination[pos++] = Math.Clamp(a * right[t], -1F, 1F);
                }

                remaining -= count;
            }
        }

        /// <summary>整個 destination 都填滿（長度要是偶數：左右交錯）。</summary>
        public void Render(float[] destination)
        {
            Render(destination, 0, destination.Length / 2);
        }

        public int MaxVolume
        {
            get
            {
                return 15;
            }
        }

        public int Volume
        {
            get
            {
                return config.audio_musicvolume;
            }

            set
            {
                config.audio_musicvolume = value;
            }
        }
    }
}
