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
    /// Doom 有焦點時把遊戲的音樂與環境音靜音，失去焦點就還原（需求.md 決定 27、31：和 Doom 的音樂疊在一起有點惱人）。
    /// 遊戲的選項把這兩種分開：「音樂音量」（Music Volume）與「環境音量」（Background Volume，EmporiumOption.ApplyBackgroundVolume）。
    /// 看四個欄位：
    /// - 音樂：StoreSoundPlayer.Instance.musicAudioSource 與 AudioManager.audioSourceMusic。店裡平常沒有音樂，只有卡帶播放器
    ///   （AudioManager.PlayMusic 換 clip）在用。**這兩個欄位在目前的遊戲裡是同一個 AudioSource**（2026-11-12 UnityPy 讀 level1：
    ///   StoreSoundPlayer 的 musicAudioSource 指到場景裡唯一的「AudioSourceMusic」，AudioManager 的 getter 也是
    ///   GameObject.Find("AudioSourceMusic")）。
    /// - 環境音：StoreSoundPlayer.Instance.ambianceAudioSource（店裡一直在播的背景聲 storeAmbient，PlayStoreAmbient）與
    ///   AudioManager.audioSourceAmbient（GameObject.Find("AudioSourceAmbient")）。第一版只靜音了音樂，店裡的背景聲照樣在播
    ///   （2026-11-13 第二次實機測試第 14 步），所以加上這兩個。
    /// StoreSoundPlayer.Instance 是靜態屬性，不在店面時可能是 null。AudioManager.Instance 的 getter 在沒有實例時會自己
    /// new GameObject、GameObject.Find 好幾個物件，所以一定先看 AudioManager.Exists，是 false 就跳過。
    /// 四個都看（哪個版本改成不同的物件也對），但同一個物件只記一次原本的 mute、只還原一次（MuteLedger）：
    /// 各記各的話，第二個記到的是第一個剛設的 true，還原後就一直靜音。
    ///
    /// 遊戲自己從不碰任何 AudioSource 的 mute（2026-11-13 grep 反編譯：沒有一處 .mute），只改 volume、Play／Pause／Stop、clip，
    /// 所以 mute 不會和遊戲打架；卡帶的換曲靠 isPlaying，靜音不影響。音效（soundAudioSource、AudioManager 的 audioSource1～3）
    /// 不動。我們自己的 Doom 音樂與音效不在這裡面，不受影響。不攔截任何方法。
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
            var player = StoreSoundPlayer.Instance;
            if (player != null)
            {
                Hold(player.musicAudioSource);
                Hold(player.ambianceAudioSource);
            }
            // 不能直接叫 AudioManager.Instance：沒有實例時它會自己建一個（還會 GameObject.Find 好幾個物件）
            if (AudioManager.Exists)
            {
                var manager = AudioManager.Instance;
                if (manager != null)
                {
                    Hold(manager.audioSourceMusic);
                    Hold(manager.audioSourceAmbient);
                }
            }
        }

        /// <summary>還原我們設過的（失去焦點、mod 停掉）：每個來源只還原一次，還原成記下的值。</summary>
        internal void Restore()
        {
            if (_held.Count > 0) _held.RestoreAll(RestoreOne);
        }

        void Hold(AudioSource source)
        {
            if (source == null) return;
            // 同一個物件（StoreSoundPlayer 與 AudioManager 的欄位指到同一個）只在第一次記原本的 mute
            if (_held.TryAdd(source, source.mute)) source.mute = true;
        }
    }
}
