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

    /// <summary>從遊戲讀的狀態（語言、繁中 mod 有沒有在跑、音樂音量），都是直接呼叫，不用反射、不碰存檔。</summary>
    static class GameInfo
    {
        /// <summary>繁中 mod 的 manifest ID。</summary>
        const string ZhHantModId = "dbfoxtw.zh-hant";

        /// <summary>
        /// 繁中 mod 這次有沒有真的跑起來：ModLoader.Mods 裡 ID 相符而且 IsEnabled（Init／OnEnable 成功才是 true）。
        /// 不用 ModLoader.IsModEnabled：它只看玩家勾選的清單，剛勾還沒重開、掃描被拒、Init 失敗時都還是 true。
        /// 不快取：本 mod Init 時繁中 mod 可能還沒載入；只在換語言時查。
        /// </summary>
        internal static bool ZhHantModRunning()
        {
            try { return ModLoader.Mods.Exists(m => m != null && m.IsEnabled && m.Manifest != null && m.Manifest.ID == ZhHantModId); }
            catch { return false; }
        }

        /// <summary>遊戲目前的語系代碼（en、zh…）；Localization 還沒初始化時是空字串。</summary>
        internal static string LocaleCode()
        {
            try { return LocHelper.GetCurrentLocaleCode() ?? ""; }
            catch { return ""; }
        }

        /// <summary>
        /// 遊戲的音樂音量（選項的「音樂音量」，EmporiumOption.musicVolume，0～1，玩家隨時能改）。遊戲沒有 AudioMixer，
        /// 主音量是 AudioListener.volume（自己的 AudioSource 自動跟著），音樂音量要自己乘。還沒有選項的實例時用遊戲的預設 0.5。
        /// </summary>
        internal static float MusicVolume()
        {
            try
            {
                var opt = EmporiumOption.Instance;
                if (opt == null) return 0.5f;
                var v = opt.musicVolume;
                return v < 0 ? 0 : v > 1 ? 1 : v;
            }
            catch { return 0.5f; }
        }
    }
}
