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
    ///   「點一下繼續」＋沒有 WAD、出錯時的說明（說明文字、路徑框、「複製路徑」按鈕，決定 29）。
    ///   掛在 body 底下，面板的三種尺寸（StoreUI.ToggleZoom 縮整個視窗）自動跟著縮；
    ///   body 的高度隨上面的客人資訊變（TabletUI.LayoutSection），畫面與說明跟著重算。
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
        // 路徑框與「複製路徑」按鈕（使用者選的示意圖，決定 29）
        static readonly Color BoxFill = new Color32(27, 27, 25, 255);
        static readonly Color BoxBorder = new Color32(92, 89, 76, 255);
        static readonly Color ButtonBorder = new Color32(142, 138, 114, 255);
        static readonly Color ButtonLabelColor = new Color32(42, 38, 41, 255);
        /// <summary>按鈕的底色：一般同說明文字的淺米色，滑過亮一點、按下暗一點（Button 的 ColorTint，底圖是白的，顏色就是這幾個）。</summary>
        static readonly Color ButtonNormal = new Color32(213, 210, 184, 255);
        static readonly Color ButtonHover = new Color32(230, 227, 204, 255);
        static readonly Color ButtonPressed = new Color32(184, 181, 156, 255);
        /// <summary>按了「複製路徑」之後顯示「已複製」幾秒。</summary>
        const float CopiedSeconds = 2f;

        /// <summary>Doom 畫面區要顯示的說明。</summary>
        internal enum Message { None, NoWad, Error }

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
        readonly TextMeshProUGUI _pauseText;

        // 沒有 WAD、出錯時的說明：一組三件（說明文字、路徑框、複製路徑按鈕），整組等比縮放（MessageLayout）
        readonly RectTransform _msgRoot, _boxRt, _copyRt;
        readonly TextMeshProUGUI _msgText, _pathText, _copyLabel;
        readonly Button _copyButton;

        Vector2 _lastButtonPos = new Vector2(float.NaN, float.NaN), _lastButtonSize;
        Vector2 _lastBodySize = new Vector2(-1, -1);
        string _lastPause;

        Message _lastKind;
        string _lastPath;
        Strings _lastStrings;
        Vector2 _lastMsgArea = new Vector2(-1, -1);
        TMP_FontAsset _lastMsgFont;
        bool _msgDirty = true, _hasBox, _hasButton;
        /// <summary>按鈕的字的寬高（「複製路徑」與「已複製」取寬的那個），換字或換字型時重量。</summary>
        float _labelW, _labelH;
        /// <summary>路徑框裡那個路徑的原始字串（按鈕複製這個；不從 TMP 讀回來：繁中 mod 會攔截 TMP 的字）。</summary>
        string _copyText;
        float _copiedUntil;
        bool _showingCopied;
        bool _copyFailed;

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

            // ── 沒有 WAD、出錯時的說明（決定 29）：整組照基準寬 530 排、在畫面區置中，放不下就整組等比縮小（MessageLayout）──
            var msg = new GameObject("Message", typeof(RectTransform));
            msg.transform.SetParent(screen.transform, false);
            _msgRoot = (RectTransform)msg.transform;
            _msgRoot.anchorMin = _msgRoot.anchorMax = _msgRoot.pivot = new Vector2(0.5f, 0.5f);
            _msgRoot.anchoredPosition = Vector2.zero;

            // 1. 說明文字：置中、照句子換行（中文用 <nobr>，TextMarkup.Sentences）
            _msgText = TabletUI.NewText(frame, _msgRoot, "Text", 0, 0, 0, 0, FontLoader.FontRole.Noto_Sans, MessageLayout.NoWadTextSize,
                TextColor, TextAlignmentOptions.Center, true);
            _msgText.text = "";

            // 2. 路徑框：邊框（外層的底色）＋深色的底（內縮邊框寬）＋靠左的路徑，只在「\」後面換行（TextMarkup.Path）。
            //    都不收點擊：點在框上照舊算點了 Doom 畫面區（重新找 WAD）
            _boxRt = NewFlat(_msgRoot, "PathBox", BoxBorder, false);
            var boxFill = NewFlat(_boxRt, "Fill", BoxFill, false);
            TabletUI.Stretch(boxFill, MessageLayout.Border);
            _pathText = TabletUI.NewText(frame, boxFill, "Path", 0, 0, 0, 0, FontLoader.FontRole.Noto_Sans, MessageLayout.PathTextSize,
                TextColor, TextAlignmentOptions.Left, true);
            // 字面的 \n、\u＋4 個十六進位…不要轉成控制字元：退回完整路徑時（例如 C:\Users\nick\…）會被拆壞
            _pathText.parseCtrlCharacters = false;
            _pathText.text = "";
            var prt2 = _pathText.rectTransform;
            prt2.anchorMin = Vector2.zero;
            prt2.anchorMax = Vector2.one;
            prt2.pivot = new Vector2(0.5f, 0.5f);
            prt2.offsetMin = new Vector2(MessageLayout.BoxPadH, MessageLayout.BoxPadV);
            prt2.offsetMax = new Vector2(-MessageLayout.BoxPadH, -MessageLayout.BoxPadV);

            // 3. 「複製路徑」按鈕：邊框收點擊（整顆都按得到），底是 Button 的 ColorTint 對象（底圖白色，顏色由 ColorBlock 給）
            var copyBorder = NewFlat(_msgRoot, "CopyPath", ButtonBorder, true);
            _copyRt = copyBorder;
            var copyFill = NewFlat(copyBorder, "Fill", Color.white, false);
            TabletUI.Stretch(copyFill, MessageLayout.Border);
            _copyLabel = TabletUI.NewText(frame, copyFill, "Label", 0, 0, 0, 0, FontLoader.FontRole.Noto_Sans, MessageLayout.ButtonTextSize,
                ButtonLabelColor, TextAlignmentOptions.Center);
            TabletUI.Stretch(_copyLabel.rectTransform);
            _copyLabel.text = "";
            _copyButton = copyBorder.gameObject.AddComponent<Button>();
            _copyButton.targetGraphic = copyFill.GetComponent<Image>();
            _copyButton.transition = Selectable.Transition.ColorTint;
            var colors = _copyButton.colors;
            colors.normalColor = ButtonNormal;
            colors.highlightedColor = ButtonHover;
            colors.pressedColor = ButtonPressed;
            colors.selectedColor = ButtonNormal;
            colors.disabledColor = ButtonNormal;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.05f;
            _copyButton.colors = colors;
            _copyButton.onClick.AddListener(CopyPath);
            TabletUI.NoKeyboardFocus(_copyButton); // 在 Doom 裡按 Enter／空白鍵不會「按」到它
            // 按鈕接走了按下，黑底的 ScreenInput 收不到（不算點了 Doom 畫面）；視窗照樣要被拉到最上層，交給它往上轉
            copyBorder.gameObject.AddComponent<ButtonPointerForward>().Screen = _input;

            msg.SetActive(false);
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

        /// <summary>一塊純色（沒有圖的 Image）；raycast＝收不收點擊。</summary>
        static RectTransform NewFlat(Transform parent, string name, Color color, bool raycast)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return (RectTransform)go.transform;
        }

        /// <summary>左上角錨點、從父物件的左上角往右 x、往下 y 的位置（畫布單位）。</summary>
        static void PlaceTopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        /// <summary>和遊戲的調整大小按鈕一樣的按鍵聲（NegociationUIManager.OnResizeClick）：DOOM 按鈕與「複製路徑」按鈕用。</summary>
        internal static void PlayButtonSound()
        {
            var audio = EmporiumAudioPlayer.Instance;
            if (audio != null && audio.audioButton1 != null) audio.audioButton1.Play();
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

        /// <summary>這一幀滑鼠在 Doom 畫面上按下了（ScreenInput；「複製路徑」按鈕上不算）。</summary>
        internal bool PointerDownThisFrame => _input != null && _input.PointerDownFrame == Time.frameCount;

        /// <summary>這一幀滑鼠**左鍵**在 Doom 畫面上按下了（滑鼠開火，MouseFire）。</summary>
        internal bool LeftDownThisFrame => _input != null && _input.LeftDownFrame == Time.frameCount;

        /// <summary>
        /// 每幀（DoomRunner.LateUpdate）：DOOM 按鈕對齊調整大小按鈕、顯示 Doom 模式的樣子；Doom 畫面區照狀態顯示。
        /// texture＝要顯示的畫面（null＝不顯示），pause＝疊「點一下繼續」，message＝說明（沒有 WAD、出錯），
        /// path＝說明底下路徑框的路徑（DataPaths.Display 的寫法；空字串＝拿不到）。
        /// </summary>
        internal void Sync(bool doomMode, Texture texture, bool pause, Message message, string path, Strings strings)
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

            SyncMessage(message, path, strings);
        }

        /// <summary>
        /// 沒有 WAD、出錯時的說明（決定 29）。說明、路徑、語言變了才重建字；字、字型、畫面區大小變了才重排（MessageLayout）。
        /// 「已複製」顯示 2 秒再變回「複製路徑」。
        /// </summary>
        void SyncMessage(Message message, string path, Strings strings)
        {
            var show = message != Message.None;
            var go = _msgRoot.gameObject;
            if (go.activeSelf != show) go.SetActive(show);
            if (!show) return;

            path = path ?? "";
            if (message != _lastKind || path != _lastPath || strings != _lastStrings) RebuildMessage(message, path, strings);

            if (_showingCopied && Time.unscaledTime >= _copiedUntil)
            {
                _showingCopied = false;
                _copyLabel.text = strings.CopyPath;
            }

            var r = _screenRt.rect;
            var area = new Vector2(r.width, r.height);
            // 字型會跟著遊戲語言換（TabletUI.NewText 掛的 TMPLocalizer），換了字的寬高就不同，要重量
            if (_msgText.font != _lastMsgFont) _msgDirty = true;
            if (!_msgDirty && area == _lastMsgArea) return;
            _msgDirty = false;
            _lastMsgArea = area;
            _lastMsgFont = _msgText.font;
            LayoutMessage(area);
        }

        /// <summary>換說明的字：說明兩句（中文用 &lt;nobr&gt; 照句子換行）、路徑框、按鈕（有路徑才有）。</summary>
        void RebuildMessage(Message message, string path, Strings strings)
        {
            _lastKind = message;
            _lastPath = path;
            _lastStrings = strings;
            _msgDirty = true;

            var error = message == Message.Error;
            _msgText.fontSize = error ? MessageLayout.ErrorTextSize : MessageLayout.NoWadTextSize;
            _msgText.text = TextMarkup.Sentences(error ? strings.Error : strings.NoWad, strings.Cjk);

            // 路徑拿不到時：出錯畫面的框裡只寫 Player.log、沒有按鈕；沒有 WAD 的畫面不顯示框（沒有東西可以寫）
            var hasPath = path.Length > 0;
            _copyText = hasPath ? path : null;
            _hasBox = hasPath || error;
            _hasButton = hasPath;
            _pathText.text = hasPath ? TextMarkup.Path(path) : error ? "Player.log" : "";
            if (_boxRt.gameObject.activeSelf != _hasBox) _boxRt.gameObject.SetActive(_hasBox);
            if (_copyRt.gameObject.activeSelf != _hasButton) _copyRt.gameObject.SetActive(_hasButton);
            _showingCopied = false;
            _copyLabel.text = strings.CopyPath;
        }

        /// <summary>
        /// 照基準寬排三件（MessageLayout），整組在畫面區置中、放不下就整組等比縮小（一個 localScale，字的比例不變）。
        /// 字的高用 TMP 照固定的寬量（GetPreferredValues），所以換行的位置不隨畫面區大小變。
        /// </summary>
        void LayoutMessage(Vector2 area)
        {
            var textH = _msgText.GetPreferredValues(_msgText.text, MessageLayout.ContentWidth, 0f).y;
            var pathH = _hasBox ? _pathText.GetPreferredValues(_pathText.text, MessageLayout.PathTextWidth, 0f).y : 0f;
            if (_hasButton)
            {
                // 「複製路徑」與「已複製」取寬的那個：按了之後按鈕不會變大小
                var a = _copyLabel.GetPreferredValues(_lastStrings.CopyPath);
                var b = _copyLabel.GetPreferredValues(_lastStrings.Copied);
                _labelW = Mathf.Max(a.x, b.x);
                _labelH = Mathf.Max(a.y, b.y);
            }
            var lay = MessageLayout.Compute(area.x, area.y, textH, _hasBox, pathH, _hasButton, _labelW, _labelH);

            _msgRoot.sizeDelta = new Vector2(lay.Width, lay.Height);
            _msgRoot.localScale = new Vector3(lay.Scale, lay.Scale, 1f);
            PlaceTopLeft(_msgText.rectTransform, MessageLayout.Inset, lay.TextY, MessageLayout.ContentWidth, lay.TextH);
            if (_hasBox) PlaceTopLeft(_boxRt, MessageLayout.Inset, lay.BoxY, MessageLayout.ContentWidth, lay.BoxH);
            if (_hasButton) PlaceTopLeft(_copyRt, (lay.Width - lay.ButtonW) / 2f, lay.ButtonY, lay.ButtonW, lay.ButtonH);
        }

        /// <summary>
        /// 「複製路徑」：把框裡那個路徑的原始字串寫進剪貼簿（不是從 TMP 讀回來的字：繁中 mod 會攔截 TMP 的字），
        /// 按鈕的字變成「已複製」2 秒，播按鍵聲。玩家貼進檔案總管的網址列就打開（%USERPROFILE% 開頭，檔案總管看得懂）。
        /// GUIUtility.systemCopyBuffer 是 Unity 的剪貼簿（UnityEngine.IMGUIModule），官方掃描不管（決定 29）。
        /// </summary>
        void CopyPath()
        {
            if (string.IsNullOrEmpty(_copyText) || _lastStrings == null) return;
            try
            {
                GUIUtility.systemCopyBuffer = _copyText;
            }
            catch (Exception e)
            {
                if (!_copyFailed) ProbablyDoomMod.Warning($"寫進剪貼簿失敗：{e.Message}");
                _copyFailed = true;
                return;
            }
            _copyLabel.text = _lastStrings.Copied;
            _showingCopied = true;
            _copiedUntil = Time.unscaledTime + CopiedSeconds;
            try { PlayButtonSound(); }
            catch (Exception) { } // 只是少了聲音
            ProbablyDoomMod.DebugLog($"複製路徑：{_copyText}");
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
