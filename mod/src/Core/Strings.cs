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
    /// <summary>
    /// mod 自己在交易面板上顯示的字（DOOM 按鈕的提示框、Doom 畫面區的說明）。不碰 Unity，離線測試測同一份。
    /// 字都在 mod 的 Localization\*.csv（panel_ 開頭的 key，需求.md 決定 34），由 Loc.Get 挑語言（DoomRunner.UpdateLanguage 傳進來）；
    /// 這裡只決定哪個欄位用哪個 key、說明文字在哪些字後面換行。設定視窗的標籤與說明也在同一批 csv（cfg_ 開頭，Settings.cs）。
    /// 英文只用 ASCII：正式版的英文字型缺很多符號（→、× 會變成空白；根目錄 docs/官方格式寫法.md）。
    /// </summary>
    internal sealed class Strings
    {
        /// <summary>DOOM 按鈕的提示框。</summary>
        internal string ButtonTooltip;
        /// <summary>
        /// ModData 裡找不到 WAD：兩句，中間是換行（'\n'），底下的路徑框放 ModData 資料夾（需求.md 決定 29）。
        /// 只寫最短的話（使用者 2026-11-12 定）：去哪裡拿 WAD、支援哪些檔寫在說明頁。
        /// </summary>
        internal string NoWad;
        /// <summary>Doom 暫停中（沒焦點），疊在最後一格畫面上。</summary>
        internal string Paused;
        /// <summary>Doom 出錯停掉了：兩句，中間是換行，底下的路徑框放 Player.log 的位置（決定 29）。</summary>
        internal string Error;
        /// <summary>路徑框下面的按鈕：按了把路徑寫進剪貼簿。</summary>
        internal string CopyPath;
        /// <summary>按了「複製路徑」之後按鈕的字（2 秒）。</summary>
        internal string Copied;
        /// <summary>
        /// 說明文字只在這些字後面換行（TextMarkup.Sentences 用 &lt;nobr&gt; 包每一段）；空字串就照原樣，TMP 照空白換。
        /// 見 <see cref="BreakAfterFor"/>。
        /// </summary>
        internal string BreakAfter = "";

        /// <summary>中文：只在「，」後面換行（TMP 的中文每個字後面都能換，會把「DOOM：」「詳情」拆開）。</summary>
        internal const string ZhBreaks = "，";
        /// <summary>日文：只在「、」「。」後面換行（日文和中文一樣沒有空白，TMP 每個字後面都能換）。</summary>
        internal const string JaBreaks = "、。";

        /// <summary>
        /// 照遊戲的語系代碼建一份：get 取 csv 的字（mod 裡是 Loc.Get，離線測試傳假的），localeCode 定說明文字的換行方式。
        /// longest（最長字串模式）拿到的是西文，不照中文、日文的標點換行。
        /// </summary>
        internal static Strings Build(Func<string, string> get, string localeCode, bool longest) => new Strings
        {
            ButtonTooltip = get("panel_button_tip"),
            NoWad = get("panel_no_wad"),
            Paused = get("panel_paused"),
            Error = get("panel_error"),
            CopyPath = get("panel_copy_path"),
            Copied = get("panel_copied"),
            BreakAfter = BreakAfterFor(localeCode, longest),
        };

        /// <summary>
        /// 說明文字在哪些字後面換行：zh 開頭（簡中，繁中 mod 有跑時是手寫的繁中，語系代碼一樣是 zh）→「，」；ja →「、」「。」；
        /// 其他語言、還沒初始化（空字串）、最長字串模式 → 不處理（西文照空白換行）。
        /// </summary>
        internal static string BreakAfterFor(string localeCode, bool longest)
        {
            if (longest || string.IsNullOrEmpty(localeCode)) return "";
            if (localeCode.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return ZhBreaks;
            if (localeCode.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return JaBreaks;
            return "";
        }
    }
}
