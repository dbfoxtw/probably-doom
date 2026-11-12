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
    /// <summary>
    /// Probably Doom：在店裡的平板上玩 Doom（ManagedDoom 的引擎＋MeltySynth）。
    /// 官方格式的 mod（manifest.xml 與 IMod）：遊戲的 mod 載入器先用 id「mod.＋ID」的 Harmony PatchAll，再呼叫 Init、OnEnable。
    /// 0.1.0 只有引擎（engine\），還沒接進遊戲：Init 只寫一行 log，不掛 patch、不建物件，
    /// 用來確認引擎編進 mod 之後過得了遊戲的掃描（ModScanner）。交易面板、按鈕、輸入、聲音是下一段。
    /// </summary>
    public sealed class ProbablyDoomMod : IMod
    {
        /// <summary>manifest.xml 的 ID（也是 Mods\ 底下的資料夾名稱）；ModHelper、ModConfig 都用它找這個 mod。</summary>
        internal const string ModId = "dbfoxtw.probably-doom";

        static ModLog _log;

        public void Init(ModManifest manifest)
        {
            _log = new ModLog(manifest);
            Log("引擎已載入（還沒接進遊戲）");
        }

        public void OnEnable() { }

        public void OnDisable() { }

        // log 寫進存檔資料夾的 Player.log，前綴是 manifest 的 Name（[Probably Doom]:）。
        // 攔截在 Init 之前就掛上了（這一版還沒有），那時 _log 還沒建，寫不出來就算了。
        internal static void Log(string msg) => _log?.Log(msg);
        internal static void Warning(string msg) => _log?.Warning(msg);
        internal static void Error(string msg) => _log?.Error(msg);
    }
}
