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
    ///
    /// EventSystem 只把一個事件交給最近的一個處理者，我們接了按下，視窗根物件的 UIDragHandler（CustomUIBuilder.SetDraggable 加的）
    /// 就收不到了：它的 OnPointerDown 負責把視窗拉到最上層（SetAsLastSibling、MoveFront）與 PaperPanelFocusStamp.Bump
    /// （Esc 關哪個視窗看這個）。所以記下之後把同一個事件往上轉給父層，照 EventSystem 自己的規則找第一個處理者
    /// （ExecuteHierarchy：Doom 畫面區 → body → Section → 視窗根，中間沒有別的按下處理者，所以是 UIDragHandler）。
    /// 原本的事件停在我們這裡，轉一次就只觸發一次；UIDragHandler 的 OnPointerDown 不看事件內容，點列表本身時也是同樣的效果。
    ///
    /// 拖曳與滾輪接住、什麼都不做：沒接的話 EventSystem 會往上找到 UIDragHandler（從 Doom 畫面拖就拖動整個視窗）
    /// 或列表的 ScrollRect（捲動藏在後面的列表）。拖動視窗照樣用標題列（上緣、客人名字那一帶）。
    ///
    /// 左鍵另外記一個幀數（LeftDownFrame）：在 Doom 畫面上按著左鍵開火（需求.md 決定 26，規則在 MouseFire）。
    /// 其他鍵照樣算「點了 Doom 畫面」（拿焦點、轉給視窗），只是不開火。
    /// </summary>
    sealed class ScreenInput : MonoBehaviour, IPointerDownHandler, IScrollHandler,
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        /// <summary>最近一次在 Doom 畫面上按下滑鼠的 Time.frameCount。</summary>
        internal int PointerDownFrame = -1;
        /// <summary>最近一次在 Doom 畫面上按下滑鼠**左鍵**的 Time.frameCount（滑鼠開火用）。</summary>
        internal int LeftDownFrame = -1;

        bool _forwardFailed;

        public void OnPointerDown(PointerEventData eventData)
        {
            PointerDownFrame = Time.frameCount;
            if (eventData.button == PointerEventData.InputButton.Left) LeftDownFrame = Time.frameCount;
            ForwardToWindow(eventData);
        }

        /// <summary>
        /// 把按下轉給交易面板的視窗（父層往上第一個按下處理者，UIDragHandler）：視窗被拉到最上層、Esc 關它。
        /// Doom 畫面區上的「複製路徑」按鈕也用這個（ButtonPointerForward）：從畫面區的父層開始找，跳過我們自己，
        /// 所以按按鈕不算「點了 Doom 畫面」（不會重新找 WAD），視窗照樣被拉到最上層。
        /// </summary>
        internal void ForwardToWindow(PointerEventData eventData)
        {
            try
            {
                var parent = transform.parent;
                if (parent != null) ExecuteEvents.ExecuteHierarchy(parent.gameObject, eventData, ExecuteEvents.pointerDownHandler);
            }
            catch (System.Exception e)
            {
                // 轉不過去只是視窗不會被拉到最上層，Doom 照樣拿到焦點；只記一次
                if (_forwardFailed) return;
                _forwardFailed = true;
                ProbablyDoomMod.Warning($"把點擊轉給交易面板的視窗失敗（視窗不會被拉到最上層）：{e.Message}");
            }
        }

        public void OnScroll(PointerEventData eventData) { }

        public void OnInitializePotentialDrag(PointerEventData eventData) { }

        public void OnBeginDrag(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData) { }

        public void OnEndDrag(PointerEventData eventData) { }
    }

    /// <summary>
    /// 掛在 Doom 畫面區裡的「複製路徑」按鈕上（需求.md 決定 29）。EventSystem 把按下交給按鈕（Selectable 的 OnPointerDown），
    /// 黑底的 ScreenInput 就收不到：這一下不算「點了 Doom 畫面」（不重新找 WAD、不拿焦點），這正是要的。
    /// 可是視窗的 UIDragHandler 也收不到，視窗不會被拉到最上層；所以這裡（同一個物件上的另一個按下處理者，EventSystem 兩個都會叫）
    /// 交給 ScreenInput.ForwardToWindow 從畫面區的父層往上轉，效果和點列表本身一樣。
    /// </summary>
    sealed class ButtonPointerForward : MonoBehaviour, IPointerDownHandler
    {
        internal ScreenInput Screen;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Screen != null) Screen.ForwardToWindow(eventData);
        }
    }
}
