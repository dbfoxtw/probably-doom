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
    /// <summary>一個矩形（UV 或座標），不用 Unity 的 Rect，離線測試才測得到。</summary>
    internal struct Box
    {
        internal float X, Y, W, H;
        internal Box(float x, float y, float w, float h)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
        }
        public override string ToString() => $"({X}, {Y}, {W}, {H})";
    }

    /// <summary>
    /// DOOM 按鈕的位置與外觀的三段拼法（需求.md 決定 2）。不碰 Unity，離線測試測同一份。
    ///
    /// 交易面板上緣的三顆按鈕（預製物 NegotiationTablet 底下的 Resize、Bargain、Close）是 32×32、左上角錨點、x＝486／522／558、y＝-8，
    /// 間距 4。圖是 16×16 的像素圖放大 2 倍（調整大小的 button_zoom_0／1／2：一般、滑過、按下，SpriteSwap），
    /// 左右各 2 欄外框＋2 欄沒有圖示的底色，圖示在中間 8 欄（第 4～11 欄）。加寬一倍時：左右各取外側 4 欄（框＋底色），
    /// 中間用沒有圖示的第 2～3 欄之間的一欄拉長（取 2.5～3.5 這一格，兩欄同色，貼圖是 Point 或 Bilinear 都不會混到圖示）。
    /// </summary>
    internal static class ButtonLayout
    {
        /// <summary>預製物裡按鈕的間距（Resize 的右緣到 Bargain 的左緣）；量不出來時用它。</summary>
        internal const float DefaultGap = 4f;

        /// <summary>
        /// 原本按鈕之間的間距：調整大小按鈕的右緣到議價按鈕的左緣（兩個都是左上角錨點時的 anchoredPosition.x）。
        /// 量出來不合理（負的、比按鈕還寬）時用預設的 4。議價按鈕隱藏時位置照舊，量得到。
        /// </summary>
        internal static float Gap(float resizeX, float resizeW, float bargainX)
        {
            var g = bargainX - (resizeX + resizeW);
            return g >= 0 && g <= resizeW ? g : DefaultGap;
        }

        /// <summary>DOOM 按鈕：在調整大小按鈕的左邊、隔一樣的間距，寬是兩倍、高一樣。回傳左上角的 x（同樣的錨點）與寬高。</summary>
        internal static Box DoomButton(float resizeX, float resizeY, float resizeW, float resizeH, float gap)
        {
            var w = 2 * resizeW;
            return new Box(resizeX - gap - w, resizeY, w, resizeH);
        }

        /// <summary>
        /// 一張按鈕圖（貼圖裡的 spriteRect，像素；貼圖 texW×texH）切成三段的 UV（0～1）：左段、中段（一欄）、右段。
        /// 左右段各是圖寬的 1/4（16 像素的圖是 4 欄）。
        /// </summary>
        internal static void SliceUvs(Box spriteRect, float texW, float texH, out Box left, out Box mid, out Box right)
        {
            var edge = spriteRect.W / 4f;
            var v = spriteRect.Y / texH;
            var vh = spriteRect.H / texH;
            left = new Box(spriteRect.X / texW, v, edge / texW, vh);
            mid = new Box((spriteRect.X + edge - 1.5f) / texW, v, 1f / texW, vh);
            right = new Box((spriteRect.X + spriteRect.W - edge) / texW, v, edge / texW, vh);
        }

        /// <summary>
        /// 三段在按鈕上的寬度：左右段照原圖放大的倍數（按鈕高 ÷ 圖高，32÷16＝2 倍 → 8），中段補滿剩下的。
        /// </summary>
        internal static void SliceWidths(float buttonW, float buttonH, Box spriteRect, out float edgeW, out float midW)
        {
            var scale = spriteRect.H > 0 ? buttonH / spriteRect.H : 2f;
            edgeW = spriteRect.W / 4f * scale;
            midW = buttonW - 2 * edgeW;
            if (midW < 0) midW = 0;
        }

        /// <summary>
        /// 字跟著按鈕往下沉多少（畫布單位，往下是負的）：原本的圖示滑過時下沉 1 個圖像素、按下時 2 個（button_zoom_1／2），
        /// 乘上放大倍數。frame：0 一般、1 滑過、2 按下。
        /// </summary>
        internal static float LabelOffset(int frame, float buttonH, float spriteH)
        {
            var scale = spriteH > 0 ? buttonH / spriteH : 2f;
            return -frame * scale;
        }
    }

    /// <summary>Doom 畫面在交易面板的列表區裡的大小（需求.md 決定 4）。</summary>
    internal static class FrameFit
    {
        /// <summary>4:3、放進 areaW×areaH 能多大就多大（置中，左右或上下留黑邊）。</summary>
        internal static void Fit43(float areaW, float areaH, out float w, out float h)
        {
            if (areaW <= 0 || areaH <= 0)
            {
                w = h = 0;
                return;
            }
            if (areaW * 3 >= areaH * 4)
            {
                h = areaH;
                w = areaH * 4f / 3f;
            }
            else
            {
                w = areaW;
                h = areaW * 3f / 4f;
            }
        }
    }

    /// <summary>
    /// 平板本體（店裡右邊桌上那台，場景的 NegociationTablet）的螢幕位置與形狀（需求.md 決定 6、28）。不碰 Unity，離線測試測同一份。
    /// 平板的圖是一整張 960×540 的圖層 negociation_tablet（pivot 中心、100 像素一單位），
    /// 螢幕在圖上（左上角為原點、含端點）x＝675～753、y＝183～237，79×55 遊戲像素（平常畫著 Useful info）。
    ///
    /// 小畫面蓋住整個 79×55，但有 336 格透明（alpha 0），讓原圖露出來（決定 28；形狀是 2026-11-12 從 sharedassets1.assets 的
    /// negociation_tablet 量的，評估報告.md「平板與交易面板（玩 Doom 的地方）」）：
    /// - 下緣兩角夾住螢幕的支架（從 y＝221 起斜切 6 列，之後到底；兩邊合計 225 格）：不是螢幕，蓋掉的話支架會不見。
    /// - 橘色的內陰影線（螢幕凹進外殼的感覺；111 格）：x＝675 的 y＝183～220、y＝183 的 x＝682～743、x＝682 的 y＝227～237。
    /// 形狀寫成常數，不在執行時讀遊戲的圖（貼圖不可讀）。
    /// </summary>
    internal static class TabletGeometry
    {
        internal const int ScreenX = 675, ScreenY = 183, ScreenW = 79, ScreenH = 55;

        /// <summary>支架斜切的第一列（y）；這一列到 <see cref="BracketFullRow"/>-1 每列往內多 1 格，之後到底都一樣寬。</summary>
        const int BracketTopRow = 221, BracketFullRow = 226;
        /// <summary>左下支架在 y＝221 時的右緣（x），之後每列 +1，y≥226 時是 681。</summary>
        const int LeftBracketRightAtTop = 676;
        /// <summary>右下支架在 y＝221 時的左緣（x），之後每列 -1，y≥226 時是 746。</summary>
        const int RightBracketLeftAtTop = 751;

        /// <summary>
        /// 平板圖上的 (x, y)（左上角原點）在小畫面裡要不要透明：下緣兩角的支架、橘色的內陰影線。螢幕範圍以外一律回傳 false。
        /// </summary>
        internal static bool IsTransparent(int x, int y)
        {
            var right = ScreenX + ScreenW - 1;
            var bottom = ScreenY + ScreenH - 1;
            if (x < ScreenX || x > right || y < ScreenY || y > bottom) return false;
            // 下緣兩角的支架
            if (y >= BracketTopRow)
            {
                var step = System.Math.Min(y, BracketFullRow) - BracketTopRow;
                if (x <= LeftBracketRightAtTop + step) return true;
                if (x >= RightBracketLeftAtTop - step) return true;
            }
            // 橘色的內陰影線：左邊一欄（支架以上）、上緣兩個上支架之間那列、左下支架旁那段
            if (x == ScreenX && y < BracketTopRow) return true;
            if (y == ScreenY && x >= 682 && x <= 743) return true;
            if (x == 682 && y >= 227) return true;
            return false;
        }

        /// <summary>
        /// 小畫面（ScreenW×ScreenH，Unity 的排法：橫列優先、由下往上）裡透明那些格的索引。建 Doom 時算一次，
        /// 之後每次縮完小畫面照著清成透明（FrameConvert.ClearPixels，熱路徑不配置記憶體）。
        /// 平板圖的 (x, y) 在小畫面的第 x-ScreenX 欄、由下往上第 ScreenH-1-(y-ScreenY) 列（Unity 的貼圖由下往上）。
        /// </summary>
        internal static int[] TransparentIndices()
        {
            var list = new System.Collections.Generic.List<int>();
            for (var r = 0; r < ScreenH; r++)
            {
                var y = ScreenY + (ScreenH - 1 - r);
                for (var c = 0; c < ScreenW; c++)
                    if (IsTransparent(ScreenX + c, y)) list.Add(r * ScreenW + c);
            }
            return list.ToArray();
        }

        /// <summary>
        /// 螢幕中心在平板物件的本地座標（Unity 單位）：sprite 的 rect 寬高、pivot（像素，左下角為原點，同 Sprite.pivot）、pixelsPerUnit 換算。
        /// 960×540、pivot (480, 270)、ppu 100 時是 (2.345, 0.595)；寬 79、高 55 都是奇數，中心在半格上，四邊剛好落在整數像素。
        /// </summary>
        internal static void ScreenCenter(float rectW, float rectH, float pivotX, float pivotY, float ppu, out float x, out float y)
        {
            var cx = ScreenX + ScreenW / 2f;
            var cyFromTop = ScreenY + ScreenH / 2f;
            var cyFromBottom = rectH - cyFromTop;
            x = (cx - pivotX) / ppu;
            y = (cyFromBottom - pivotY) / ppu;
        }
    }

    /// <summary>
    /// 沒有 WAD 與出錯畫面的版面（需求.md 決定 29）：說明文字、路徑框、「複製路徑」按鈕三件，垂直排、整組在畫面區置中。
    /// 不碰 Unity，離線測試測同一份；字的高度由宿主用 TMP 量好（GetPreferredValues）再交進來。
    ///
    /// 數值是使用者選的示意圖在預設大小的列表區（寬 530 畫布單位）量的。整組一律照這個寬度排（換行的位置固定），
    /// 畫面區比它小（上面有客人資訊時列表區變矮、TabletUI.LayoutSection）就**整組等比縮小**（一個 localScale），
    /// 所以三件的字永遠保持同樣的比例，也不會溢出畫面區、疊在一起；不讓三個 TMP 各自 auto-size（會變成大小不一）。
    /// 面板的三種尺寸（StoreUI.ToggleZoom）是整個視窗縮放，畫布單位不變，不用另外處理。
    /// </summary>
    internal static class MessageLayout
    {
        /// <summary>排版的基準寬度（預設的列表區寬）。</summary>
        internal const float RefWidth = 530f;
        /// <summary>整組四周內縮、三件之間的間距。</summary>
        internal const float Inset = 16f, Gap = 12f;
        /// <summary>說明文字的大小：沒有 WAD、出錯（出錯的字少，放大一點）。</summary>
        internal const float NoWadTextSize = 24f, ErrorTextSize = 28f;
        /// <summary>路徑框：框裡的字、內距（上下、左右）、邊框寬。</summary>
        internal const float PathTextSize = 19f, BoxPadV = 8f, BoxPadH = 12f, Border = 2f;
        /// <summary>按鈕：字、內距（上下、左右）；邊框寬同路徑框。</summary>
        internal const float ButtonTextSize = 18f, ButtonPadV = 4f, ButtonPadH = 16f;

        /// <summary>說明文字與路徑框的寬（基準寬扣掉左右內縮）。</summary>
        internal const float ContentWidth = RefWidth - 2 * Inset;
        /// <summary>路徑框裡的字能用的寬（扣掉邊框與左右內距）。</summary>
        internal const float PathTextWidth = ContentWidth - 2 * Border - 2 * BoxPadH;

        /// <summary>排好的結果：位置都是從整組的左上角往下量（畫布單位、縮放之前），整組再乘 Scale。</summary>
        internal struct Result
        {
            /// <summary>整組的縮放（0～1；畫面區大小是 0 時是 0）。</summary>
            internal float Scale;
            /// <summary>整組縮放之前的寬高。</summary>
            internal float Width, Height;
            /// <summary>說明文字：上緣、高。</summary>
            internal float TextY, TextH;
            /// <summary>路徑框：上緣、高（寬是 ContentWidth）。</summary>
            internal float BoxY, BoxH;
            /// <summary>按鈕：上緣、寬、高（水平置中）；沒有按鈕時寬高是 0。</summary>
            internal float ButtonY, ButtonW, ButtonH;
        }

        /// <summary>
        /// 排版：textH＝說明文字的高（寬 ContentWidth 時）、box＝要不要路徑框、pathH＝框裡路徑的高（寬 PathTextWidth 時）、
        /// button＝要不要按鈕（有框才有）、labelW／labelH＝按鈕字的寬高（「複製路徑」與「已複製」取寬的那個，按了之後按鈕不會變大小）。
        /// 縮放＝min(1, 畫面區寬 ÷ 整組寬, 畫面區高 ÷ 整組高)：放得下就照原大小，放不下整組一起縮。
        /// </summary>
        internal static Result Compute(float areaW, float areaH, float textH, bool box, float pathH, bool button, float labelW, float labelH)
        {
            var r = new Result { Width = RefWidth };
            var y = Inset;
            r.TextY = y;
            r.TextH = textH;
            y += textH;
            if (box)
            {
                y += Gap;
                r.BoxY = y;
                r.BoxH = pathH + 2 * BoxPadV + 2 * Border;
                y += r.BoxH;
            }
            if (box && button)
            {
                y += Gap;
                r.ButtonY = y;
                r.ButtonW = labelW + 2 * ButtonPadH + 2 * Border;
                r.ButtonH = labelH + 2 * ButtonPadV + 2 * Border;
                y += r.ButtonH;
            }
            r.Height = y + Inset;
            if (areaW <= 0 || areaH <= 0) r.Scale = 0f;
            else r.Scale = System.Math.Min(1f, System.Math.Min(areaW / r.Width, areaH / r.Height));
            return r;
        }
    }
}
