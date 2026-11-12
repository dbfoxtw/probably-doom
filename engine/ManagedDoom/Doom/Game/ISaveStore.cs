//
// Probably Doom 新增（2026-11-12）：存讀檔改成經過宿主實作的介面，引擎不碰檔案系統。
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



namespace ManagedDoom
{
    /// <summary>
    /// Doom 自己的存檔（選單的 Save Game／Load Game，六格）放在哪裡由宿主決定。
    /// 上游直接讀寫執行檔旁的 doomsav0.dsg～doomsav5.dsg；遊戲的 mod 掃描不准 mod 自己開檔案，
    /// 所以改成宿主實作這個介面（Probably Doom 用遊戲的 ModHelper 寫存檔資料夾的 ModData）。
    /// 內容就是原版的 .dsg 格式：開頭 24 bytes 是存檔的說明文字（SaveAndLoad.DescriptionSize）。
    /// 呼叫都在 Doom.Update() 裡（遊戲的主執行緒）。
    /// </summary>
    public interface ISaveStore
    {
        /// <summary>第 slot 格（0 起算，共 SaveSlots.SlotCount 格）的存檔內容；那一格沒有存檔時回傳 null。</summary>
        byte[] Read(int slot);

        /// <summary>把存檔寫進第 slot 格（整個取代）。寫不進去時丟例外，Doom.Update() 會把它往外丟給宿主。</summary>
        void Write(int slot, byte[] data);
    }
}
