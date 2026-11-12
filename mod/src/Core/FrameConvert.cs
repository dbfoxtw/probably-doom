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

using System;

namespace ProbablyDoom
{
    /// <summary>
    /// 把 Doom 的畫面緩衝轉成 Unity 貼圖要的排法，以及平板本體的 79×55 小畫面（需求.md 決定 1、4、6、28）。不碰 Unity，離線測試測同一份。
    ///
    /// 畫素一律用 uint 表示一個 RGBA32 畫素（記憶體裡的位元組順序 R、G、B、A，所以在小端序的 uint 裡是 A&lt;&lt;24 | B&lt;&lt;16 | G&lt;&lt;8 | R）：
    /// 引擎的 byte[] 用 Buffer.BlockCopy 整塊搬進 uint[]，轉完再用 Texture2D.SetPixelData 原樣交給貼圖，一個畫素一次搬 4 bytes。
    /// 不用 unsafe 與指標（遊戲的 mod 掃描不准）。
    /// - 引擎（FrameBufferVideo.Data）：**直行優先**，畫素 (x, y)（左上角是原點、y 往下）在 x * height + y。
    /// - Unity 的 Texture2D：**橫列優先、由下往上**，畫素 (x, 列 r)（左下角是原點）在 r * width + x。
    /// </summary>
    internal static class FrameConvert
    {
        /// <summary>不透明的黑（R=G=B=0、A=255）。</summary>
        internal const uint Black = 0xFF000000u;
        /// <summary>透明（alpha 0）：平板本體的小畫面上要露出原圖的格子（TabletGeometry.IsTransparent）。</summary>
        internal const uint Transparent = 0x00000000u;

        // Thumbnail 每一欄的累加用：每兩個 tic 縮一次，不要每次 new（熱路徑不配置，免得 GC 在玩的時候頓一下）。
        // 只有主執行緒呼叫（DoomSession.Render；離線測試也是單執行緒），不夠大時才換大的。
        static int[] _sumR = new int[0], _sumG = new int[0], _sumB = new int[0], _count = new int[0];

        /// <summary>
        /// 引擎的直行優先轉成 Unity 的橫列優先、由下往上：Doom 的 (x, y) → 貼圖的 (x, height-1-y)。
        /// src、dst 都要至少 width*height 個；兩個不能是同一個陣列。順著 src 讀（一行一行），寫的時候跳著寫。
        /// </summary>
        internal static void ToUnityRows(uint[] src, int width, int height, uint[] dst)
        {
            var s = 0;
            for (var x = 0; x < width; x++)
            {
                // y = 0（最上面）放到貼圖的最後一列
                var d = (height - 1) * width + x;
                for (var y = 0; y < height; y++)
                {
                    dst[d] = src[s++];
                    d -= width;
                }
            }
        }

        /// <summary>
        /// 縮圖裡 Doom 畫面佔的寬高：Doom 的畫面是 4:3（原版 320×200 的畫素不是正方形，拉成 4:3 顯示），
        /// 放進 tw×th 時保持 4:3、能多大就多大，剩下的留黑邊（79×55 → 73×55，左右各 3 欄黑邊）。
        /// </summary>
        internal static void ContentSize(int tw, int th, out int cw, out int ch)
        {
            if (tw * 3 >= th * 4)
            {
                ch = th;
                cw = Math.Min(tw, (int)Math.Round(th * 4 / 3.0));
            }
            else
            {
                cw = tw;
                ch = Math.Min(th, (int)Math.Round(tw * 3 / 4.0));
            }
        }

        /// <summary>
        /// 平板本體的小畫面：Doom 的畫面（直行優先，width×height）縮成 tw×th（Unity 的排法：橫列優先、由下往上），
        /// 4:3 置中、其餘塗黑。每個小畫素是它涵蓋的原畫素的平均（box filter；只取一點的話細線與文字會閃）。
        /// 每個原畫素剛好算進一個小畫素（邊界用整數除法切）。dst 要至少 tw*th 個。
        /// </summary>
        internal static void Thumbnail(uint[] src, int width, int height, uint[] dst, int tw, int th)
        {
            ContentSize(tw, th, out var cw, out var ch);
            var ox = (tw - cw) / 2;
            var oy = (th - ch) / 2;

            for (var i = 0; i < tw * th; i++) dst[i] = Black;

            if (_count.Length < ch)
            {
                _sumR = new int[ch];
                _sumG = new int[ch];
                _sumB = new int[ch];
                _count = new int[ch];
            }
            var sumR = _sumR;
            var sumG = _sumG;
            var sumB = _sumB;
            var count = _count;

            for (var i = 0; i < cw; i++)
            {
                var x0 = i * width / cw;
                var x1 = (i + 1) * width / cw;
                Array.Clear(sumR, 0, ch);
                Array.Clear(sumG, 0, ch);
                Array.Clear(sumB, 0, ch);
                Array.Clear(count, 0, ch);

                for (var x = x0; x < x1; x++)
                {
                    var col = x * height;
                    for (var j = 0; j < ch; j++)
                    {
                        var y0 = j * height / ch;
                        var y1 = (j + 1) * height / ch;
                        int r = 0, g = 0, b = 0;
                        for (var y = y0; y < y1; y++)
                        {
                            var p = src[col + y];
                            r += (int)(p & 0xFF);
                            g += (int)((p >> 8) & 0xFF);
                            b += (int)((p >> 16) & 0xFF);
                        }
                        sumR[j] += r;
                        sumG[j] += g;
                        sumB[j] += b;
                        count[j] += y1 - y0;
                    }
                }

                for (var j = 0; j < ch; j++)
                {
                    var n = count[j];
                    if (n == 0) continue;
                    var r = (uint)((sumR[j] + n / 2) / n);
                    var g = (uint)((sumG[j] + n / 2) / n);
                    var b = (uint)((sumB[j] + n / 2) / n);
                    // 小畫面的第 j 列（由上往下）是貼圖的第 th-1-(oy+j) 列
                    var row = th - 1 - (oy + j);
                    dst[row * tw + ox + i] = 0xFF000000u | (b << 16) | (g << 8) | r;
                }
            }
        }

        /// <summary>
        /// 把 indices 列的格子清成透明（平板本體的小畫面：支架與橘色陰影線的地方露出原圖，TabletGeometry.TransparentIndices）。
        /// 每次縮完小畫面都要做（Thumbnail 會把整張塗滿）；只走現成的陣列，不配置記憶體。
        /// </summary>
        internal static void ClearPixels(uint[] dst, int[] indices)
        {
            for (var i = 0; i < indices.Length; i++) dst[indices[i]] = Transparent;
        }

        /// <summary>組一個不透明的畫素（測試與畫面以外的地方用）。</summary>
        internal static uint Rgb(byte r, byte g, byte b) => 0xFF000000u | ((uint)b << 16) | ((uint)g << 8) | r;
    }
}
