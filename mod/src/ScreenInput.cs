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
    using UnityEngine;
    using UnityEngine.EventSystems;

    /// <summary>
    /// 掛在交易面板裡 Doom 畫面區的黑底上：記下「這一幀滑鼠在 Doom 畫面上按下了」（取得焦點，需求.md 決定 10）。
    /// 用 EventSystem 的按下事件而不是自己比矩形：EventSystem 只把按下交給最上層的介面，Doom 畫面被別的視窗蓋住時點那個視窗不算點到 Doom。
    /// EventSystem 在它的 Update 裡送事件，DoomRunner 在 LateUpdate 看，同一幀就對得上。
    /// 也接住滾輪：不然滾輪會往上傳到列表的 ScrollRect，捲動藏在後面的列表。
    /// </summary>
    sealed class ScreenInput : MonoBehaviour, IPointerDownHandler, IScrollHandler
    {
        /// <summary>最近一次在 Doom 畫面上按下滑鼠的 Time.frameCount。</summary>
        internal int PointerDownFrame = -1;

        public void OnPointerDown(PointerEventData eventData) => PointerDownFrame = Time.frameCount;

        public void OnScroll(PointerEventData eventData) { }
    }
}
