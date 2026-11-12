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
    using UnityEngine;

    /// <summary>
    /// Doom 有焦點時把遊戲的背景音樂靜音，失去焦點就還原（需求.md 決定 27：兩個音樂疊在一起有點惱人）。看兩個欄位：
    /// - 店裡的背景音樂 StoreSoundPlayer.Instance.musicAudioSource（靜態屬性，不在店面時可能是 null）。
    /// - 收音機（卡帶播放器）的 AudioManager.audioSourceMusic。AudioManager.Instance 的 getter 在沒有實例時會自己 new GameObject、
    ///   GameObject.Find 好幾個物件，所以一定先看 AudioManager.Exists，是 false 就跳過。
    /// **這兩個欄位在目前的遊戲裡是同一個 AudioSource**（2026-11-12 UnityPy 讀 level1：StoreSoundPlayer 的 musicAudioSource 指到場景裡
    /// 唯一的「AudioSourceMusic」，AudioManager 的 getter 也是 GameObject.Find("AudioSourceMusic")；卡帶只是換它的 clip）。
    /// 兩個都還是看（哪個版本改成兩個不同的物件也對），但同一個物件只記一次原本的 mute、只還原一次（MuteLedger）：
    /// 各記各的話，第二個記到的是第一個剛設的 true，還原後就一直靜音。
    ///
    /// 遊戲自己只改它們的 volume、Play／Pause／Stop，從不碰 mute（2026-11-12 grep 反編譯確認），所以 mute 不會和遊戲打架；
    /// 卡帶的換曲靠 isPlaying，靜音不影響。我們自己的 Doom 音樂與音效不在這裡面，不受影響。不攔截任何方法。
    /// 要不要靜音由設定 mute_game_music 決定（預設開，決定 31；DoomRunner 傳 有焦點 && 設定 進來，設定每幀讀）。
    /// 有焦點的期間每幀再看一次：中途才出現（或換了一個）的來源也蓋到，已經記過的不重記。
    /// </summary>
    sealed class GameMusicMute
    {
        readonly MuteLedger<AudioSource> _held = new MuteLedger<AudioSource>();

        // 還原一個來源：Unity 物件被 Destroy 之後 == null 是 true（假 null），換場景銷毀了就跳過。不用 ?.
        static readonly Action<AudioSource, bool> RestoreOne = (source, wasMuted) =>
        {
            if (source != null) source.mute = wasMuted;
        };

        /// <summary>每幀（DoomRunner）：mute＝Doom 有焦點。true 時把來源靜音（已經記過的不重記），false 時還原。</summary>
        internal void Update(bool mute)
        {
            if (!mute)
            {
                Restore();
                return;
            }
            Hold(StoreMusic());
            Hold(RadioMusic());
        }

        /// <summary>還原我們設過的（失去焦點、mod 停掉）：每個來源只還原一次，還原成記下的值。</summary>
        internal void Restore()
        {
            if (_held.Count > 0) _held.RestoreAll(RestoreOne);
        }

        static AudioSource StoreMusic()
        {
            var player = StoreSoundPlayer.Instance;
            return player != null ? player.musicAudioSource : null;
        }

        static AudioSource RadioMusic()
        {
            // 不能直接叫 AudioManager.Instance：沒有實例時它會自己建一個（還會 GameObject.Find 好幾個物件）
            if (!AudioManager.Exists) return null;
            var manager = AudioManager.Instance;
            return manager != null ? manager.audioSourceMusic : null;
        }

        void Hold(AudioSource source)
        {
            if (source == null) return;
            // 同一個物件（店裡的與收音機的是同一個）只在第一次記原本的 mute
            if (_held.TryAdd(source, source.mute)) source.mute = true;
        }
    }
}
