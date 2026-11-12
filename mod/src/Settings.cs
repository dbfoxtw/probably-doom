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
    using System.Collections.Generic;

    /// <summary>
    /// mod 設定（需求.md 決定 5、7、13、31）：遊戲官方的 ModConfig（存在存檔資料夾的 ModConfigs\dbfoxtw.probably-doom.json，
    /// 主選單「模組」畫面的齒輪打開設定視窗）。四項照定義的順序排：
    /// 1. wad：放了好幾個 WAD 時玩哪一個（Auto 或檔名；選項是 Init 時在 ModData 找到的 IWAD，新放的要重開遊戲才出現）。
    /// 2. resolution：640x400（預設）或 320x200（用 ASCII 的 x：英文介面的字型可能沒有 ×）。
    /// 3. tablet_screen：平板本體也播小畫面（預設開）。
    /// 4. mute_game_music：玩的時候遊戲的音樂（卡帶播放器）與環境音（店裡的背景聲）靜音（預設開，決定 31；鍵名沿用第一版的 music）。
    /// wad 與 resolution 是 requiresRestart：Doom 整個遊戲執行期間只建一次（第一次切到 Doom 時），之後換 WAD、換解析度都要重開；
    /// tablet_screen 與 mute_game_music 每幀讀，按套用就生效（玩家從主選單改，回到店裡時就是新的值；
    /// 開發工具在店裡打開同一個設定視窗時也是馬上生效）。
    /// 標籤與說明在 mod 的 Localization\en.csv、zh.csv（ModHelper.GetLocalized；遊戲在 Init 之後才讀這些檔，所以傳函式，
    /// 打開設定視窗時才查）。繁中由繁中 mod 在顯示時把簡中轉過去（我們其他 mod 的設定也這樣）。
    /// </summary>
    static class Settings
    {
        /// <summary>
        /// 設定視窗裡的段落標題。遊戲直接顯示這個字串（不經過翻譯），段落又只能在 Init 時定（那時 mod 的 Localization 還沒載入），
        /// 所以寫固定的英文（一鍵收水的慣例）。用 mod 的名字（使用者 2026-11-12 定，原本是 Doom；2026-11-13 改成大寫的 DOOM）。段落不存進設定檔，改名不影響玩家存的設定。
        /// </summary>
        const string Section = "Probably DOOM";

        internal const string HighRes = "640x400";
        internal const string LowRes = "320x200";

        static ConfigEntry<string> _wad, _resolution;
        static ConfigEntry<bool> _tabletScreen, _muteGameMusic;

        /// <summary>Init 時在 ModData 找到的 IWAD（照「自動」的順序）；只用在 log 與設定的選項。</summary>
        internal static List<string> WadsAtStart = new List<string>();

        internal static void Init()
        {
            try { WadsAtStart = WadPicker.FindIwads(ModHelper.ListModDataFiles(ProbablyDoomMod.ModId)); }
            catch (Exception e) { ProbablyDoomMod.Warning($"列 ModData 的檔案失敗（WAD 的選項只有 Auto）：{e.Message}"); }

            var choices = new List<string> { WadPicker.Auto };
            choices.AddRange(WadsAtStart);

            var cfg = ModConfig.For(ProbablyDoomMod.ModId);
            _wad = cfg.Define("wad", WadPicker.Auto, Section, () => Loc("cfg_wad"), choices.ToArray(), () => Loc("cfg_wad_tip"),
                requiresRestart: true);
            _resolution = cfg.Define("resolution", HighRes, Section, () => Loc("cfg_resolution"), new[] { HighRes, LowRes },
                () => Loc("cfg_resolution_tip"), requiresRestart: true);
            _tabletScreen = cfg.Define("tablet_screen", true, Section, () => Loc("cfg_tablet_screen"), () => Loc("cfg_tablet_screen_tip"));
            _muteGameMusic = cfg.Define("mute_game_music", true, Section, () => Loc("cfg_mute_game_music"),
                () => Loc("cfg_mute_game_music_tip"));
        }

        static string Loc(string key) => ModHelper.GetLocalized(ProbablyDoomMod.ModId, key);

        /// <summary>wad 設定的值（Auto 或檔名）。還沒讀設定時用 Auto。</summary>
        internal static string Wad => _wad != null ? _wad.Value : WadPicker.Auto;

        /// <summary>640x400 以外（只有 320x200）都當高解析度。</summary>
        internal static bool HighResolution => _resolution == null || _resolution.Value != LowRes;

        /// <summary>平板本體的小畫面（決定 7）。每幀讀。</summary>
        internal static bool TabletScreen => _tabletScreen == null || _tabletScreen.Value;

        /// <summary>玩的時候遊戲的音樂靜音（決定 27、31）。每幀讀。</summary>
        internal static bool MuteGameMusic => _muteGameMusic == null || _muteGameMusic.Value;

        internal static string Describe()
        {
            var wads = WadsAtStart.Count == 0 ? "ModData 裡沒有 WAD" : "ModData 裡的 WAD：" + string.Join("、", WadsAtStart);
            return $"{wads}；設定：WAD {Wad}、解析度 {(HighResolution ? HighRes : LowRes)}、平板本體的小畫面 {(TabletScreen ? "開" : "關")}、" +
                $"遊戲音樂與環境音靜音 {(MuteGameMusic ? "開" : "關")}";
        }
    }
}
