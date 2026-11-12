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

namespace ProbablyDoom
{
    using System;
    using ManagedDoom;
    using ManagedDoom.Audio;
    using UnityEngine;

    /// <summary>
    /// Doom 的音樂（IMusic，需求.md 決定 15）：SynthMusic（MeltySynth＋TimGM6mb.sf2）邊合成邊播。
    /// 串流的 AudioClip（AudioClip.Create 的 stream＝true＋PCMReaderCallback，44100 Hz 立體聲）掛在一個 loop 的 AudioSource，
    /// Unity 要資料時呼叫 OnRead，裡面叫 SynthMusic.Render。callback 可能在 Unity 的音訊執行緒上跑：不是我們開的執行緒
    /// （遊戲的掃描抓的是 mod 自己開執行緒），SynthMusic 本身只靠 volatile 交接解碼器、合成器只在 Render 裡碰，所以不用鎖。
    /// 音量：Doom 自己的音樂音量（0～15）在 Render 裡乘；遊戲的音樂音量（EmporiumOption.musicVolume）每幀設到 AudioSource.volume；
    /// 主音量是 AudioListener.volume，自動跟著。三個相乘。
    /// 合成出錯時（音訊執行緒上，不能直接停掉 Doom）改成輸出靜音，主執行緒下次 Tick 時記一次 log；Doom 照樣能玩，只是沒有音樂。
    /// </summary>
    sealed class UnityMusic : IMusic
    {
        readonly SynthMusic synth;
        readonly AudioSource source;
        volatile bool failed;
        volatile Exception error;
        bool errorLogged, started;

        internal UnityMusic(SynthMusic synth, GameObject host)
        {
            this.synth = synth;
            source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            // 長度只決定 loop 的週期；串流的 clip 會一直呼叫 OnRead，曲子本身的長度與重複由 SynthMusic 的解碼器管
            source.clip = AudioClip.Create("ProbablyDoom.Music", SynthMusic.SampleRate, SynthMusic.ChannelCount, SynthMusic.SampleRate, true, OnRead);
        }

        void OnRead(float[] data)
        {
            if (failed)
            {
                Array.Clear(data, 0, data.Length);
                return;
            }
            try
            {
                synth.Render(data, 0, data.Length / SynthMusic.ChannelCount);
            }
            catch (Exception e)
            {
                error = e;
                failed = true;
                Array.Clear(data, 0, data.Length);
            }
        }

        /// <summary>每幀（主執行緒）：Doom 有焦點時播、沒焦點時暫停；音量跟遊戲的音樂音量；合成出錯時記一次 log。</summary>
        internal void Tick(bool playing, float gameMusicVolume)
        {
            if (failed)
            {
                if (!errorLogged)
                {
                    errorLogged = true;
                    source.Stop();
                    ProbablyDoomMod.Error($"合成音樂出錯，之後不播音樂（Doom 照樣能玩）：{error}");
                }
                return;
            }

            source.volume = gameMusicVolume;
            if (playing)
            {
                if (!started)
                {
                    source.Play();
                    started = true;
                }
                else if (!source.isPlaying)
                {
                    source.UnPause();
                }
            }
            else if (source.isPlaying)
            {
                source.Pause();
            }
        }

        /// <summary>馬上暫停（失去焦點時，不等下一幀的 Tick）。</summary>
        internal void Pause()
        {
            if (source.isPlaying) source.Pause();
        }

        public void StartMusic(Bgm bgm, bool loop) => synth.StartMusic(bgm, loop);

        public int MaxVolume => synth.MaxVolume;

        public int Volume
        {
            get => synth.Volume;
            set => synth.Volume = value;
        }
    }
}
