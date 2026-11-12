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
            /// <summary>
            /// 遊戲視窗有焦點（Application.isFocused）。玩家 Alt-Tab 出去時遊戲在背景照跑（只降 fps），
            /// Doom 不能跟著繼續跑、也不能讓按著的鍵留著（切出去時放開的鍵 Unity 收不到）。
            /// </summary>
            internal bool AppFocused;
        }

        /// <summary>
        /// 下一幀有沒有焦點：
        /// - 沒顯示 Doom（切回結帳、關面板、離開店面場景）、Doom 沒在跑、選了 Quit Game、遊戲視窗沒有焦點（切到別的程式）：沒有。
        ///   切回遊戲後不會自己拿回焦點，要再點一下 Doom 畫面（和點別處再點回來一樣）。
        /// - 這一幀按了滑鼠：點在 Doom 畫面上就有，點別處（物品、別的視窗、DOOM 按鈕）就沒有。
        /// - 其他時候照舊。
        /// </summary>
        internal static bool Next(bool focused, Frame f)
        {
            if (!f.DoomShown || !f.Running || f.QuitRequested || !f.AppFocused) return false;
            if (f.PointerDownOnScreen) return true;
            if (f.MouseDown) return false;
            return focused;
        }
    }

    /// <summary>
    /// 滑鼠左鍵在 Doom 畫面上開火（需求.md 決定 26）。不碰 Unity，離線測試測同一份。
    /// Ctrl 連按會叫出 Windows「按 Ctrl 鍵時顯示指標位置」的圈圈，所以多給左鍵開火；滑鼠照舊不轉向。
    /// - 按下：左鍵在 Doom 畫面區（ScreenInput 掛的黑底，4:3 畫面連左右黑邊）按下，而且**按下之前就有焦點**才算。
    ///   拿焦點的那一下（按下時 Doom 還沒有焦點）不開火，按著也不開火。「之前」＝這一幀算焦點之前的狀態：
    ///   EventSystem 在它的 Update 送按下，DoomRunner 在 LateUpdate 才算焦點。
    /// - 按著：每幀看左鍵還按著沒有（Input.GetMouseButton(0)），放開就停，在哪裡放開都算（拖出畫面照樣開火到放開）。
    /// - 至少一個 tic：每次有效的按下都回傳 true，DoomKeyboard 把它鎖存到 tic 跑過才清。35 Hz 的 tic 比畫面的幀慢，
    ///   按下又放開可能落在兩個 tic 之間（同一幀內，或第 A 幀按下、第 B 幀放開、中間都沒跑到 tic），只看「按著」的話會漏掉一槍。
    ///   鍵盤沒有這種跨幀的鎖存（鍵盤只補同一幀內的 KeyDown／KeyUp 事件），這是滑鼠開火另外加的。
    /// - 失去焦點就放開（同鍵盤的 ReleaseAll，回來時不會一直開火）。
    /// </summary>
    internal sealed class MouseFire
    {
        /// <summary>現在算按著（開火中）。</summary>
        internal bool Held { get; private set; }

        /// <summary>
        /// 每幀（算完焦點之後）：focusedBefore＝這一幀算焦點之前有沒有焦點、focusedNow＝算完之後、
        /// leftDownOnScreen＝這一幀左鍵在 Doom 畫面區按下、leftHeld＝左鍵現在按著。
        /// 回傳 true＝這一幀有新的有效按下（交給 DoomKeyboard 鎖存，至少開火一個 tic）；長按的行為由 Held 決定，不受影響。
        /// </summary>
        internal bool Update(bool focusedBefore, bool focusedNow, bool leftDownOnScreen, bool leftHeld)
        {
            if (!focusedNow)
            {
                Held = false;
                return false;
            }
            var pressed = leftDownOnScreen && focusedBefore;
            if (pressed) Held = true;
            if (Held && !leftHeld) Held = false;
            return pressed;
        }

        /// <summary>放開（失去焦點、mod 停掉）。</summary>
        internal void Release() => Held = false;
    }

    /// <summary>
    /// 交易面板在 Doom 模式還是結帳畫面（需求.md 決定 2、12、25）。不碰 Unity，離線測試測同一份。
    /// 「Doom 模式」＝按了 DOOM、按鈕顯示成按下去的樣子、列表區換成 Doom 畫面區；焦點另外由 FocusRules 管。
    /// </summary>
    internal static class ModeRules
    {
        /// <summary>按 DOOM 要做什麼。</summary>
        internal enum Press
        {
            /// <summary>從結帳畫面切到 Doom（還沒建就建；沒有 WAD 時停在說明畫面）。</summary>
            EnterDoom,
            /// <summary>切回結帳畫面（Doom 暫停）。</summary>
            LeaveDoom,
            /// <summary>Doom 模式中、還沒有 WAD：先重新找一次，找到就開始 Doom、留在 Doom 模式，還是沒有才切回結帳（<see cref="StayAfterRetry"/>）。</summary>
            RetryWad,
        }

        /// <summary>
        /// 按 DOOM：結帳畫面 → 切到 Doom；Doom 模式中 → 切回結帳，但畫面是「找不到 WAD」的說明時先重新找一次：
        /// 說明寫的是「放好 WAD 再按一次 DOOM」，玩家照做時按的是同一顆按鈕，只切回結帳的話還要再按一次才看得到 Doom。
        /// </summary>
        internal static Press OnDoomButton(bool doomMode, bool noWad) => !doomMode ? Press.EnterDoom : noWad ? Press.RetryWad : Press.LeaveDoom;

        /// <summary>按 DOOM 重新找 WAD 之後：找到（Doom 開始跑）就留在 Doom 模式，還是沒有就切回結帳（按鈕照常是開關）。</summary>
        internal static bool StayAfterRetry(bool found) => found;

        /// <summary>
        /// 點一下 Doom 畫面區要不要重新找 WAD：Doom 模式中、畫面是「找不到 WAD」的說明時。找到了就照常開始（同一下點擊也給焦點，
        /// 點的就是 Doom 畫面）；還是沒有就留在說明畫面。
        /// </summary>
        internal static bool RetryOnScreenClick(bool doomMode, bool noWad, bool pointerDownOnScreen) => doomMode && noWad && pointerDownOnScreen;

        /// <summary>
        /// 這一幀過後還在不在 Doom 模式：面板沒了（離開店面場景）或關掉了（uiObject 變 inactive：關閉按鈕、Esc、遊戲自己關）就回到結帳，
        /// 下次打開平板看到的是結帳畫面（決定 25），要玩再按 DOOM（Doom 照決定 11 暫停著，按了接著玩）。
        /// 只看面板自己的 activeSelf，不看 activeInHierarchy：上層暫時藏起來不算玩家關掉平板。
        /// </summary>
        internal static bool KeepDoomMode(bool doomMode, bool panelAlive, bool panelActiveSelf) => doomMode && panelAlive && panelActiveSelf;
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
