//
// Probably Doom 新增（2026-11-12）：WAD 改成宿主讀好交給引擎（檔名＋內容），引擎不碰檔案系統。
//
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
//



using System;

namespace ManagedDoom
{
    /// <summary>
    /// 一個 WAD 檔：檔名與整個檔案的內容。上游的 Wad 自己開 FileStream 讀，遊戲的 mod 掃描不准，
    /// 所以改成宿主（用遊戲的 ModHelper 讀存檔資料夾的 ModData）讀成 byte[] 交進來。
    /// 檔名要照原檔名給（例如 "DOOM2.WAD"、"freedoom1.wad"）：引擎靠檔名（不含副檔名、不分大小寫）判斷遊戲模式
    /// （doom1＝共享版、doom／freedoom1＝Ultimate Doom、doom2／freedoom2＝Doom II、plutonia、tnt＝Final Doom），見 Wad.GetGameMode。
    /// 只給檔名的部分就好，帶資料夾也可以（只取最後一段）。
    /// </summary>
    public sealed class WadFile
    {
        private string fileName;
        private byte[] data;

        public WadFile(string fileName, byte[] data)
        {
            if (fileName == null)
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            this.fileName = fileName;
            this.data = data;
        }

        public string FileName => fileName;
        public byte[] Data => data;
    }
}
