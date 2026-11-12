//
// Probably Doom 修改（2026-11-12）：由上游的 Silk/SilkVideo.cs 改寫。上游把畫面傳給 OpenGL 的貼圖畫到視窗，
// 這裡拿掉 OpenGL 的部分，只留「Renderer 畫進一塊 RGBA 緩衝」，由宿主（Unity 的 Texture2D）拿去顯示。
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

namespace ManagedDoom.Video
{
    /// <summary>
    /// 把 Doom 的畫面畫進 Data（RGBA32）的 IVideo。宿主每幀呼叫 Render(doom, frameFrac)，再把 Data 交給貼圖。
    ///
    /// 緩衝的格式（同上游 Renderer.Render）：
    /// - 大小 Width×Height：設定 video_highresolution 是 true 時 640×400，false 時 320×200（建構時決定，之後不變；
    ///   要換解析度就用新的 Config 重建 FrameBufferVideo 與 Doom）。原版的畫素是 320×200 拉成 4:3 顯示（不是正方形畫素）。
    /// - 每個畫素 4 bytes：R、G、B、A（A 一律 255），調色盤、受傷變紅、gamma 都已套好。
    /// - **直行優先**：第 (x, y) 個畫素（x 往右、y 往下，左上角是 (0, 0)）在 Data[4 * (x * Height + y)]。
    ///   所以把 Data 原樣當成「寬 Height、高 Width」的圖就是轉了 90 度的畫面：Unity 端開 Height×Width 的 Texture2D、
    ///   LoadRawTextureData 後，顯示時轉回來（上游 SilkVideo 也是這樣，把貼圖座標對調）。
    /// </summary>
    public sealed class FrameBufferVideo : IVideo
    {
        private Renderer renderer;
        private byte[] data;
        private bool hasFocus;

        public FrameBufferVideo(Config config, GameContent content)
        {
            renderer = new Renderer(config, content);
            data = new byte[4 * renderer.Width * renderer.Height];
            hasFocus = true;
        }

        public void Render(Doom doom, Fixed frameFrac)
        {
            renderer.Render(doom, data, frameFrac);
        }

        public void InitializeWipe()
        {
            renderer.InitializeWipe();
        }

        public bool HasFocus()
        {
            return hasFocus;
        }

        /// <summary>畫面的 RGBA32 緩衝（格式見類別的說明）。Render 每次覆寫同一個陣列。</summary>
        public byte[] Data => data;

        public int Width => renderer.Width;
        public int Height => renderer.Height;

        /// <summary>Doom 用它決定要不要抓滑鼠（IUserInput.GrabMouse）；Probably Doom 不用滑鼠，宿主照需要設。</summary>
        public bool Focused
        {
            get => hasFocus;
            set => hasFocus = value;
        }

        public int WipeBandCount => renderer.WipeBandCount;
        public int WipeHeight => renderer.WipeHeight;

        public int MaxWindowSize => renderer.MaxWindowSize;

        public int WindowSize
        {
            get => renderer.WindowSize;
            set => renderer.WindowSize = value;
        }

        public bool DisplayMessage
        {
            get => renderer.DisplayMessage;
            set => renderer.DisplayMessage = value;
        }

        public int MaxGammaCorrectionLevel => renderer.MaxGammaCorrectionLevel;

        public int GammaCorrectionLevel
        {
            get => renderer.GammaCorrectionLevel;
            set => renderer.GammaCorrectionLevel = value;
        }
    }
}
