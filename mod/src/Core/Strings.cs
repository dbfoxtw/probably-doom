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

namespace ProbablyDoom
{
    internal enum Lang { En, ZhHans, ZhHant }

    /// <summary>
    /// mod 自己在交易面板上顯示的字（DOOM 按鈕的提示框、Doom 畫面區的說明）。不碰 Unity，離線測試測同一份。
    /// 設定視窗的標籤與說明不在這裡，在 mod 資料夾的 Localization\en.csv、zh.csv（遊戲的 ModHelper.GetLocalized）。
    /// 英文只用 ASCII：正式版的英文字型缺很多符號（→、× 會變成空白；根目錄 docs/官方格式寫法.md）。
    /// 繁中字串本身沒有簡體字，繁中 mod 的轉換會直接跳過。
    /// </summary>
    internal sealed class Strings
    {
        /// <summary>DOOM 按鈕的提示框。</summary>
        internal string ButtonTooltip;
        /// <summary>
        /// ModData 裡找不到 WAD（{0}＝ModData 資料夾的路徑，DataPaths.Display：開頭是 %USERPROFILE%）。
        /// 只寫最短的一句（使用者 2026-11-12 定）：去哪裡拿 WAD、支援哪些檔寫在說明頁。
        /// </summary>
        internal string NoWad;
        /// <summary>Doom 暫停中（沒焦點），疊在最後一格畫面上。</summary>
        internal string Paused;
        /// <summary>Doom 出錯停掉了。</summary>
        internal string Error;

        internal static readonly Strings En = new Strings
        {
            ButtonTooltip = "Switch between Doom and the Tablet's usual screen",
            NoWad = "No WAD found. Put one in this folder, then press DOOM again:\n{0}",
            Paused = "Click to continue",
            Error = "Doom stopped because of an error. Details are in Player.log.",
        };

        internal static readonly Strings ZhHans = new Strings
        {
            ButtonTooltip = "在 Doom 和平板电脑原本的画面之间切换",
            NoWad = "找不到 WAD。放一个进这个文件夹，再按一次 DOOM：\n{0}",
            Paused = "点击继续",
            Error = "Doom 出错停止了，详情见 Player.log。",
        };

        internal static readonly Strings ZhHant = new Strings
        {
            ButtonTooltip = "在 Doom 和平板電腦原本的畫面之間切換",
            NoWad = "找不到 WAD。放一個進這個資料夾，再按一次 DOOM：\n{0}",
            Paused = "點一下繼續",
            Error = "Doom 出錯停止了，詳情在 Player.log。",
        };

        internal static Strings For(Lang lang) => lang == Lang.ZhHant ? ZhHant : lang == Lang.ZhHans ? ZhHans : En;

        internal static Strings[] All => new[] { En, ZhHans, ZhHant };

        /// <summary>
        /// 用哪個語言（照我們其他 mod 的 GameState.DetectLanguage）：遊戲只有一個中文語系 zh（簡中），繁體是繁中 mod 在顯示時轉出來的，
        /// 所以遊戲語言是中文時，繁中 mod 在跑就用繁中、沒有就用簡中；其他語言（俄、德、日…）用英文。
        /// 遊戲語言不是中文時不用繁中：英文介面的字型沒有中文字。
        /// </summary>
        internal static Lang PickLang(string localeCode, bool zhHantModRunning)
        {
            if (localeCode != null && localeCode.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                return zhHantModRunning ? Lang.ZhHant : Lang.ZhHans;
            return Lang.En;
        }
    }
}
