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
    using TMPro;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// 交易面板（NegociationUIManager 建的平板視窗）上我們加的東西（需求.md 決定 1～5）：
    /// - 上緣的 DOOM 按鈕（DoomButton）：調整大小按鈕的左邊、同樣的間距，寬兩倍、高一樣；每幀對齊調整大小按鈕（Sync）。
    /// - 中間列表區（TabletFrame.body）底下撐滿的 Doom 畫面區：黑底（擋住後面列表的點擊）＋4:3 的 RawImage（左右留黑邊）＋
    ///   「點一下繼續」＋說明文字（沒有 WAD、出錯）。掛在 body 底下，面板的三種尺寸（StoreUI.ToggleZoom 縮整個視窗）自動跟著縮；
    ///   body 的高度隨上面的客人資訊變（TabletUI.LayoutSection），畫面跟著重算。
    /// 遊戲每 0.2 秒重建列表（RefreshNow → Rebuild）只清 infoHost、content、footer、buttonHost，不動 body 的直接子物件，
    /// 但保險起見每幀確認畫面區還是 body 的最後一個子物件（畫在最上層）。
    /// 每次載入店面場景重建一次（物件跟著場景銷毀；Alive 會變成 false）。
    /// </summary>
    sealed class TabletPanel
    {
        /// <summary>DOOM 字的顏色：遊戲按鈕圖示的暗紅（button_zoom 圖裡的 (100,52,59)；使用者選的示意圖從截圖量的是 (98,53,58)）。</summary>
        static readonly Color LabelColor = new Color32(100, 52, 59, 255);
        /// <summary>說明文字與「點一下繼續」：淺米色，接近按鈕圖的亮邊 (213,210,184)，黑底上看得清楚。</summary>
        static readonly Color TextColor = new Color32(213, 210, 184, 255);

        readonly NegotiationTabletFrame _frame;
        readonly GameObject _ui;
        readonly RectTransform _resize, _bargain;

        readonly RectTransform _buttonRt;
        readonly DoomButton _button;
        readonly Box _spriteRect;

        readonly RectTransform _screenRt;
        readonly ScreenInput _input;
        readonly RawImage _view;
        readonly GameObject _pause;
        readonly TextMeshProUGUI _pauseText, _message;

        Vector2 _lastButtonPos = new Vector2(float.NaN, float.NaN), _lastButtonSize;
        Vector2 _lastBodySize = new Vector2(-1, -1);
        string _lastMessage, _lastPause;

        TabletPanel(NegotiationTabletFrame frame, GameObject ui, Action onClick, Func<string> tooltip)
        {
            _frame = frame;
            _ui = ui;
            _resize = (RectTransform)frame.resizeButton.transform;
            _bargain = frame.bargainButton != null ? (RectTransform)frame.bargainButton.transform : null;

            // ── DOOM 按鈕：和調整大小按鈕同一層（視窗的根），排在它前面 ──
            var go = new GameObject("ProbablyDoom.Button", typeof(RectTransform));
            go.transform.SetParent(_resize.parent, false);
            go.transform.SetSiblingIndex(_resize.GetSiblingIndex());
            _buttonRt = (RectTransform)go.transform;
            _buttonRt.anchorMin = _resize.anchorMin;
            _buttonRt.anchorMax = _resize.anchorMax;
            _buttonRt.pivot = _resize.pivot;

            var hit = go.AddComponent<Image>(); // 點擊範圍（透明）；三段的圖都不收點擊
            hit.color = Color.clear;
            hit.raycastTarget = true;

            var left = NewSlice(go.transform, "Left");
            var mid = NewSlice(go.transform, "Mid");
            var right = NewSlice(go.transform, "Right");

            var label = TabletUI.NewText(frame, go.transform, "Label", 0, 0, 0, 0, FontLoader.FontRole.Lubrifont, 20f, LabelColor,
                TextAlignmentOptions.Center);
            label.text = "DOOM";
            label.enableAutoSizing = true; // 四個字母填滿按鈕
            label.fontSizeMin = 6f;
            label.fontSizeMax = 64f;
            label.margin = Vector4.zero;

            // 外觀：從調整大小按鈕的圖組三段（DoomButton 的註解）
            var uvs = new Rect[3][];
            var textures = new Texture[3];
            _spriteRect = new Box(0, 0, 16, 16);
            var normal = frame.resizeButton.image != null ? frame.resizeButton.image.sprite : null;
            if (normal != null)
            {
                Sprite hover = normal, pressed = normal;
                if (frame.resizeButton.transition == Selectable.Transition.SpriteSwap)
                {
                    var ss = frame.resizeButton.spriteState;
                    if (ss.highlightedSprite != null) hover = ss.highlightedSprite;
                    if (ss.pressedSprite != null) pressed = ss.pressedSprite;
                }
                else ProbablyDoomMod.Warning($"調整大小按鈕不是 SpriteSwap（{frame.resizeButton.transition}），DOOM 按鈕只有一般的樣子");
                var sprites = new[] { normal, hover, pressed };
                for (var i = 0; i < 3; i++)
                {
                    uvs[i] = SliceUvs(sprites[i], out var rect);
                    textures[i] = sprites[i].texture;
                    if (i == 0) _spriteRect = rect;
                }
            }
            else
            {
                ProbablyDoomMod.Warning("讀不到調整大小按鈕的圖，DOOM 按鈕用素色的底");
                left.color = mid.color = right.color = new Color32(168, 174, 165, 255);
            }

            _button = go.AddComponent<DoomButton>();
            _button.transition = Selectable.Transition.None;
            _button.targetGraphic = hit;
            _button.Left = left;
            _button.Mid = mid;
            _button.Right = right;
            _button.Label = label.rectTransform;
            _button.SpriteHeight = _spriteRect.H;
            if (normal != null)
            {
                _button.Uvs = uvs;
                left.texture = mid.texture = right.texture = textures[0];
                _button.Textures = textures;
            }
            _button.onClick.AddListener(() => onClick());
            TabletUI.NoKeyboardFocus(_button);
            TabletUI.AddTooltip(go, tooltip);

            // ── Doom 畫面區：body 底下撐滿、最後一個子物件 ──
            var screen = new GameObject("ProbablyDoom.Screen", typeof(RectTransform));
            screen.transform.SetParent(frame.body, false);
            _screenRt = (RectTransform)screen.transform;
            TabletUI.Stretch(_screenRt);
            var bg = screen.AddComponent<Image>();
            bg.color = Color.black;
            bg.raycastTarget = true; // 擋住後面列表的點擊
            _input = screen.AddComponent<ScreenInput>();

            var viewGo = new GameObject("View", typeof(RectTransform));
            viewGo.transform.SetParent(screen.transform, false);
            _view = viewGo.AddComponent<RawImage>();
            _view.raycastTarget = false;
            var vrt = _view.rectTransform;
            vrt.anchorMin = vrt.anchorMax = vrt.pivot = new Vector2(0.5f, 0.5f);

            _pause = new GameObject("Pause", typeof(RectTransform));
            _pause.transform.SetParent(screen.transform, false);
            var prt = (RectTransform)_pause.transform;
            prt.anchorMin = new Vector2(0f, 0.4f);
            prt.anchorMax = new Vector2(1f, 0.6f);
            prt.offsetMin = prt.offsetMax = Vector2.zero;
            var band = _pause.AddComponent<Image>();
            band.color = new Color(0f, 0f, 0f, 0.6f);
            band.raycastTarget = false;
            _pauseText = NewStretchedText(frame, _pause.transform, "Text", 4f, 8f, 28f);

            // 說明文字（沒有 WAD、出錯）：字很短，自動放大填滿畫面區（上限 48、下限 12，預設的列表區 530×378）；
            // 換行打開：ModData 的路徑一行放不下時自動換行（TMP 在空白處換，一個字比一行長時在字中間換）
            _message = NewStretchedText(frame, screen.transform, "Message", 16f, 12f, 48f);
            _message.enableWordWrapping = true;

            screen.SetActive(false);
            _button.Refresh();
        }

        /// <summary>在店面場景建好交易面板之後接上（NegociationUIManager.CreateWindow 的 postfix）。出錯時回傳 null、記 log。</summary>
        internal static TabletPanel Attach(NegotiationTabletFrame frame, GameObject ui, Action onClick, Func<string> tooltip)
        {
            if (frame == null || ui == null || frame.resizeButton == null || frame.body == null)
            {
                ProbablyDoomMod.Warning("交易面板少了調整大小按鈕或列表區，不加 DOOM 按鈕");
                return null;
            }
            return new TabletPanel(frame, ui, onClick, tooltip);
        }

        static RawImage NewSlice(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<RawImage>();
            img.raycastTarget = false;
            return img;
        }

        static TextMeshProUGUI NewStretchedText(TabletSkin skin, Transform parent, string name, float inset, float minSize, float maxSize)
        {
            var t = TabletUI.NewText(skin, parent, name, 0, 0, 0, 0, FontLoader.FontRole.Noto_Sans, maxSize, TextColor,
                TextAlignmentOptions.Center);
            TabletUI.Stretch(t.rectTransform, inset);
            t.enableAutoSizing = true;
            t.fontSizeMin = minSize;
            t.fontSizeMax = maxSize;
            t.text = "";
            return t;
        }

        /// <summary>一張按鈕圖切三段的 UV（ButtonLayout.SliceUvs）；rect 回傳圖在貼圖裡的位置（像素）。</summary>
        static Rect[] SliceUvs(Sprite sprite, out Box rect)
        {
            Rect r;
            try { r = sprite.textureRect; } // 緊密打包的圖拿不到 textureRect，退回 rect
            catch (Exception) { r = sprite.rect; }
            rect = new Box(r.x, r.y, r.width, r.height);
            var tex = sprite.texture;
            ButtonLayout.SliceUvs(rect, tex.width, tex.height, out var l, out var m, out var rr);
            return new[] { ToRect(l), ToRect(m), ToRect(rr) };
        }

        static Rect ToRect(Box b) => new Rect(b.X, b.Y, b.W, b.H);

        /// <summary>面板的物件還在（離開店面場景時跟著場景銷毀）。</summary>
        internal bool Alive => _frame != null && _ui != null && _button != null && _screenRt != null && _resize != null;

        /// <summary>交易面板開著（畫得出來：自己和上層都是 active）。</summary>
        internal bool Open => Alive && _ui.activeInHierarchy;

        /// <summary>面板自己是 active（遊戲的 CloseUI 把 uiObject 設成 inactive；ModeRules.KeepDoomMode 看這個）。</summary>
        internal bool ActiveSelf => Alive && _ui.activeSelf;

        /// <summary>
        /// mod 被停掉時（DoomRunner.Shutdown）：拿掉我們加的 DOOM 按鈕與 Doom 畫面區。遊戲停掉 mod 時只 Destroy 我們型別的
        /// MonoBehaviour（DoomButton、ScreenInput），Image、RawImage、TextMeshPro 這些 Unity 的元件會留在面板上：
        /// 黑底會一直蓋住列表、擋住點擊。兩個各自包 try。
        /// </summary>
        internal void Teardown()
        {
            // 先 inactive 再 Destroy：Destroy 要等這一幀結束才生效，inactive 馬上就看不到、點不到
            try
            {
                if (_buttonRt != null)
                {
                    _buttonRt.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(_buttonRt.gameObject);
                }
            }
            catch (Exception e) { ProbablyDoomMod.Warning($"拿掉 DOOM 按鈕失敗：{e.Message}"); }
            try
            {
                if (_screenRt != null)
                {
                    _screenRt.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(_screenRt.gameObject);
                }
            }
            catch (Exception e) { ProbablyDoomMod.Warning($"拿掉 Doom 畫面區失敗：{e.Message}"); }
        }

        /// <summary>這一幀滑鼠在 Doom 畫面上按下了（ScreenInput）。</summary>
        internal bool PointerDownThisFrame => _input != null && _input.PointerDownFrame == Time.frameCount;

        /// <summary>
        /// 每幀（DoomRunner.LateUpdate）：DOOM 按鈕對齊調整大小按鈕、顯示 Doom 模式的樣子；Doom 畫面區照狀態顯示。
        /// texture＝要顯示的畫面（null＝不顯示），message＝說明文字（null＝不顯示），pause＝疊「點一下繼續」。
        /// </summary>
        internal void Sync(bool doomMode, Texture texture, bool pause, string message, Strings strings)
        {
            AlignButton();
            _button.Active = doomMode;

            var screen = _screenRt.gameObject;
            if (screen.activeSelf != doomMode) screen.SetActive(doomMode);
            if (!doomMode) return;

            if (_screenRt.GetSiblingIndex() != _screenRt.parent.childCount - 1) _screenRt.SetAsLastSibling();

            var showView = texture != null;
            if (_view.gameObject.activeSelf != showView) _view.gameObject.SetActive(showView);
            if (showView)
            {
                if (_view.texture != texture) _view.texture = texture;
                FitView();
            }

            var showPause = pause && showView;
            if (_pause.activeSelf != showPause) _pause.SetActive(showPause);
            if (showPause && _lastPause != strings.Paused)
            {
                _lastPause = strings.Paused;
                _pauseText.text = strings.Paused;
            }

            var showMessage = message != null;
            if (_message.gameObject.activeSelf != showMessage) _message.gameObject.SetActive(showMessage);
            if (showMessage && _lastMessage != message)
            {
                _lastMessage = message;
                _message.text = message;
            }
        }

        /// <summary>
        /// DOOM 按鈕跟著調整大小按鈕：左邊、同樣的間距（量調整大小按鈕右緣到議價按鈕左緣，議價按鈕隱藏時位置照舊），
        /// 寬兩倍、高一樣（ButtonLayout）。位置用兩顆按鈕的左緣算（不假設 pivot 在左上角）。沒變就不動。
        /// </summary>
        void AlignButton()
        {
            var rs = _resize.sizeDelta;
            var rp = _resize.anchoredPosition;
            var resizeLeft = rp.x - _resize.pivot.x * rs.x;
            var gap = ButtonLayout.DefaultGap;
            if (_bargain != null)
            {
                var bargainLeft = _bargain.anchoredPosition.x - _bargain.pivot.x * _bargain.sizeDelta.x;
                gap = ButtonLayout.Gap(resizeLeft, rs.x, bargainLeft);
            }
            var box = ButtonLayout.DoomButton(resizeLeft, rp.y, rs.x, rs.y, gap);
            var pos = new Vector2(box.X + _buttonRt.pivot.x * box.W, box.Y);
            var size = new Vector2(box.W, box.H);
            if (pos == _lastButtonPos && size == _lastButtonSize) return;
            _lastButtonPos = pos;
            _lastButtonSize = size;
            _buttonRt.anchoredPosition = pos;
            _buttonRt.sizeDelta = size;

            ButtonLayout.SliceWidths(box.W, box.H, _spriteRect, out var edge, out var midW);
            SetSlice(_button.Left.rectTransform, 0f, edge, true);
            SetSlice(_button.Right.rectTransform, 1f, edge, true);
            var m = _button.Mid.rectTransform;
            m.anchorMin = Vector2.zero;
            m.anchorMax = Vector2.one;
            m.pivot = new Vector2(0.5f, 0.5f);
            m.offsetMin = new Vector2(edge, 0f);
            m.offsetMax = new Vector2(-edge, 0f);

            // 字的範圍：框（2 個圖像素）以內，左右再留一點
            var scale = _spriteRect.H > 0 ? box.H / _spriteRect.H : 2f;
            var lrt = _button.Label;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.offsetMin = new Vector2(3 * scale, 3 * scale);
            lrt.offsetMax = new Vector2(-3 * scale, -3 * scale);
            _button.Refresh();
        }

        static void SetSlice(RectTransform rt, float side, float width, bool fullHeight)
        {
            rt.anchorMin = new Vector2(side, 0f);
            rt.anchorMax = new Vector2(side, fullHeight ? 1f : 0f);
            rt.pivot = new Vector2(side, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(width, 0f);
        }

        /// <summary>畫面 4:3、放在畫面區中間能多大就多大（左右或上下留黑；決定 4）。區域大小變了才重算。</summary>
        void FitView()
        {
            var r = _screenRt.rect;
            var size = new Vector2(r.width, r.height);
            if (size == _lastBodySize) return;
            _lastBodySize = size;
            FrameFit.Fit43(r.width, r.height, out var w, out var h);
            var vrt = _view.rectTransform;
            vrt.anchoredPosition = Vector2.zero;
            vrt.sizeDelta = new Vector2(w, h);
        }
    }
}
