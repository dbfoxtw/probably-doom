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
    using UnityEngine.UI;

    /// <summary>
    /// 交易面板上緣的 DOOM 按鈕（需求.md 決定 2）：外觀照調整大小按鈕，寬度加倍。
    /// 原按鈕是一張 16×16 的像素圖、SpriteSwap 換三張（一般、滑過、按下）；加寬的底用同一張貼圖切三段拼
    /// （左右各 4 欄、中間一欄拉長，ButtonLayout），所以不能用 Button 內建的 SpriteSwap（它只換一張 Image 的 sprite）。
    /// 這裡繼承 Button、覆寫 DoStateTransition：Unity 告訴我們現在是一般／滑過／按下，就換三段的 UV；
    /// Doom 模式中（Active）一直顯示按下去的樣子，和議價按鈕開著時一樣是「開著」的意思。
    /// 「DOOM」字跟著原圖示的下沉量往下移（滑過 1 個圖像素、按下 2 個）。
    /// </summary>
    sealed class DoomButton : Button
    {
        /// <summary>三種狀態（0 一般、1 滑過、2 按下）各自三段的 UV；沒拿到的狀態用一般的。</summary>
        internal Rect[][] Uvs;
        /// <summary>三種狀態各自的貼圖（遊戲的三張圖在同一張貼圖上，但不假設）。</summary>
        internal Texture[] Textures;
        internal RawImage Left, Mid, Right;
        internal RectTransform Label;
        /// <summary>原按鈕圖的高（像素），算字的下沉量用。</summary>
        internal float SpriteHeight = 16f;

        SelectionState _state = SelectionState.Normal;
        bool _active;
        int _shown = -1;

        /// <summary>Doom 模式中：一直顯示按下去的樣子。</summary>
        internal bool Active
        {
            get => _active;
            set
            {
                if (_active == value) return;
                _active = value;
                Apply();
            }
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            _state = state;
            Apply();
        }

        /// <summary>重新套用（尺寸變了、剛建好時）。</summary>
        internal void Refresh()
        {
            _shown = -1;
            Apply();
        }

        void Apply()
        {
            // AddComponent 的當下 Unity 就會呼叫 DoStateTransition，那時欄位還沒設
            if (Uvs == null || Left == null || Mid == null || Right == null) return;
            var frame = _active || _state == SelectionState.Pressed ? 2 : _state == SelectionState.Highlighted ? 1 : 0;
            if (frame == _shown) return;
            _shown = frame;
            var uv = Uvs[frame] ?? Uvs[0];
            if (uv == null) return;
            if (Textures != null)
            {
                var tex = Textures[frame];
                if (tex == null) tex = Textures[0];
                Left.texture = Mid.texture = Right.texture = tex;
            }
            Left.uvRect = uv[0];
            Mid.uvRect = uv[1];
            Right.uvRect = uv[2];
            if (Label != null)
            {
                var rt = (RectTransform)transform;
                Label.anchoredPosition = new Vector2(0f, ButtonLayout.LabelOffset(frame, rt.rect.height, SpriteHeight));
            }
        }
    }
}
