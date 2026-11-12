//
// Probably Doom 新增（2026-11-12）：ISaveStore 的空實作（照上游 NullVideo、NullSound 的寫法），宿主沒給存檔位置時用。
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
    /// <summary>每一格都是空的、寫入直接丟掉：選單照常能開，只是存不起來。</summary>
    public sealed class NullSaveStore : ISaveStore
    {
        private static NullSaveStore instance;

        public static NullSaveStore GetInstance()
        {
            if (instance == null)
            {
                instance = new NullSaveStore();
            }

            return instance;
        }

        public byte[] Read(int slot)
        {
            return null;
        }

        public void Write(int slot, byte[] data)
        {
        }
    }
}
