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
    using ManagedDoom;

    /// <summary>
    /// Doom 自己的存檔（選單的 Save Game／Load Game，需求.md 決定 11）：存檔資料夾的
    /// ModData\dbfoxtw_probably-doom\saves\&lt;WAD 檔名小寫不含副檔名&gt;\doomsav&lt;格&gt;.dsg（DataPaths.SaveSlot），每個 WAD 分開。
    /// 經過遊戲的 ModHelper（不宣告權限；Steam 雲端只同步 .es3，這些檔不上雲端），完全不碰遊戲的存檔。
    /// 寫不進去時 ModHelper 丟例外，Doom.Update 會把它往外丟，DoomRunner 接住後停掉 Doom、記 log（不丟回遊戲）。
    /// </summary>
    sealed class ModDataSaveStore : ISaveStore
    {
        readonly string _wad;

        internal ModDataSaveStore(string wadFileName)
        {
            _wad = wadFileName;
        }

        public byte[] Read(int slot)
        {
            var path = DataPaths.SaveSlot(_wad, slot);
            return ModHelper.ModDataFileExists(ProbablyDoomMod.ModId, path) ? ModHelper.ReadModDataFileBytes(ProbablyDoomMod.ModId, path) : null;
        }

        public void Write(int slot, byte[] data)
        {
            var path = DataPaths.SaveSlot(_wad, slot);
            ModHelper.WriteModDataFileBytes(ProbablyDoomMod.ModId, path, data);
            ProbablyDoomMod.DebugLog($"Doom 存檔：{path}（{data.Length:N0} bytes）");
        }
    }
}
