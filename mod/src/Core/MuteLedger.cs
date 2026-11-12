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
using System.Collections.Generic;

namespace ProbablyDoom
{
    /// <summary>
    /// 我們靜音過的來源與各自原本的 mute（GameMusicMute 用，需求.md 決定 27）。不碰 Unity，離線測試測同一份（T 換成假的物件）。
    ///
    /// **同一個來源只記一次、只還原一次**：店裡的背景音樂（StoreSoundPlayer.musicAudioSource）與收音機（AudioManager.audioSourceMusic）
    /// 在目前的遊戲裡是同一個 AudioSource（2026-11-12 UnityPy 讀 level1：都是場景裡唯一的「AudioSourceMusic」）。
    /// 兩個各自記的話，第二個記到的「原本的 mute」是第一個剛設的 true，還原後就一直靜音。
    /// 用參考相等比對（同一個物件），已經在清單裡的不再加；還原時每個只還原一次，不依賴順序。
    /// </summary>
    internal sealed class MuteLedger<T> where T : class
    {
        readonly List<T> _items = new List<T>(2);
        readonly List<bool> _wasMuted = new List<bool>(2);

        /// <summary>記了幾個來源。</summary>
        internal int Count => _items.Count;

        /// <summary>這個來源已經記過了（同一個物件）。</summary>
        internal bool Contains(T item)
        {
            for (var i = 0; i < _items.Count; i++)
                if (ReferenceEquals(_items[i], item)) return true;
            return false;
        }

        /// <summary>
        /// 要靜音一個來源：還沒記過才記下原本的 mute（wasMuted）並回傳 true（呼叫端接著設成靜音）；記過的回傳 false、什麼都不做。
        /// </summary>
        internal bool TryAdd(T item, bool wasMuted)
        {
            if (item == null || Contains(item)) return false;
            _items.Add(item);
            _wasMuted.Add(wasMuted);
            return true;
        }

        /// <summary>
        /// 每個記過的來源還原一次（restore(來源, 原本的 mute)；物件還在不在由呼叫端判斷），之後清空。
        /// 一個還原出錯不影響其他的；全部試過之後再把第一個例外丟出去（呼叫端記 log）。
        /// </summary>
        internal void RestoreAll(Action<T, bool> restore)
        {
            Exception first = null;
            for (var i = 0; i < _items.Count; i++)
            {
                try { restore(_items[i], _wasMuted[i]); }
                catch (Exception e) { if (first == null) first = e; }
            }
            _items.Clear();
            _wasMuted.Clear();
            if (first != null) throw first;
        }
    }
}
