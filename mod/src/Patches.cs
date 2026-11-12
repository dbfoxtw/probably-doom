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
    using HarmonyLib;
    using UnityEngine;

    // 攔截都用 [HarmonyPatch] 屬性，集中在這個檔：遊戲的 mod 載入器在 Init 之前就用自己的 Harmony（id 是 mod.dbfoxtw.probably-doom）PatchAll。
    // 目標全部是 Assembly-CSharp 裡的方法（遊戲 12-10 的安全更新起，攔截遊戲以外的方法，例如 UnityEngine.Input，算 reflection-external，
    // 不宣告權限就不能載入；這個 mod 一個權限都不宣告）。private 的用字串名並寫明參數型別；目標與參數由 monorepo 根目錄的 tools/modscan 離線確認。
    // 參數用位置（__0）或私有欄位（___名稱），不依賴遊戲的參數名稱。
    //
    // 每個類別都有 [HarmonyCleanup]：掛不上（例如遊戲更新後方法改名）時記下來、吞掉例外。
    // 不吞的話遊戲的 PatchAll 丟例外，整個 mod 不能啟用；吞了只是少那一個攔截（Init 後寫進 log，見 ProbablyDoomMod.LogPatches）。
    // 掛點的內容一開頭先看 ProbablyDoomMod.Ready（Init 之前一律放行），例外不丟回遊戲。

    static class Patches
    {
        /// <summary>預期掛上的方法數：交易面板建好 1，擋快捷鍵 5（IsTextFieldFocused、GetKeyDown、GetKey、GetBackDown、PressKeypad）。</summary>
        internal const int Expected = 6;

        /// <summary>掛不上的攔截（Cleanup 吞掉的例外）：PatchAll 在 Init 之前，那時還沒有 log，先記著。</summary>
        internal static readonly List<string> Failures = new List<string>();

        /// <summary>[HarmonyCleanup]：掛不上的記下來，回傳 null＝不把例外丟回遊戲的 PatchAll（成功時 ex 是 null，什麼都不做）。</summary>
        internal static Exception Cleanup(string what, Exception ex)
        {
            if (ex == null) return null;
            var inner = ex is HarmonyException && ex.InnerException != null ? ex.InnerException : ex;
            Failures.Add($"{what}：{inner.GetType().Name}: {inner.Message}");
            return null;
        }
    }

    // ── 交易面板（需求.md 決定 1～5） ──

    /// <summary>
    /// 交易面板建好之後（NegociationUIManager.Start 裡的 private CreateWindow，每次載入店面場景一次）：接上 DOOM 按鈕與 Doom 畫面。
    /// 視窗的 frame（NegotiationTabletFrame）與 uiObject 是 private 欄位，用 Harmony 的 ___ 參數拿，不用反射。
    /// 建視窗失敗時遊戲把 frame 留成 null（預製物壞了），那時不接。
    /// </summary>
    [HarmonyPatch(typeof(NegociationUIManager), "CreateWindow")]
    static class CreateWindowPatch
    {
        static void Postfix(NegotiationTabletFrame ___frame, GameObject ___uiObject) => DoomRunner.OnWindowCreated(___frame, ___uiObject);
        [HarmonyCleanup] static Exception Cleanup(Exception ex) => Patches.Cleanup("NegociationUIManager.CreateWindow", ex);
    }

    // ── 擋遊戲的快捷鍵（需求.md 決定 10；照簡易待辦的掛點，InputBlocker）。prefix 回傳 false＝不執行原本的方法，直接用 __result ──

    /// <summary>
    /// 遊戲的「正在打字」：Doom 有焦點時也算。IsAnyTyping、IsTyping 都經過它，物品操作的鍵、操作提示、丟棄、工具箱、
    /// 選項這些不動作；看它的 mod（一鍵關窗、視窗群組、物品速覽）也不動作。只會改成 true，和簡易待辦的同一個 postfix 並存。
    /// </summary>
    [HarmonyPatch(typeof(StoreUIManager), nameof(StoreUIManager.IsTextFieldFocused))]
    static class TextFieldFocusedPatch
    {
        static void Postfix(ref bool __result) => InputBlocker.TextFieldFocused(ref __result);
        [HarmonyCleanup] static Exception Cleanup(Exception ex) => Patches.Cleanup("StoreUIManager.IsTextFieldFocused", ex);
    }

    /// <summary>可改綁的鍵按下（空白鍵推進對話、B 叫下一位、1～9 工具箱…）。</summary>
    [HarmonyPatch(typeof(KeyBindHelper), nameof(KeyBindHelper.GetKeyDown), typeof(string))]
    static class BindingDownPatch
    {
        static bool Prefix(string __0, ref bool __result) => InputBlocker.Binding(__0, ref __result);
        [HarmonyCleanup] static Exception Cleanup(Exception ex) => Patches.Cleanup("KeyBindHelper.GetKeyDown", ex);
    }

    /// <summary>可改綁的鍵按住（快速移動的 Ctrl／Shift／Alt 這類）。</summary>
    [HarmonyPatch(typeof(KeyBindHelper), nameof(KeyBindHelper.GetKey), typeof(string))]
    static class BindingHeldPatch
    {
        static bool Prefix(string __0, ref bool __result) => InputBlocker.Binding(__0, ref __result);
        [HarmonyCleanup] static Exception Cleanup(Exception ex) => Patches.Cleanup("KeyBindHelper.GetKey", ex);
    }

    /// <summary>Esc（遊戲的 Esc 不看打字：關最上層視窗或打開暫停選單）。Doom 有焦點時 Esc 給 Doom 開它自己的選單。</summary>
    [HarmonyPatch(typeof(KeyBindHelper), nameof(KeyBindHelper.GetBackDown))]
    static class BackDownPatch
    {
        static bool Prefix(ref bool __result) => InputBlocker.BackDown(ref __result);
        [HarmonyCleanup] static Exception Cleanup(Exception ex) => Patches.Cleanup("KeyBindHelper.GetBackDown", ex);
    }

    /// <summary>手機面板開著時的小鍵盤 0～9（只有 PhoneUIManager.Update 讀小鍵盤時呼叫）。private，用字串名。</summary>
    [HarmonyPatch(typeof(PhoneUIManager), "PressKeypad", typeof(int))]
    static class KeypadPatch
    {
        static bool Prefix() => InputBlocker.Keypad();
        [HarmonyCleanup] static Exception Cleanup(Exception ex) => Patches.Cleanup("PhoneUIManager.PressKeypad", ex);
    }
}
