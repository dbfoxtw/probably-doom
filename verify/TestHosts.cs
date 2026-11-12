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

using System.Collections.Generic;
using ManagedDoom;
using ManagedDoom.Audio;
using ManagedDoom.UserInput;

namespace ProbablyDoom.Verify
{
    /// <summary>存在記憶體裡的 ISaveStore：離線測試用（遊戲裡由宿主用 ModHelper 寫 ModData）。</summary>
    internal sealed class MemorySaveStore : ISaveStore
    {
        public readonly Dictionary<int, byte[]> Slots = new Dictionary<int, byte[]>();
        public int Reads, Writes;

        public byte[] Read(int slot)
        {
            Reads++;
            return Slots.TryGetValue(slot, out var data) ? data : null;
        }

        public void Write(int slot, byte[] data)
        {
            Writes++;
            Slots[slot] = data;
        }
    }

    /// <summary>不出聲、只記下引擎要了哪些音效：確認引擎有透過 ISound 叫聲音。</summary>
    internal sealed class RecordingSound : ISound
    {
        public int StartCount;
        public readonly HashSet<Sfx> Seen = new HashSet<Sfx>();
        int _volume = 8;

        public void SetListener(Mobj listener) { }
        public void Update() { }
        public void StartSound(Sfx sfx) { StartCount++; Seen.Add(sfx); }
        public void StartSound(Mobj mobj, Sfx sfx, SfxType type) { StartCount++; Seen.Add(sfx); }
        public void StartSound(Mobj mobj, Sfx sfx, SfxType type, int volume) { StartCount++; Seen.Add(sfx); }
        public void StopSound(Mobj mobj) { }
        public void Reset() { }
        public void Pause() { }
        public void Resume() { }
        public int MaxVolume => 15;
        public int Volume { get => _volume; set => _volume = value; }
    }

    /// <summary>不出聲、只記下引擎要放哪首音樂。</summary>
    internal sealed class RecordingMusic : IMusic
    {
        public readonly List<Bgm> Started = new List<Bgm>();
        int _volume = 8;

        public void StartMusic(Bgm bgm, bool loop) { Started.Add(bgm); }
        public int MaxVolume => 15;
        public int Volume { get => _volume; set => _volume = value; }
    }

    /// <summary>照程式設定的值產生 TicCmd（代替鍵盤）：讓存讀檔測試裡的玩家真的會走動、轉向。</summary>
    internal sealed class ScriptedInput : IUserInput
    {
        public sbyte Forward;
        public short Turn;

        public void BuildTicCmd(TicCmd cmd)
        {
            cmd.Clear();
            cmd.ForwardMove = Forward;
            cmd.AngleTurn = Turn;
        }

        public void Reset() { }
        public void GrabMouse() { }
        public void ReleaseMouse() { }
        public int MaxMouseSensitivity => 9;
        public int MouseSensitivity { get; set; }
    }
}
