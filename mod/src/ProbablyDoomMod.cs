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
    using HarmonyLib;
    using UnityEngine;

    /// <summary>
    /// Probably Doom：在店裡的平板上玩 Doom（ManagedDoom 的引擎＋MeltySynth）。
    /// 交易面板（點平板開的視窗）上緣多一顆 DOOM 按鈕，按了中間的列表區換成 Doom 的畫面；玩的時候桌上的平板本體也播小畫面。
    /// 官方格式的 mod（manifest.xml 與 IMod）：遊戲的 mod 載入器先用 id「mod.＋ID」的 Harmony PatchAll（攔截都在 Patches.cs），
    /// 再呼叫 Init、OnEnable；結束遊戲或遊戲停掉這個 mod 時呼叫 OnDisable。攔截在 Init 跑完之前一律放行（Ready）。
    /// 每幀的工作交給不隨場景銷毀的 DoomRunner（官方的 IMod 沒有每幀的回呼）。不宣告任何權限：檔案都經過遊戲的 ModHelper。
    /// 程式結構與每個決定的理由見 monorepo 的 mods/probably-doom/docs/設計.md。
    /// </summary>
    public sealed class ProbablyDoomMod : IMod
    {
        /// <summary>manifest.xml 的 ID（也是 Mods\ 底下的資料夾名稱）；ModHelper、ModConfig 都用它找這個 mod。</summary>
        internal const string ModId = "dbfoxtw.probably-doom";
        /// <summary>遊戲替這個 mod PatchAll 用的 Harmony id（ModLoader：「mod.」＋ID）。</summary>
        const string HarmonyId = "mod." + ModId;

        /// <summary>Init 跑完了：遊戲在 Init 之前就掛上攔截，這之前攔截一律放行。</summary>
        internal static bool Ready;

        static ModLog _log;

        public void Init(ModManifest manifest)
        {
            _log = new ModLog(manifest);
            Settings.Init(); // ModConfig 與 ModData 的列表 Init 就能用（路徑只看存檔資料夾）
            var go = new GameObject("ProbablyDoom");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<DoomRunner>();
            LogPatches();
            Log(Settings.Describe());
            Ready = true;
        }

        public void OnEnable() { }

        /// <summary>
        /// 遊戲結束時（ModLoader.OnApplicationQuit），或遊戲在執行中停掉這個 mod 時（ModLoader.StopRunningMod：信任清單封鎖、暫停 mod）呼叫。
        /// 停掉 mod 時遊戲之後只拆 patch、Destroy 我們型別的 MonoBehaviour，聲音、交易面板上加的東西、小畫面都會留著，
        /// 所以交給 DoomRunner.Shutdown 收尾（失去焦點、停聲音、拿掉加的東西、存 doom.cfg、拿掉自己的物件）。
        /// 先把 Ready 關掉：收尾途中與之後，攔截一律放行、每幀的更新不再做事。
        /// </summary>
        public void OnDisable()
        {
            Ready = false;
            try
            {
                var runner = DoomRunner.Instance;
                if (runner != null) runner.Shutdown();
            }
            catch (Exception e) { Warning($"停止時收尾失敗：{e.Message}"); }
        }

        /// <summary>
        /// PatchAll 是遊戲做的，這裡只把結果寫進 log：預期 Patches.Expected 個方法。掛不上的由各類別的 Cleanup 記在 Patches.Failures
        /// （例如遊戲更新後方法改名），少哪一個就只少那一項（交易面板的按鈕或擋某一種快捷鍵），mod 照樣能啟用。
        /// </summary>
        static void LogPatches()
        {
            foreach (var f in Patches.Failures) Warning($"攔截失敗：{f}");
            try
            {
                var names = new List<string>();
                foreach (var m in new Harmony(HarmonyId).GetPatchedMethods())
                    names.Add(m.DeclaringType.Name + "." + m.Name);
                names.Sort(StringComparer.Ordinal);
                if (names.Count != Patches.Expected) Warning($"攔截的方法和預期的不同（預期 {Patches.Expected} 個，掛上 {names.Count} 個）：{string.Join("、", names)}");
                else Log($"已攔截 {names.Count} 個方法：{string.Join("、", names)}");
            }
            catch (Exception e)
            {
                Warning($"列出攔截的方法失敗：{e.Message}");
            }
        }

        // log 寫進存檔資料夾的 Player.log，前綴是 manifest 的 Name（[Probably Doom]:）。
        // 攔截在 Init 之前就掛上了，那時 _log 還沒建，寫不出來就算了。
        internal static void Log(string msg) => _log?.Log(msg);
        internal static void Warning(string msg) => _log?.Warning(msg);
        internal static void Error(string msg) => _log?.Error(msg);

        static bool _debugKnown, _debug;

        /// <summary>
        /// 除錯模式：mod 資料夾裡有 debug 檔（install.bat 裝的開發版）時，每 10 秒把 Doom 的耗時寫進 log、焦點切換也記。
        /// Init 時遊戲還沒把這個 mod 登記進 ModLoader.Mods（Init 回來之後才登記），ModHelper 找不到 mod 資料夾，
        /// 所以延後到第一次需要時才查、查到就記住；查得到之前當作關。
        /// </summary>
        internal static bool Debug
        {
            get
            {
                if (_debugKnown) return _debug;
                try
                {
                    if (ModHelper.GetModFolder(ModId) == null) return false;
                    _debug = ModHelper.ModFileExists(ModId, "debug");
                }
                catch (Exception e)
                {
                    _debug = false;
                    Warning($"讀不到除錯標記，當作關：{e.Message}");
                }
                _debugKnown = true;
                if (_debug) Log("除錯模式（mod 資料夾裡有 debug 檔）：每 10 秒記一次 Doom 的耗時，焦點切換也記");
                return _debug;
            }
        }

        internal static void DebugLog(string msg)
        {
            if (Debug) Log(msg);
        }
    }
}
