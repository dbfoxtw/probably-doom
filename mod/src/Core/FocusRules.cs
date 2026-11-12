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
    /// <summary>
    /// 鍵盤給不給 Doom（需求.md 決定 10、11、20）。不碰 Unity，離線測試測同一份。
    /// 「有焦點」＝Doom 在跑（每個 tic 呼叫 Update）、鍵盤給 Doom、遊戲的快捷鍵被擋；沒焦點時 Doom 停在最後一格、聲音暫停。
    /// </summary>
    internal static class FocusRules
    {
        /// <summary>這一幀的狀況（DoomRunner 每幀在 LateUpdate 收集）。</summary>
        internal struct Frame
        {
            /// <summary>Doom 畫面顯示中：在店面場景、交易面板開著、按了 DOOM 切到 Doom 模式。</summary>
            internal bool DoomShown;
            /// <summary>Doom 建好了、沒出錯（沒有 WAD 或出錯停掉時是 false）。</summary>
            internal bool Running;
            /// <summary>這一幀按下了任一個滑鼠鍵（左、右、中）。</summary>
            internal bool MouseDown;
            /// <summary>這一幀的按下落在 Doom 畫面上（EventSystem 把按下交給最上層的介面，被別的視窗蓋住時不算）。</summary>
            internal bool PointerDownOnScreen;
            /// <summary>Doom 選單選了 Quit Game（這一幀的 tic 裡觸發的 QuitRequested）。</summary>
            internal bool QuitRequested;
        }

        /// <summary>
        /// 下一幀有沒有焦點：
        /// - 沒顯示 Doom（切回結帳、關面板、離開店面場景）、Doom 沒在跑、選了 Quit Game：沒有。
        /// - 這一幀按了滑鼠：點在 Doom 畫面上就有，點別處（物品、別的視窗、DOOM 按鈕）就沒有。
        /// - 其他時候照舊。
        /// </summary>
        internal static bool Next(bool focused, Frame f)
        {
            if (!f.DoomShown || !f.Running || f.QuitRequested) return false;
            if (f.PointerDownOnScreen) return true;
            if (f.MouseDown) return false;
            return focused;
        }
    }

    /// <summary>擋遊戲快捷鍵的判斷（Patches.cs 的掛點用；需求.md 決定 10，做法照簡易待辦）。KeyCode 用數值傳進來，不碰 Unity。</summary>
    internal static class HotkeyRules
    {
        /// <summary>KeyCode 的數值：None＝0，滑鼠鍵是 Mouse0（323）～Mouse6（329）。</summary>
        internal const int None = 0, Mouse0 = 323, Mouse6 = 329;

        internal static bool IsMouse(int keyCode) => keyCode >= Mouse0 && keyCode <= Mouse6;

        /// <summary>
        /// 遊戲的按鍵綁定（KeyBindHelper.GetKeyDown／GetKey）要不要回 false：Doom 有焦點、而且綁的是鍵盤鍵時擋。
        /// 滑鼠鍵（右鍵的物品選單、中鍵翻轉）不擋：滑鼠一直是遊戲的，點別處就把焦點還給遊戲。沒綁（None）原本就回 false，不管。
        /// </summary>
        internal static bool BlockBinding(int keyCode, bool focused) => focused && keyCode != None && !IsMouse(keyCode);

        /// <summary>
        /// 遊戲的「正在打字」（StoreUIManager.IsTextFieldFocused）要不要改成 true：有焦點、而且這一幀沒按滑鼠時才改。
        /// 點別處把焦點還給遊戲的那一下，遊戲在「正在打字」的幀會整段跳過釘選、漏斗、儲物協調器的挑選（簡易待辦查過），
        /// 改了那一下會被吃掉、要點第二次；按住的鍵在那一幀照樣被按鍵綁定的掛點擋住。
        /// </summary>
        internal static bool OverrideTyping(bool focused, bool mouseDownThisFrame) => focused && !mouseDownThisFrame;
    }

    /// <summary>
    /// Doom 的 35 Hz 計時（GameConst.TicRate）：每幀加上經過的時間，滿 1/35 秒跑一個 tic。不碰 Unity，離線測試測同一份。
    /// 一幀最多補 MaxPerFrame 個 tic：遊戲卡了一下（讀檔、換場景）回來時，不一口氣補跑一堆（Doom 只會慢一點，不會越追越卡）。
    /// </summary>
    internal sealed class TicClock
    {
        internal const int TicRate = 35;
        internal const int MaxPerFrame = 3;

        double _acc;

        /// <summary>重新起算（取得焦點時：暫停期間的時間不算）。</summary>
        internal void Reset() => _acc = 0;

        /// <summary>經過 seconds 秒，這一幀要跑幾個 tic。</summary>
        internal int Advance(double seconds)
        {
            if (seconds > 0) _acc += seconds;
            var n = (int)System.Math.Floor(_acc * TicRate + 1e-9);
            if (n > MaxPerFrame)
            {
                _acc = 0;
                return MaxPerFrame;
            }
            _acc -= (double)n / TicRate;
            if (_acc < 0) _acc = 0;
            return n;
        }
    }
}
