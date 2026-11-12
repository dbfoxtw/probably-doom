// Probably Doom
// Copyright (C) 2026 dbfoxtw
// 照 ManagedDoom 上游的 Silk/SilkSound.cs 改寫（Copyright (C) 1993-1996 Id Software, Inc.；Copyright (C) 2019-2020 Nobuaki Tanaka；
// GPL v2 or later）：聲道數、優先序、距離衰減、左右聲道與隨機音高的算法照上游，OpenAL（DrippyAL）換成 Unity 的 AudioSource。
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
    /// Doom 的音效（ISound，需求.md 決定 15）：8 個聲道＋1 個介面聲道，每個聲道一個 AudioSource（2D，左右聲道用 panStereo）。
    /// WAD 的 DS* lump 用 DmxSound 解成 AudioClip（建構時全部建好、之後重用）。
    /// 音量照 Doom 自己的音效音量（選單的 Sound Volume，0～15）；遊戲的主音量是 AudioListener.volume，AudioSource 自動跟著。
    /// 上游在 Update 開頭擋掉 0.01 秒內的重複呼叫（給 -timedemo 用）；這裡每個 tic 才呼叫一次，不需要。
    /// </summary>
    sealed class UnitySound : ISound
    {
        static readonly int channelCount = 8;

        static readonly float fastDecay = (float)Math.Pow(0.5, 1.0 / (35 / 5));
        static readonly float slowDecay = (float)Math.Pow(0.5, 1.0 / 35);

        static readonly float clipDist = 1200;
        static readonly float closeDist = 160;
        static readonly float attenuator = clipDist - closeDist;

        readonly Config config;

        readonly AudioClip[] buffers;
        readonly float[] amplitudes;

        readonly DoomRandom random;

        readonly AudioSource[] channels;
        readonly ChannelInfo[] infos;
        /// <summary>被 Doom 的 Pause（選單打開）暫停的聲道，Resume 時繼續。</summary>
        readonly bool[] doomPaused;
        /// <summary>被宿主暫停（Doom 失去焦點）的聲道，HostResume 時繼續。</summary>
        readonly bool[] hostPaused;

        readonly AudioSource uiChannel;
        bool uiHostPaused;
        Sfx uiReserved;

        Mobj listener;

        float masterVolumeDecay;

        /// <summary>有幾個音效建成 AudioClip（log 用）。</summary>
        internal int ClipCount { get; }

        internal UnitySound(Config config, GameContent content, GameObject host)
        {
            this.config = config;

            config.audio_soundvolume = Math.Clamp(config.audio_soundvolume, 0, MaxVolume);

            buffers = new AudioClip[DoomInfo.SfxNames.Length];
            amplitudes = new float[DoomInfo.SfxNames.Length];

            if (config.audio_randompitch)
            {
                random = new DoomRandom();
            }

            for (var i = 0; i < DoomInfo.SfxNames.Length; i++)
            {
                var name = "DS" + DoomInfo.SfxNames[i].ToString().ToUpper();
                var dmx = DmxSound.FromWad(content.Wad, name);
                if (dmx == null || dmx.SampleRate <= 0)
                {
                    // 沒有這個音效，或檔頭的取樣率是 0（AudioClip.Create 會丟例外，整個 Doom 就建不起來）：這個音效不播
                    continue;
                }

                var clip = AudioClip.Create("ProbablyDoom." + name, dmx.Samples.Length, 1, dmx.SampleRate, false);
                clip.SetData(dmx.ToFloat(), 0);
                buffers[i] = clip;
                amplitudes[i] = dmx.Amplitude;
                ClipCount++;
            }

            channels = new AudioSource[channelCount];
            infos = new ChannelInfo[channelCount];
            doomPaused = new bool[channelCount];
            hostPaused = new bool[channelCount];
            for (var i = 0; i < channels.Length; i++)
            {
                channels[i] = NewSource(host);
                infos[i] = new ChannelInfo();
            }

            uiChannel = NewSource(host);
            uiReserved = Sfx.NONE;

            masterVolumeDecay = (float)config.audio_soundvolume / MaxVolume;
        }

        /// <summary>2D 的 AudioSource（不隨距離變小、不受 3D 音效設定影響）；左右聲道用 panStereo 自己算。</summary>
        static AudioSource NewSource(GameObject host)
        {
            var s = host.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = false;
            s.spatialBlend = 0f;
            s.dopplerLevel = 0f;
            return s;
        }

        /// <summary>聲道停了沒（上游的 PlaybackState.Stopped）：沒在播、也不是被暫停的。</summary>
        bool Stopped(int i) => !channels[i].isPlaying && !doomPaused[i] && !hostPaused[i];

        public void SetListener(Mobj listener)
        {
            this.listener = listener;
        }

        public void Update()
        {
            for (var i = 0; i < infos.Length; i++)
            {
                var info = infos[i];
                var channel = channels[i];

                if (info.Playing != Sfx.NONE)
                {
                    if (!Stopped(i))
                    {
                        if (info.Type == SfxType.Diffuse)
                        {
                            info.Priority *= slowDecay;
                        }
                        else
                        {
                            info.Priority *= fastDecay;
                        }
                        SetParam(channel, info);
                    }
                    else
                    {
                        info.Playing = Sfx.NONE;
                        if (info.Reserved == Sfx.NONE)
                        {
                            info.Source = null;
                        }
                    }
                }

                if (info.Reserved != Sfx.NONE)
                {
                    if (info.Playing != Sfx.NONE)
                    {
                        channel.Stop();
                    }

                    doomPaused[i] = false;
                    hostPaused[i] = false;
                    channel.clip = buffers[(int)info.Reserved];
                    SetParam(channel, info);
                    channel.pitch = GetPitch(info.Type, info.Reserved);
                    channel.Play();
                    info.Playing = info.Reserved;
                    info.Reserved = Sfx.NONE;
                }
            }

            if (uiReserved != Sfx.NONE)
            {
                if (uiChannel.isPlaying)
                {
                    uiChannel.Stop();
                }
                uiHostPaused = false;
                uiChannel.panStereo = 0f;
                uiChannel.volume = masterVolumeDecay;
                uiChannel.pitch = 1f;
                uiChannel.clip = buffers[(int)uiReserved];
                uiChannel.Play();
                uiReserved = Sfx.NONE;
            }
        }

        public void StartSound(Sfx sfx)
        {
            if (buffers[(int)sfx] == null)
            {
                return;
            }

            uiReserved = sfx;
        }

        public void StartSound(Mobj mobj, Sfx sfx, SfxType type)
        {
            StartSound(mobj, sfx, type, 100);
        }

        public void StartSound(Mobj mobj, Sfx sfx, SfxType type, int volume)
        {
            if (buffers[(int)sfx] == null)
            {
                return;
            }

            // 上游假設這時已經有 listener（進關卡時設定）；保險起見，還沒有時當成近距離的聲音，不讓 null 例外停掉整個 Doom
            var dist = 0f;
            if (listener != null && mobj != null)
            {
                var x = (mobj.X - listener.X).ToFloat();
                var y = (mobj.Y - listener.Y).ToFloat();
                dist = MathF.Sqrt(x * x + y * y);
            }

            float priority;
            if (type == SfxType.Diffuse)
            {
                priority = volume;
            }
            else
            {
                priority = amplitudes[(int)sfx] * GetDistanceDecay(dist) * volume;
            }

            for (var i = 0; i < infos.Length; i++)
            {
                var info = infos[i];
                if (info.Source == mobj && info.Type == type)
                {
                    info.Reserved = sfx;
                    info.Priority = priority;
                    info.Volume = volume;
                    return;
                }
            }

            for (var i = 0; i < infos.Length; i++)
            {
                var info = infos[i];
                if (info.Reserved == Sfx.NONE && info.Playing == Sfx.NONE)
                {
                    info.Reserved = sfx;
                    info.Priority = priority;
                    info.Source = mobj;
                    info.Type = type;
                    info.Volume = volume;
                    return;
                }
            }

            var minPriority = float.MaxValue;
            var minChannel = -1;
            for (var i = 0; i < infos.Length; i++)
            {
                var info = infos[i];
                if (info.Priority < minPriority)
                {
                    minPriority = info.Priority;
                    minChannel = i;
                }
            }
            if (minChannel >= 0 && priority >= minPriority)
            {
                var info = infos[minChannel];
                info.Reserved = sfx;
                info.Priority = priority;
                info.Source = mobj;
                info.Type = type;
                info.Volume = volume;
            }
        }

        public void StopSound(Mobj mobj)
        {
            for (var i = 0; i < infos.Length; i++)
            {
                var info = infos[i];
                if (info.Source == mobj)
                {
                    info.LastX = info.Source.X;
                    info.LastY = info.Source.Y;
                    info.Source = null;
                    info.Volume /= 5;
                }
            }
        }

        public void Reset()
        {
            if (random != null)
            {
                random.Clear();
            }

            for (var i = 0; i < infos.Length; i++)
            {
                channels[i].Stop();
                doomPaused[i] = false;
                hostPaused[i] = false;
                infos[i].Clear();
            }

            listener = null;
        }

        /// <summary>Doom 自己的暫停（例如選單打開）：同上游，只停還剩 0.2 秒以上的聲音，短的讓它播完。</summary>
        public void Pause()
        {
            for (var i = 0; i < infos.Length; i++)
            {
                var channel = channels[i];

                if (channel.isPlaying && channel.clip != null && channel.clip.length - channel.time > 0.2f)
                {
                    channel.Pause();
                    doomPaused[i] = true;
                }
            }
        }

        public void Resume()
        {
            for (var i = 0; i < infos.Length; i++)
            {
                if (doomPaused[i])
                {
                    doomPaused[i] = false;
                    if (!hostPaused[i]) channels[i].UnPause();
                }
            }
        }

        /// <summary>Doom 失去焦點（宿主不再呼叫 Update）：正在播的全部暫停，包括介面聲道（需求.md 決定 10「音效停」）。</summary>
        internal void HostPause()
        {
            for (var i = 0; i < channels.Length; i++)
            {
                if (channels[i].isPlaying)
                {
                    channels[i].Pause();
                    hostPaused[i] = true;
                }
            }
            if (uiChannel.isPlaying)
            {
                uiChannel.Pause();
                uiHostPaused = true;
            }
        }

        /// <summary>重新取得焦點：HostPause 停掉的接著播（被 Doom 自己暫停的照舊等 Resume）。</summary>
        internal void HostResume()
        {
            for (var i = 0; i < channels.Length; i++)
            {
                if (hostPaused[i])
                {
                    hostPaused[i] = false;
                    if (!doomPaused[i]) channels[i].UnPause();
                }
            }
            if (uiHostPaused)
            {
                uiHostPaused = false;
                uiChannel.UnPause();
            }
        }

        void SetParam(AudioSource sound, ChannelInfo info)
        {
            if (info.Type == SfxType.Diffuse || listener == null)
            {
                sound.panStereo = 0f;
                sound.volume = 0.01F * masterVolumeDecay * info.Volume;
            }
            else
            {
                Fixed sourceX;
                Fixed sourceY;
                if (info.Source == null)
                {
                    sourceX = info.LastX;
                    sourceY = info.LastY;
                }
                else
                {
                    sourceX = info.Source.X;
                    sourceY = info.Source.Y;
                }

                var x = (sourceX - listener.X).ToFloat();
                var y = (sourceY - listener.Y).ToFloat();

                if (Math.Abs(x) < 16 && Math.Abs(y) < 16)
                {
                    sound.panStereo = 0f;
                    sound.volume = 0.01F * masterVolumeDecay * info.Volume;
                }
                else
                {
                    var dist = MathF.Sqrt(x * x + y * y);
                    var angle = MathF.Atan2(y, x) - (float)listener.Angle.ToRadian();
                    // 上游把聲源放在聽者周圍半徑 1 的圓上（OpenAL 的座標 (-sin, 0, -cos)，聽者面向 -Z、右邊是 +X），
                    // 左右只看 X：-sin(angle)，正的是右邊，同 Unity 的 panStereo
                    sound.panStereo = Mathf.Clamp(-MathF.Sin(angle), -1f, 1f);
                    sound.volume = 0.01F * masterVolumeDecay * GetDistanceDecay(dist) * info.Volume;
                }
            }
        }

        float GetDistanceDecay(float dist)
        {
            if (dist < closeDist)
            {
                return 1F;
            }
            else
            {
                return Math.Max((clipDist - dist) / attenuator, 0F);
            }
        }

        float GetPitch(SfxType type, Sfx sfx)
        {
            if (random != null)
            {
                if (sfx == Sfx.ITEMUP || sfx == Sfx.TINK || sfx == Sfx.RADIO)
                {
                    return 1.0F;
                }
                else if (type == SfxType.Voice)
                {
                    return 1.0F + 0.075F * (random.Next() - 128) / 128;
                }
                else
                {
                    return 1.0F + 0.025F * (random.Next() - 128) / 128;
                }
            }
            else
            {
                return 1.0F;
            }
        }

        public int MaxVolume => 15;

        public int Volume
        {
            get
            {
                return config.audio_soundvolume;
            }

            set
            {
                config.audio_soundvolume = value;
                masterVolumeDecay = (float)config.audio_soundvolume / MaxVolume;
            }
        }

        sealed class ChannelInfo
        {
            public Sfx Reserved;
            public Sfx Playing;
            public float Priority;

            public Mobj Source;
            public SfxType Type;
            public int Volume;
            public Fixed LastX;
            public Fixed LastY;

            public void Clear()
            {
                Reserved = Sfx.NONE;
                Playing = Sfx.NONE;
                Priority = 0;
                Source = null;
                Type = 0;
                Volume = 0;
                LastX = Fixed.Zero;
                LastY = Fixed.Zero;
            }
        }
    }
}
