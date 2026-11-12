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
    /// 平板本體（店裡右邊桌上那台，場景的 NegociationTablet）的螢幕位置（需求.md 決定 6）。不碰 Unity，離線測試測同一份。
    /// 平板的圖是一整張 960×540 的圖層 negociation_tablet（pivot 中心、100 像素一單位），
    /// 螢幕在圖上（左上角為原點）(676,183)～(754,238)，78×55 遊戲像素（平常畫著 Useful info）。
    /// </summary>
    internal static class TabletGeometry
    {
        internal const int ScreenX = 676, ScreenY = 183, ScreenW = 78, ScreenH = 55;

        /// <summary>
        /// 螢幕中心在平板物件的本地座標（Unity 單位）：sprite 的 rect 寬高、pivot（像素，左下角為原點，同 Sprite.pivot）、pixelsPerUnit 換算。
        /// 960×540、pivot (480, 270)、ppu 100 時是 (2.35, 0.595)。
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
}
