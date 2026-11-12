// Probably Doom
// Copyright (C) 2026 dbfoxtw
// 鍵的對應表照 ManagedDoom 上游的 Silk/SilkUserInput.cs（SilkToDoom）改寫（Copyright (C) 2019-2020 Nobuaki Tanaka，GPL v2 or later），
// Silk.NET 的 Key 換成 Unity 的 KeyCode。
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
    using ManagedDoom;
    using UnityEngine;
    using DoomEventType = ManagedDoom.EventType;

    /// <summary>
    /// Doom 有焦點時每幀讀鍵盤（直接呼叫 UnityEngine.Input：直接呼叫引擎的方法不算權限，攔截才算），
    /// 照上游 SilkUserInput 的對應表把鍵的按下、放開送成 DoomEvent，按著的鍵記在 DoomKeyboard（BuildTicCmd 用）。
    /// 用「這一幀按著沒有」判斷（GetKey），再加上 GetKeyDown 補一幀內按下又放開的短按：比只看 GetKeyDown／GetKeyUp 穩，
    /// 漏掉的放開（例如切出遊戲視窗）下一幀就補上，角色不會一直走。
    /// </summary>
    sealed class UnityKeyboard
    {
        /// <summary>Unity 的鍵 → Doom 的鍵（上游 SilkToDoom 的對應，註解掉的上游也沒有；左右 Alt 是我們拿掉的）。</summary>
        static readonly (KeyCode Unity, DoomKey Doom)[] Map =
        {
            (KeyCode.Space, DoomKey.Space),
            (KeyCode.Comma, DoomKey.Comma),
            (KeyCode.Minus, DoomKey.Subtract),
            (KeyCode.Period, DoomKey.Period),
            (KeyCode.Slash, DoomKey.Slash),
            (KeyCode.Alpha0, DoomKey.Num0),
            (KeyCode.Alpha1, DoomKey.Num1),
            (KeyCode.Alpha2, DoomKey.Num2),
            (KeyCode.Alpha3, DoomKey.Num3),
            (KeyCode.Alpha4, DoomKey.Num4),
            (KeyCode.Alpha5, DoomKey.Num5),
            (KeyCode.Alpha6, DoomKey.Num6),
            (KeyCode.Alpha7, DoomKey.Num7),
            (KeyCode.Alpha8, DoomKey.Num8),
            (KeyCode.Alpha9, DoomKey.Num9),
            (KeyCode.Semicolon, DoomKey.Semicolon),
            (KeyCode.Equals, DoomKey.Equal),
            (KeyCode.A, DoomKey.A),
            (KeyCode.B, DoomKey.B),
            (KeyCode.C, DoomKey.C),
            (KeyCode.D, DoomKey.D),
            (KeyCode.E, DoomKey.E),
            (KeyCode.F, DoomKey.F),
            (KeyCode.G, DoomKey.G),
            (KeyCode.H, DoomKey.H),
            (KeyCode.I, DoomKey.I),
            (KeyCode.J, DoomKey.J),
            (KeyCode.K, DoomKey.K),
            (KeyCode.L, DoomKey.L),
            (KeyCode.M, DoomKey.M),
            (KeyCode.N, DoomKey.N),
            (KeyCode.O, DoomKey.O),
            (KeyCode.P, DoomKey.P),
            (KeyCode.Q, DoomKey.Q),
            (KeyCode.R, DoomKey.R),
            (KeyCode.S, DoomKey.S),
            (KeyCode.T, DoomKey.T),
            (KeyCode.U, DoomKey.U),
            (KeyCode.V, DoomKey.V),
            (KeyCode.W, DoomKey.W),
            (KeyCode.X, DoomKey.X),
            (KeyCode.Y, DoomKey.Y),
            (KeyCode.Z, DoomKey.Z),
            (KeyCode.LeftBracket, DoomKey.LBracket),
            (KeyCode.Backslash, DoomKey.Backslash),
            (KeyCode.RightBracket, DoomKey.RBracket),
            (KeyCode.Escape, DoomKey.Escape),
            (KeyCode.Return, DoomKey.Enter),
            (KeyCode.Tab, DoomKey.Tab),
            (KeyCode.Backspace, DoomKey.Backspace),
            (KeyCode.Insert, DoomKey.Insert),
            (KeyCode.Delete, DoomKey.Delete),
            (KeyCode.RightArrow, DoomKey.Right),
            (KeyCode.LeftArrow, DoomKey.Left),
            (KeyCode.DownArrow, DoomKey.Down),
            (KeyCode.UpArrow, DoomKey.Up),
            (KeyCode.PageUp, DoomKey.PageUp),
            (KeyCode.PageDown, DoomKey.PageDown),
            (KeyCode.Home, DoomKey.Home),
            (KeyCode.End, DoomKey.End),
            (KeyCode.Pause, DoomKey.Pause),
            (KeyCode.F1, DoomKey.F1),
            (KeyCode.F2, DoomKey.F2),
            (KeyCode.F3, DoomKey.F3),
            (KeyCode.F4, DoomKey.F4),
            (KeyCode.F5, DoomKey.F5),
            (KeyCode.F6, DoomKey.F6),
            (KeyCode.F7, DoomKey.F7),
            (KeyCode.F8, DoomKey.F8),
            (KeyCode.F9, DoomKey.F9),
            (KeyCode.F10, DoomKey.F10),
            (KeyCode.F11, DoomKey.F11),
            (KeyCode.F12, DoomKey.F12),
            (KeyCode.F13, DoomKey.F13),
            (KeyCode.F14, DoomKey.F14),
            (KeyCode.F15, DoomKey.F15),
            (KeyCode.Keypad0, DoomKey.Numpad0),
            (KeyCode.Keypad1, DoomKey.Numpad1),
            (KeyCode.Keypad2, DoomKey.Numpad2),
            (KeyCode.Keypad3, DoomKey.Numpad3),
            (KeyCode.Keypad4, DoomKey.Numpad4),
            (KeyCode.Keypad5, DoomKey.Numpad5),
            (KeyCode.Keypad6, DoomKey.Numpad6),
            (KeyCode.Keypad7, DoomKey.Numpad7),
            (KeyCode.Keypad8, DoomKey.Numpad8),
            (KeyCode.Keypad9, DoomKey.Numpad9),
            (KeyCode.KeypadDivide, DoomKey.Divide),
            (KeyCode.KeypadMultiply, DoomKey.Multiply),
            (KeyCode.KeypadMinus, DoomKey.Subtract),
            (KeyCode.KeypadPlus, DoomKey.Add),
            (KeyCode.KeypadEnter, DoomKey.Enter),
            (KeyCode.KeypadEquals, DoomKey.Equal),
            (KeyCode.LeftShift, DoomKey.LShift),
            (KeyCode.LeftControl, DoomKey.LControl),
            (KeyCode.RightShift, DoomKey.RShift),
            (KeyCode.RightControl, DoomKey.RControl),
            // 左右 Alt 不給 Doom（上游有）：按著 Alt 再按 F4 會變成 Alt+F4 關掉整個遊戲（DoomKeyboard.Ignored）。F4 本身照常給
            (KeyCode.Menu, DoomKey.Menu),
        };

        readonly DoomKeyboard _keys;
        /// <summary>每個 Unity 鍵這邊記的「按著」（和 Map 同順序）。</summary>
        readonly bool[] _down = new bool[Map.Length];

        internal UnityKeyboard(DoomKeyboard keys)
        {
            _keys = keys;
        }

        /// <summary>有焦點時每幀呼叫一次：讀每個鍵，按下、放開送給 Doom。</summary>
        internal void Poll(Doom doom)
        {
            for (var i = 0; i < Map.Length; i++)
            {
                var kc = Map[i].Unity;
                var held = Input.GetKey(kc);
                if (!_down[i] && (held || Input.GetKeyDown(kc)))
                {
                    _down[i] = true;
                    if (_keys.Press(Map[i].Doom)) doom.PostEvent(new DoomEvent(DoomEventType.KeyDown, Map[i].Doom));
                }
                if (_down[i] && !held)
                {
                    _down[i] = false;
                    if (_keys.Release(Map[i].Doom)) doom.PostEvent(new DoomEvent(DoomEventType.KeyUp, Map[i].Doom));
                }
            }
        }

        /// <summary>失去焦點時：按著的全部放開、送 KeyUp（Doom 下次 Update 時收到），免得回來時角色一直走、一直開火。</summary>
        internal void ReleaseAll(Doom doom)
        {
            Array.Clear(_down, 0, _down.Length);
            foreach (var k in _keys.ReleaseAll())
                doom.PostEvent(new DoomEvent(DoomEventType.KeyUp, k));
        }
    }
}
