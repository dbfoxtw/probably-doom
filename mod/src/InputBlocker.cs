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
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Doom 有焦點時不讓遊戲收到鍵盤（需求.md 決定 10）。做法照簡易待辦的「打字時擋住遊戲快捷鍵」（monorepo 的 mods/todo/docs/設計.md）：
    /// 遊戲的快捷鍵走自己的 InputActionManager 與 KeyBindHelper（每幀用 Input 查），不經過 UGUI，所以掛遊戲自己的方法：
    /// 「正在打字」（StoreUIManager.IsTextFieldFocused）、按鍵綁定（KeyBindHelper.GetKeyDown／GetKey）、Esc（GetBackDown）、
    /// 手機的小鍵盤（PhoneUIManager.PressKeypad）。
    /// 掛點本身是 Patches.cs 的 [HarmonyPatch]，這裡是內容。遊戲在 Init 之前就掛上，先看 ProbablyDoomMod.Ready，還沒好就放行；
    /// 例外不丟回遊戲（同一個地方只記一次 log，照常執行遊戲的方法）。
    /// Doom 自己讀鍵直接呼叫 Input（UnityKeyboard），不經過這些掛點，不會被自己擋掉；直接呼叫引擎方法不算權限，攔截才算。
    /// </summary>
    static class InputBlocker
    {
        /// <summary>Doom 有沒有焦點：DoomRunner 每幀在 LateUpdate 算完設好，下一幀遊戲的 Update 看到的就是它。</summary>
        internal static bool Focused;

        static int _frame = -1;
        static bool _mouseDown, _mouseError;
        static readonly HashSet<string> _failed = new HashSet<string>();

        /// <summary>除錯統計：真的改了遊戲看到的結果的次數（DoomRunner 每 10 秒寫進 log 後歸零）。</summary>
        internal static long Typing, Bindings, Back, Keypads;

        static bool Active => ProbablyDoomMod.Ready && Focused;

        /// <summary>這一幀有沒有按下滑鼠鍵（每幀只讀一次）。讀不到就當成沒按：最多是點別處的那一下要點第二次，不會漏擋鍵盤。</summary>
        static bool MouseDownThisFrame()
        {
            var f = Time.frameCount;
            if (f == _frame) return _mouseDown;
            _frame = f;
            _mouseDown = false;
            try
            {
                for (var b = 0; b <= 2 && !_mouseDown; b++) _mouseDown = Input.GetMouseButtonDown(b);
            }
            catch (Exception e)
            {
                if (!_mouseError) ProbablyDoomMod.Error($"讀取滑鼠按下失敗：{e}");
                _mouseError = true;
            }
            return _mouseDown;
        }

        static void Fail(string where, Exception e)
        {
            if (!_failed.Add(where)) return;
            ProbablyDoomMod.Error($"擋快捷鍵（{where}）出錯，這裡改成不擋：{e}");
        }

        /// <summary>
        /// StoreUIManager.IsTextFieldFocused 的 postfix：Doom 有焦點時也算遊戲的「正在打字」。只會改成 true（和簡易待辦的同一個 postfix 並存）。
        /// 這一幀按下了滑鼠鍵就不改（HotkeyRules.OverrideTyping）：點別處把焦點還給遊戲的那一下不能被吃掉。
        /// </summary>
        internal static void TextFieldFocused(ref bool result)
        {
            if (result || !Active) return;
            try
            {
                if (!HotkeyRules.OverrideTyping(Focused, MouseDownThisFrame())) return;
                result = true;
                Typing++;
            }
            catch (Exception e) { Fail("正在打字", e); }
        }

        /// <summary>KeyBindHelper.GetKeyDown(id)／GetKey(id) 的 prefix：Doom 有焦點時，綁鍵盤鍵的回 false，滑鼠鍵照常。</summary>
        internal static bool Binding(string id, ref bool result)
        {
            if (!Active) return true;
            try
            {
                var k = KeyBindHelper.Get(id);
                if (!HotkeyRules.BlockBinding((int)k, Focused)) return true;
                if (Input.GetKey(k)) Bindings++;
                result = false;
                return false;
            }
            catch (Exception e)
            {
                Fail("按鍵綁定", e);
                return true;
            }
        }

        /// <summary>KeyBindHelper.GetBackDown 的 prefix：Doom 有焦點時回 false（Esc 給 Doom，不關視窗、不開暫停選單）。</summary>
        internal static bool BackDown(ref bool result)
        {
            if (!Active) return true;
            try
            {
                if (Input.GetKeyDown(KeyCode.Escape)) Back++;
            }
            catch (Exception) { } // 只是統計，讀不到照樣擋
            result = false;
            return false;
        }

        /// <summary>PhoneUIManager.PressKeypad 的 prefix：Doom 有焦點時不執行（小鍵盤的數字給 Doom）。</summary>
        internal static bool Keypad()
        {
            if (!Active) return true;
            Keypads++;
            return false;
        }

        internal static string TakeStats()
        {
            var s = $"擋快捷鍵：正在打字 {Typing} 次、按鍵綁定 {Bindings} 次、Esc {Back} 次、小鍵盤 {Keypads} 次";
            Typing = Bindings = Back = Keypads = 0;
            return s;
        }
    }
}
