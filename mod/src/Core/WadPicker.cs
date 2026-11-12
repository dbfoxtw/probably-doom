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
    /// 從 ModData 裡的檔案挑要玩的 WAD（需求.md 決定 12、13）。不碰 Unity 與遊戲：檔案清單由呼叫端給
    /// （ModHelper.ListModDataFiles 的結果，相對路徑、斜線是 /），離線測試（verify\）測同一份。
    /// </summary>
    internal static class WadPicker
    {
        /// <summary>
        /// mod 設定 wad 的「自動」。設定視窗的下拉選項照字面顯示（遊戲不翻譯選項），存進設定檔的也是這個字串，
        /// 所以三個語言都是 Auto（中文在說明欄寫「Auto（自動）」）。
        /// </summary>
        internal const string Auto = "Auto";

        /// <summary>
        /// 認得的 IWAD（檔名不含副檔名、小寫），照「自動」的優先順序排：DOOM2 > DOOM（含 doomu）> DOOM1 > freedoom2 > freedoom1 > plutonia > tnt
        /// （需求.md 決定 13；Final Doom 兩個排最後是交辦時定的）。
        /// 引擎靠檔名判斷遊戲模式（Wad.GetGameMode：doom1、doom、doom2、freedoom1、freedoom2、plutonia、tnt），
        /// doomu（有些版本的 Ultimate Doom 叫這個名字）引擎不認得，交給引擎時換成 DOOM.WAD（<see cref="EngineFileName"/>）。
        /// </summary>
        static readonly string[] Priority = { "doom2", "doom", "doomu", "doom1", "freedoom2", "freedoom1", "plutonia", "tnt" };

        /// <summary>這個檔名（可以帶資料夾）在「自動」的順序是第幾（0 最優先）；不是認得的 IWAD 時回傳 -1。副檔名要是 .wad（不分大小寫）。</summary>
        internal static int Rank(string path)
        {
            if (string.IsNullOrEmpty(path)) return -1;
            var name = FileName(path);
            if (!name.EndsWith(".wad", StringComparison.OrdinalIgnoreCase)) return -1;
            return Array.IndexOf(Priority, name.Substring(0, name.Length - 4).ToLowerInvariant());
        }

        /// <summary>
        /// ModData 的檔案清單裡，放在最上層（不在子資料夾裡，例如不算 saves/ 底下的）的 IWAD，照「自動」的順序排。
        /// 只看最上層：玩家照說明把 WAD 放進這個資料夾；子資料夾是 mod 自己的存檔。
        /// </summary>
        internal static List<string> FindIwads(IEnumerable<string> modDataFiles)
        {
            var found = new List<string>();
            if (modDataFiles == null) return found;
            foreach (var f in modDataFiles)
            {
                if (string.IsNullOrEmpty(f) || f.IndexOf('/') >= 0 || f.IndexOf('\\') >= 0) continue;
                if (Rank(f) >= 0) found.Add(f);
            }
            found.Sort((a, b) =>
            {
                var r = Rank(a).CompareTo(Rank(b));
                return r != 0 ? r : string.CompareOrdinal(a, b);
            });
            return found;
        }

        /// <summary>
        /// 照設定挑一個：設定是 Auto（或空的）時取順序最前面的；設定是檔名時找同名的（不分大小寫），
        /// 找不到（檔案拿走了）就退回自動，fellBack 設成 true（呼叫端記一行 log）。一個都沒有時回傳 null。
        /// </summary>
        internal static string Choose(IReadOnlyList<string> iwads, string setting, out bool fellBack)
        {
            fellBack = false;
            if (iwads == null || iwads.Count == 0) return null;
            if (!string.IsNullOrEmpty(setting) && !string.Equals(setting, Auto, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var w in iwads)
                    if (string.Equals(w, setting, StringComparison.OrdinalIgnoreCase)) return w;
                fellBack = true;
            }
            return iwads[0];
        }

        /// <summary>
        /// 交給引擎的檔名（WadFile 的 FileName）：引擎靠檔名判斷遊戲模式，doomu.wad 不在它的清單裡，換成 DOOM.WAD
        /// （內容就是 Ultimate Doom，和 DOOM.WAD 同一個遊戲模式）；其他照原檔名。
        /// </summary>
        internal static string EngineFileName(string path)
        {
            var name = FileName(path);
            return string.Equals(name, "doomu.wad", StringComparison.OrdinalIgnoreCase) ? "DOOM.WAD" : name;
        }

        /// <summary>每個 WAD 一個存檔資料夾的名稱：檔名不含副檔名、小寫（DOOM2.WAD → doom2）。</summary>
        internal static string SaveKey(string path)
        {
            var name = FileName(path);
            var dot = name.LastIndexOf('.');
            return (dot > 0 ? name.Substring(0, dot) : name).ToLowerInvariant();
        }

        static string FileName(string path)
        {
            var i = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
            return i >= 0 ? path.Substring(i + 1) : path;
        }
    }

    /// <summary>ModData 裡的路徑（需求.md 決定 11、21）。都是相對於 ModData\dbfoxtw_probably-doom\、用 / 分隔（ModHelper 的寫法）。</summary>
    internal static class DataPaths
    {
        /// <summary>Doom 自己的設定（按鍵、音量…，Config.ToText 的格式），玩家可以關著遊戲直接改。</summary>
        internal const string ConfigFile = "doom.cfg";

        /// <summary>Doom 的存檔：每個 WAD 分開（不同 WAD 的存檔互相讀不了），格子 0～5 同原版的 doomsav0.dsg～doomsav5.dsg。</summary>
        internal static string SaveSlot(string wadPath, int slot) => $"saves/{WadPicker.SaveKey(wadPath)}/doomsav{slot}.dsg";

        /// <summary>遊戲的存檔資料夾（Application.persistentDataPath）在 Windows 上相對於使用者資料夾的位置：app.info 的公司名與產品名。</summary>
        internal const string SaveFolderUnderProfile = @"\AppData\LocalLow\Questing Goose Studio\Probably Stolen";

        /// <summary>
        /// 顯示給玩家看的資料夾路徑（沒有 WAD 時的說明）：
        /// - Application.persistentDataPath 在 Windows 用 /，Path.Combine 接上的部分用 \，混在一起不好看，統一成 \。
        /// - 開頭是「使用者資料夾\AppData\LocalLow\Questing Goose Studio\Probably Stolen」時，使用者資料夾換成 %USERPROFILE%：
        ///   截圖時不露出 Windows 的使用者名稱，整串貼進檔案總管的網址列也打得開（使用者 2026-11-12 定）。
        /// - 不是這個開頭（別的平台、資料夾被搬走或重新導向、拿不到使用者資料夾）時照原樣顯示完整路徑：換了反而指到錯的地方。
        /// userProfile＝Environment.GetFolderPath(UserProfile)（呼叫端給，離線測試不碰真的環境）。
        /// </summary>
        internal static string Display(string folder, string userProfile)
        {
            if (string.IsNullOrEmpty(folder)) return "";
            var path = folder.Replace('/', '\\');
            if (string.IsNullOrEmpty(userProfile)) return path;
            var home = userProfile.Replace('/', '\\').TrimEnd('\\');
            if (home.Length == 0) return path;
            var expected = home + SaveFolderUnderProfile;
            if (!path.StartsWith(expected, StringComparison.OrdinalIgnoreCase)) return path;
            if (path.Length != expected.Length && path[expected.Length] != '\\') return path; // 例如 …\Probably Stolen Demo
            return "%USERPROFILE%" + path.Substring(home.Length);
        }
    }
}
