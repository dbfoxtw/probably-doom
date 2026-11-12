//
// Probably Doom 修改（2026-11-12）：各格的說明文字改從宿主的 ISaveStore 讀（原本讀 doomsav<N>.dsg，還用了 .NET 7 才有的 Stream.ReadExactly）。
//
// Copyright (C) 1993-1996 Id Software, Inc.
// Copyright (C) 2019-2020 Nobuaki Tanaka
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
    public sealed class SaveSlots
    {
        private static readonly int slotCount = 6;

        private ISaveStore store;
        private string[] slots;

        public SaveSlots(ISaveStore store)
        {
            this.store = store;
        }

        // Probably Doom：各格的說明文字改從宿主的 ISaveStore 讀（原本直接讀執行檔旁的 doomsav<N>.dsg 開頭 24 bytes）。
        private void ReadSlots()
        {
            slots = new string[slotCount];

            for (var i = 0; i < slots.Length; i++)
            {
                slots[i] = SaveAndLoad.GetDescription(store.Read(i));
            }
        }

        public static int SlotCount => slotCount;

        public string this[int number]
        {
            get
            {
                if (slots == null)
                {
                    ReadSlots();
                }

                return slots[number];
            }

            set
            {
                slots[number] = value;
            }
        }

        public int Count => slots.Length;
    }
}
