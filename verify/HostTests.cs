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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ManagedDoom;
using ManagedDoom.Video;

namespace ProbablyDoom.Verify
{
    /// <summary>
    /// mod 宿主層裡不碰 Unity 的純邏輯（mod\src\Core\，和 mod 同一份原始碼）。
    /// Unity 那一層（按鈕、AudioSource、Input、ModHelper）離線測不到，要實機測。
    /// </summary>
    internal static partial class Program
    {
        static void RunHostTests()
        {
            Run("7a. WAD 挑選、存檔路徑", TestWadPicker);
            Run("7b. 畫面轉成貼圖的排法（直行優先 → 橫列優先、由下往上）", TestToUnityRows);
            Run("7c. 平板本體的小畫面（79×55，4:3、box filter）", TestThumbnail);
            Run("7d. 焦點規則", TestFocus);
            Run("7e. 擋快捷鍵的判斷", TestHotkeys);
            Run("7f. 35 Hz 計時", TestTicClock);
            Run("7g. 語言與畫面上的字", TestStrings);
            Run("7h. DOOM 按鈕的位置與三段拼法、畫面 4:3、平板本體的螢幕位置", TestLayout);
            Run("7i. 鍵盤（按著的鍵、BuildTicCmd、全部放開）", TestKeyboard);
            Run("7j. 不給 Doom 的 Alt、新的 doom.cfg 的側移鍵", TestAltAndNewConfig);
            Run("7k. Doom 模式（DOOM 按鈕、找不到 WAD 時重新找、關掉平板回到結帳）", TestModeRules);
            Run("7l. 說明畫面的路徑（ModData 資料夾、Player.log；%USERPROFILE% 開頭、不是時退回完整路徑）", TestDisplayPath);
            Run("7m. 熱路徑不配置記憶體（小畫面、透明格、BuildTicCmd）", TestNoAllocations);
            Run("7n. 平板本體的小畫面的透明格（下緣兩角的支架、橘色陰影線）", TestThumbMask);
            Run("7o. 滑鼠左鍵開火（按下時有焦點才算、放開就停、按下至少一個 tic、失去焦點）", TestMouseFire);
            Run("7s. 背景音樂靜音的記錄（同一個來源只記一次、只還原一次）", TestMuteLedger);
            Run("7p. 說明與路徑的換行標記（<nobr>＋零寬空白）", TestMarkup);
            Run("7q. 沒有 WAD／出錯畫面的版面（整組等比縮小）", TestMessageLayout);
            Run("7r. 引擎的訊息（EngineLog）", TestEngineLog);
        }

        static void TestWadPicker()
        {
            var files = new[]
            {
                "doom.cfg", "freedoom1.wad", "saves/doom2/doomsav0.dsg", "sub/DOOM2.WAD", "mywad.wad", "DOOM1.WAD", "Doom2.wad",
                "readme.txt", "TNT.WAD", "freedoom2.WAD", "doomu.wad",
            };
            var found = WadPicker.FindIwads(files);
            var expect = new[] { "Doom2.wad", "doomu.wad", "DOOM1.WAD", "freedoom2.WAD", "freedoom1.wad", "TNT.WAD" };
            Check(found.SequenceEqual(expect), $"找到的 IWAD 照自動的順序：{string.Join("、", found)}（子資料夾裡的、mywad.wad 不算）");

            var withDoom = WadPicker.FindIwads(new[] { "doomu.wad", "DOOM.WAD", "plutonia.wad" });
            Check(withDoom.SequenceEqual(new[] { "DOOM.WAD", "doomu.wad", "plutonia.wad" }), $"DOOM 排在 doomu 前面、plutonia 最後：{string.Join("、", withDoom)}");

            Check(WadPicker.Choose(found, WadPicker.Auto, out var fb1) == "Doom2.wad" && !fb1, "Auto → 順序最前面的 Doom2.wad");
            Check(WadPicker.Choose(found, "", out var fb0) == "Doom2.wad" && !fb0, "空字串也當 Auto");
            Check(WadPicker.Choose(found, "freedoom1.WAD", out var fb2) == "freedoom1.wad" && !fb2, "指定檔名（不分大小寫）→ freedoom1.wad");
            Check(WadPicker.Choose(found, "DOOM.WAD", out var fb3) == "Doom2.wad" && fb3, "指定的檔不見了 → 退回自動，fellBack");
            Check(WadPicker.Choose(Array.Empty<string>(), WadPicker.Auto, out _) == null, "一個都沒有 → null");

            Check(WadPicker.EngineFileName("doomu.wad") == "DOOM.WAD" && WadPicker.EngineFileName("DOOM2.WAD") == "DOOM2.WAD",
                  "doomu.wad 交給引擎時換成 DOOM.WAD（引擎靠檔名判斷模式，不認得 doomu），其他照原名");
            var empty = new byte[12];
            System.Text.Encoding.ASCII.GetBytes("IWAD").CopyTo(empty, 0);
            BitConverter.GetBytes(12).CopyTo(empty, 8);
            var mode = new Wad(new WadFile(WadPicker.EngineFileName("doomu.wad"), empty)).GameMode;
            Check(mode == GameMode.Retail, $"doomu.wad 換名後引擎判斷成 {mode}（The Ultimate Doom）");

            Check(WadPicker.SaveKey("DOOM2.WAD") == "doom2" && WadPicker.SaveKey("freedoom1.wad") == "freedoom1", "存檔資料夾名：小寫、不含副檔名");
            Check(DataPaths.SaveSlot("DOOM2.WAD", 3) == "saves/doom2/doomsav3.dsg", $"存檔路徑 {DataPaths.SaveSlot("DOOM2.WAD", 3)}");
            Check(DataPaths.SaveSlot("doomu.wad", 0) == "saves/doomu/doomsav0.dsg", "doomu 的存檔照原檔名分開放");
            Check(DataPaths.Display("C:/Users/x/AppData/LocalLow/Q/P/ModData\\dbfoxtw_probably-doom", null) == @"C:\Users\x\AppData\LocalLow\Q\P\ModData\dbfoxtw_probably-doom",
                  "顯示給玩家的路徑統一用 \\");
        }

        /// <summary>直行優先的測試畫面：每個畫素的值帶著自己的 (x, y)。</summary>
        static uint[] ColumnMajor(int w, int h)
        {
            var src = new uint[w * h];
            for (var x = 0; x < w; x++)
                for (var y = 0; y < h; y++)
                    src[x * h + y] = 0xFF000000u | ((uint)x << 12) | (uint)y;
            return src;
        }

        static void TestToUnityRows()
        {
            foreach (var (w, h) in new[] { (3, 2), (640, 400), (320, 200) })
            {
                var src = ColumnMajor(w, h);
                var dst = new uint[w * h];
                FrameConvert.ToUnityRows(src, w, h, dst);
                var bad = 0;
                for (var x = 0; x < w; x++)
                    for (var y = 0; y < h; y++)
                        if (dst[(h - 1 - y) * w + x] != src[x * h + y]) bad++;
                Check(bad == 0, $"{w}×{h}：Doom 的 (x, y) 都到了貼圖的 (x, {h}-1-y)（左上角 → 貼圖最後一列的開頭）");
            }
            var s = ColumnMajor(3, 2);
            var d = new uint[6];
            FrameConvert.ToUnityRows(s, 3, 2, d);
            Check(d[3] == s[0] && d[5] == s[4] && d[0] == s[1], "3×2 的小例子：左上 → 第 2 列第 1 個、右上 → 第 2 列最後、左下 → 第 1 列第 1 個");
        }

        static void TestThumbnail()
        {
            FrameConvert.ContentSize(79, 55, out var cw, out var ch);
            Check(cw == 73 && ch == 55, $"79×55 裡 4:3 的範圍是 {cw}×{ch}（左右留黑）");
            FrameConvert.ContentSize(40, 40, out var cw2, out var ch2);
            Check(cw2 == 40 && ch2 == 30, $"40×40 裡是 {cw2}×{ch2}（上下留黑）");

            const int W = 640, H = 400, TW = TabletGeometry.ScreenW, TH = TabletGeometry.ScreenH;
            Check(TW == 79 && TH == 55, $"小畫面 {TW}×{TH}");
            var red = FrameConvert.Rgb(255, 0, 0);
            var blue = FrameConvert.Rgb(0, 0, 255);
            var green = FrameConvert.Rgb(0, 255, 0);
            var src = new uint[W * H];
            for (var x = 0; x < W; x++)
                for (var y = 0; y < H; y++)
                    src[x * H + y] = y < H / 4 ? green : x < W / 2 ? red : blue;
            var dst = new uint[TW * TH];
            FrameConvert.Thumbnail(src, W, H, dst, TW, TH);

            uint At(int x, int yFromTop) => dst[(TH - 1 - yFromTop) * TW + x];
            var leftBlack = Enumerable.Range(0, TH).All(y => At(0, y) == FrameConvert.Black && At(1, y) == FrameConvert.Black && At(2, y) == FrameConvert.Black);
            var rightBlack = Enumerable.Range(0, TH).All(y => At(76, y) == FrameConvert.Black && At(77, y) == FrameConvert.Black && At(78, y) == FrameConvert.Black);
            var content = Enumerable.Range(0, TH).All(y => At(3, y) != FrameConvert.Black && At(75, y) != FrameConvert.Black);
            Check(leftBlack && rightBlack && content, "黑邊：左右各 3 欄（第 0～2、76～78 欄），Doom 的畫面在第 3～75 欄");
            Check(At(3, 0) == green && At(75, 0) == green, "最上面一列是畫面的上方（綠）：由下往上的排法沒有上下顛倒");
            Check(At(3, TH - 1) == red && At(75, TH - 1) == blue, "最下面一列：左半紅、右半藍");
            var mid = At(3 + 36, TH - 1);
            var r = mid & 0xFF;
            var b = (mid >> 16) & 0xFF;
            Check(r > 0 && b > 0, $"跨過紅藍交界的那一欄是平均（R {r}、B {b}），不是只取一點");
            Check(dst.All(p => (p >> 24) == 0xFF), "縮完（還沒清透明格）每個畫素都不透明");
        }

        static void TestFocus()
        {
            FocusRules.Frame F(bool shown = true, bool running = true, bool down = false, bool onScreen = false, bool quit = false, bool app = true) =>
                new FocusRules.Frame { DoomShown = shown, Running = running, MouseDown = down, PointerDownOnScreen = onScreen, QuitRequested = quit, AppFocused = app };

            Check(FocusRules.Next(false, F(down: true, onScreen: true)), "點 Doom 畫面 → 拿到焦點");
            Check(FocusRules.Next(true, F()), "沒按滑鼠 → 照舊有焦點");
            Check(!FocusRules.Next(true, F(down: true)), "點別處（物品、別的視窗、DOOM 按鈕）→ 失去焦點");
            Check(!FocusRules.Next(false, F()), "切到 Doom 但還沒點畫面 → 沒有焦點");
            Check(!FocusRules.Next(true, F(shown: false)), "切回結帳、關面板、離開店面場景 → 失去焦點");
            Check(!FocusRules.Next(true, F(running: false)), "Doom 出錯停掉 → 失去焦點");
            Check(!FocusRules.Next(false, F(running: false, down: true, onScreen: true)), "沒有 WAD 時點畫面 → 不給焦點");
            Check(!FocusRules.Next(true, F(quit: true)), "Doom 選單選了 Quit Game → 失去焦點");
            Check(!FocusRules.Next(true, F(app: false)), "遊戲視窗失焦（Alt-Tab 切到別的程式）→ 失去焦點");
            Check(!FocusRules.Next(false, F(app: true)), "切回遊戲視窗、還沒點畫面 → 不會自己拿回焦點");
            Check(FocusRules.Next(false, F(down: true, onScreen: true, app: true)), "切回遊戲後再點一下 Doom 畫面 → 拿到焦點");
            Check(!FocusRules.Next(false, F(down: true, onScreen: true, app: false)), "遊戲視窗沒有焦點時的點擊 → 不給焦點");
        }

        static void TestHotkeys()
        {
            const int Space = 32, Escape = 27, Mouse1 = 324, None = 0;
            Check(HotkeyRules.BlockBinding(Space, true) && !HotkeyRules.BlockBinding(Space, false), "鍵盤鍵：有焦點才擋");
            Check(!HotkeyRules.BlockBinding(Mouse1, true), "滑鼠鍵（右鍵的物品選單）不擋");
            Check(!HotkeyRules.BlockBinding(None, true), "沒綁的不管");
            Check(HotkeyRules.BlockBinding(Escape, true), "Esc 綁在按鍵綁定裡時也擋");
            Check(HotkeyRules.OverrideTyping(true, false) && !HotkeyRules.OverrideTyping(true, true) && !HotkeyRules.OverrideTyping(false, false),
                  "「正在打字」：有焦點、這一幀沒按滑鼠才改成 true");
        }

        static void TestTicClock()
        {
            var c = new TicClock();
            Check(c.Advance(1.0 / 35) == 1, "經過 1/35 秒 → 1 個 tic");
            var n = 0;
            for (var i = 0; i < 60; i++) n += c.Advance(1.0 / 60);
            Check(n == 35, $"60 fps 跑 1 秒 → {n} 個 tic（預期 35）");
            c.Reset();
            n = 0;
            for (var i = 0; i < 144; i++) n += c.Advance(1.0 / 144);
            Check(n == 35, $"144 fps 跑 1 秒 → {n} 個 tic");
            c.Reset();
            Check(c.Advance(1.0) == TicClock.MaxPerFrame && c.Advance(0) == 0, $"卡了 1 秒 → 只補 {TicClock.MaxPerFrame} 個，之後不再追");
            c.Reset();
            Check(c.Advance(0.02) == 0 && c.Advance(0.01) == 1, "0.02 秒不到一個 tic，再加 0.01 秒就到了");
        }

        static void TestStrings()
        {
            Check(Strings.PickLang("en", true) == Lang.En, "英文介面 → 英文（繁中 mod 在跑也一樣：英文字型沒有中文字）");
            Check(Strings.PickLang("zh", false) == Lang.ZhHans, "中文、沒有繁中 mod → 簡中");
            Check(Strings.PickLang("zh", true) == Lang.ZhHant, "中文＋繁中 mod 在跑 → 繁中");
            Check(Strings.PickLang("ja", true) == Lang.En && Strings.PickLang("", false) == Lang.En && Strings.PickLang(null, false) == Lang.En,
                  "其他語言、還沒初始化 → 英文");
            var en = Strings.En;
            var ascii = new[] { en.ButtonTooltip, en.NoWad, en.Paused, en.Error, en.CopyPath, en.Copied }.All(s => s.All(ch => ch < 128));
            Check(ascii, "英文的字只用 ASCII（正式版的英文字型缺很多符號）");
            foreach (var s in Strings.All)
            {
                var noWad = s.NoWad.Split('\n');
                var error = s.Error.Split('\n');
                Check(noWad.Length == 2 && error.Length == 2 && noWad.Concat(error).All(l => l.Length > 0) &&
                      !string.IsNullOrEmpty(s.ButtonTooltip) && !string.IsNullOrEmpty(s.Paused) && !string.IsNullOrEmpty(s.CopyPath) && !string.IsNullOrEmpty(s.Copied),
                      $"沒有 WAD、出錯的說明都是兩句（中間是換行）：{noWad[0]}／{error[0]}");
            }
            Check(!Strings.En.Cjk && Strings.ZhHans.Cjk && Strings.ZhHant.Cjk, "中文照句子換行（Cjk），英文不用");
            // 使用者 2026-11-12 看示意圖定的字（需求.md 決定 29）
            Check(Strings.En.NoWad == "No WAD found.\nPut one in this folder, then press DOOM again:" &&
                  Strings.En.Error == "Doom stopped because of an error.\nDetails are in this file:" &&
                  Strings.En.CopyPath == "Copy path" && Strings.En.Copied == "Copied",
                  "英文是定稿的字");
            Check(Strings.ZhHant.NoWad == "找不到 WAD。\n放一個進這個資料夾，再按一次 DOOM：" &&
                  Strings.ZhHant.Error == "Doom 出錯停止了。\n詳情寫在這個檔案：" &&
                  Strings.ZhHant.CopyPath == "複製路徑" && Strings.ZhHant.Copied == "已複製",
                  "繁中是定稿的字");
            Check(Strings.ZhHans.NoWad == "找不到 WAD。\n放一个进这个文件夹，再按一次 DOOM：" &&
                  Strings.ZhHans.Error == "Doom 出错停止了。\n详情写在这个文件里：" &&
                  Strings.ZhHans.CopyPath == "复制路径" && Strings.ZhHans.Copied == "已复制",
                  "簡中是定稿的字");
        }

        static void TestAltAndNewConfig()
        {
            var keys = new DoomKeyboard(new Config());
            Check(!keys.Press(DoomKey.LAlt) && !keys.Press(DoomKey.RAlt) && !keys.IsDown(DoomKey.LAlt) && !keys.IsDown(DoomKey.RAlt),
                  "左右 Alt 按下不送給 Doom、也不算按著（Alt+F4 會關掉遊戲）");
            Check(!keys.Release(DoomKey.LAlt), "Alt 放開也不送");
            Check(keys.Press(DoomKey.F4) && keys.IsDown(DoomKey.F4), "F4 照常給 Doom（音量選單）");

            // 玩家自己的 doom.cfg 設了 Alt 側移：照讀（不改玩家的設定），但 Alt 不會觸發
            var mine = Config.FromText("key_strafe = lalt, ralt\nkey_strafeleft = a\n");
            Check(mine.key_strafe.Keys.Contains(DoomKey.LAlt), "已經有的 doom.cfg 照玩家設的讀（key_strafe = lalt, ralt 留著）");
            var mk = new DoomKeyboard(mine);
            mk.Press(DoomKey.LAlt);
            mk.Press(DoomKey.Right);
            var cmd = new TicCmd();
            mk.BuildTicCmd(cmd);
            Check(cmd.SideMove == 0 && cmd.AngleTurn != 0, $"按著 Alt＋→ 只是轉向、不是側移（SideMove {cmd.SideMove}、AngleTurn {cmd.AngleTurn}）");

            // 新建 doom.cfg：沒有 Alt 側移，側移左右是 A／D 加上原版的 , 與 .
            var fresh = Config.FromText(null);
            DoomKeyboard.ApplyNewConfigDefaults(fresh);
            Check(fresh.key_strafe.Keys.Count == 0 && fresh.key_strafe.MouseButtons.Count == 0, $"新的 doom.cfg：key_strafe = {fresh.key_strafe}");
            Check(fresh.key_strafeleft.Keys.SequenceEqual(new[] { DoomKey.A, DoomKey.Comma }) && fresh.key_straferight.Keys.SequenceEqual(new[] { DoomKey.D, DoomKey.Period }),
                  $"側移左 {fresh.key_strafeleft}、右 {fresh.key_straferight}");
            var text = fresh.ToText();
            Check(text.Contains("key_strafe = none") && text.Contains("key_strafeleft = a, comma") && text.Contains("key_straferight = d, period"),
                  "寫成 doom.cfg 的字：key_strafe = none、key_strafeleft = a, comma、key_straferight = d, period");
            var back = Config.FromText(text);
            Check(back.key_strafe.Keys.Count == 0 && back.key_strafeleft.Keys.Contains(DoomKey.Comma) && back.key_straferight.Keys.Contains(DoomKey.Period),
                  "寫出去再讀回來一樣（下次開遊戲讀到的是存下來的 doom.cfg，不會再套一次預設）");

            var fk = new DoomKeyboard(fresh);
            fk.Press(DoomKey.Comma);
            fk.BuildTicCmd(cmd);
            var left = cmd.SideMove;
            fk.ReleaseAll();
            fk.Press(DoomKey.Period);
            fk.BuildTicCmd(cmd);
            Check(left < 0 && cmd.SideMove > 0, $", 往左側移（{left}）、. 往右側移（{cmd.SideMove}）");
        }

        static void TestModeRules()
        {
            Check(ModeRules.OnDoomButton(false, false) == ModeRules.Press.EnterDoom && ModeRules.OnDoomButton(false, true) == ModeRules.Press.EnterDoom,
                  "結帳畫面按 DOOM → 切到 Doom");
            Check(ModeRules.OnDoomButton(true, false) == ModeRules.Press.LeaveDoom, "Doom 模式（在跑、暫停、出錯）按 DOOM → 切回結帳");
            Check(ModeRules.OnDoomButton(true, true) == ModeRules.Press.RetryWad, "Doom 模式、找不到 WAD 時按 DOOM → 先重新找一次");
            Check(ModeRules.StayAfterRetry(true) && !ModeRules.StayAfterRetry(false), "重新找到了 → 留在 Doom；還是沒有 → 切回結帳");
            Check(ModeRules.RetryOnScreenClick(true, true, true), "找不到 WAD 的畫面上點一下 → 重新找");
            Check(!ModeRules.RetryOnScreenClick(true, false, true) && !ModeRules.RetryOnScreenClick(false, true, true) && !ModeRules.RetryOnScreenClick(true, true, false),
                  "Doom 在跑、不在 Doom 模式、沒點畫面 → 不重新找");
            Check(ModeRules.KeepDoomMode(true, true, true), "面板開著 → 留在 Doom 模式");
            Check(!ModeRules.KeepDoomMode(true, true, false), "關掉平板（uiObject 變 inactive）→ 回到結帳，下次打開看到結帳畫面");
            Check(!ModeRules.KeepDoomMode(true, false, false), "面板沒了（離開店面場景）→ 回到結帳");
            Check(!ModeRules.KeepDoomMode(false, true, true), "本來就在結帳 → 照舊");
        }

        static void TestDisplayPath()
        {
            const string home = @"C:\Users\someone";
            const string folder = "C:/Users/someone/AppData/LocalLow/Questing Goose Studio/Probably Stolen\\ModData\\dbfoxtw_probably-doom";
            const string expect = @"%USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen\ModData\dbfoxtw_probably-doom";
            Check(DataPaths.Display(folder, home) == expect, $"存檔資料夾在使用者資料夾底下 → {DataPaths.Display(folder, home)}");
            Check(DataPaths.Display(folder, home + @"\") == expect && DataPaths.Display(folder, "C:/Users/someone") == expect,
                  "使用者資料夾結尾有 \\、用 / 也一樣");
            Check(DataPaths.Display(folder.ToLowerInvariant(), home).StartsWith("%USERPROFILE%"), "大小寫不同也認得（Windows 的路徑不分大小寫）");
            Check(!DataPaths.Display(folder, home).Contains("someone"), "顯示的路徑裡沒有 Windows 的使用者名稱");

            Check(DataPaths.Display(folder, @"C:\Users\other") == folder.Replace('/', '\\'), "使用者資料夾對不上（資料夾被搬走、重新導向）→ 完整路徑");
            const string moved = @"D:\Saves\Questing Goose Studio\Probably Stolen\ModData\dbfoxtw_probably-doom";
            Check(DataPaths.Display(moved, home) == moved, "不在使用者資料夾底下 → 完整路徑");
            const string demo = @"C:\Users\someone\AppData\LocalLow\Questing Goose Studio\Probably Stolen Demo\ModData\x";
            Check(DataPaths.Display(demo, home) == demo, "…\\Probably Stolen Demo 不算（開頭一樣但不是同一個資料夾）→ 完整路徑");
            const string linux = "/home/someone/.config/unity3d/Questing Goose Studio/Probably Stolen/ModData/dbfoxtw_probably-doom";
            Check(!DataPaths.Display(linux, "/home/someone").StartsWith("%USERPROFILE%"), "別的平台（沒有 AppData\\LocalLow）→ 不換成 %USERPROFILE%");
            Check(DataPaths.Display(folder, null) == folder.Replace('/', '\\') && DataPaths.Display(folder, "") == folder.Replace('/', '\\'),
                  "拿不到使用者資料夾 → 完整路徑");
            Check(DataPaths.Display("", home) == "" && DataPaths.Display(null, home) == "", "拿不到資料夾 → 空字串");

            // 出錯畫面的 Player.log（Application.consoleLogPath：檔案路徑，Windows 上用 /）
            const string log = "C:/Users/someone/AppData/LocalLow/Questing Goose Studio/Probably Stolen/Player.log";
            Check(DataPaths.Display(log, home) == @"%USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen\Player.log",
                  $"Player.log 在標準位置 → {DataPaths.Display(log, home)}");
            Check(DataPaths.Display(log, @"C:\Users\other") == log.Replace('/', '\\'), "Player.log 不在這個使用者底下 → 完整路徑");
            const string demoLog = @"C:\Users\someone\AppData\LocalLow\Questing Goose Studio\Probably Stolen Demo\Player.log";
            Check(DataPaths.Display(demoLog, home) == demoLog, "Demo 的 Player.log → 完整路徑");
            const string otherLog = @"D:\Logs\Player.log";
            Check(DataPaths.Display(otherLog, home) == otherLog, "-logFile 指到別的地方 → 完整路徑");
        }

        static void TestNoAllocations()
        {
            const int W = 640, H = 400, TW = TabletGeometry.ScreenW, TH = TabletGeometry.ScreenH;
            var src = ColumnMajor(W, H);
            var dst1 = new uint[TW * TH];
            FrameConvert.Thumbnail(src, W, H, dst1, TW, TH); // 第一次：建累加用的陣列
            var before = GC.GetAllocatedBytesForCurrentThread();
            FrameConvert.Thumbnail(src, W, H, dst1, TW, TH);
            var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(bytes == 0, $"小畫面縮第二次起不配置記憶體（{bytes} bytes）");

            // 重用的累加陣列比需要的大時（先縮過高的、再縮矮的）結果照樣對：和全新算的一樣
            var tall = new uint[40 * 120];
            FrameConvert.Thumbnail(src, W, H, tall, 40, 120);
            var dst2 = new uint[TW * TH];
            FrameConvert.Thumbnail(src, W, H, dst2, TW, TH);
            Check(dst1.SequenceEqual(dst2), "累加陣列重用（大小不同的縮圖交錯）結果不變");

            // 清透明格（建 Doom 時算好的索引）也不配置
            var mask = TabletGeometry.TransparentIndices();
            FrameConvert.ClearPixels(dst1, mask);
            before = GC.GetAllocatedBytesForCurrentThread();
            FrameConvert.Thumbnail(src, W, H, dst1, TW, TH);
            FrameConvert.ClearPixels(dst1, mask);
            bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(bytes == 0, $"小畫面縮＋清透明格，第二次起不配置記憶體（{bytes} bytes）");

            var keys = new DoomKeyboard(new Config());
            var cmd = new TicCmd();
            keys.Press(DoomKey.W);
            keys.Press(DoomKey.Left);
            keys.BuildTicCmd(cmd);
            before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 35; i++) keys.BuildTicCmd(cmd);
            bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(bytes == 0, $"BuildTicCmd 跑 35 次（1 秒）不配置記憶體（{bytes} bytes）");

            keys.SetMouse1(true, false);
            keys.BuildTicCmd(cmd);
            before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 35; i++) keys.BuildTicCmd(cmd);
            bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(bytes == 0 && (cmd.Buttons & TicCmdButtons.Attack) != 0, $"按著滑鼠左鍵時 BuildTicCmd 跑 35 次也不配置記憶體（{bytes} bytes）");
        }

        static void TestThumbMask()
        {
            const int X0 = 675, Y0 = 183, W = 79, H = 55;
            Check(TabletGeometry.ScreenX == X0 && TabletGeometry.ScreenY == Y0 && TabletGeometry.ScreenW == W && TabletGeometry.ScreenH == H,
                  "螢幕 x＝675～753、y＝183～237（79×55）");

            // 照評估報告.md「平板本體螢幕的形狀」的表另外列一次，和程式的判斷比對
            var bracket = new HashSet<(int, int)>();
            int[] leftEdge = { 676, 677, 678, 679, 680 }, rightEdge = { 751, 750, 749, 748, 747 };
            for (var y = 221; y <= 237; y++)
            {
                var l = y <= 225 ? leftEdge[y - 221] : 681;
                var r = y <= 225 ? rightEdge[y - 221] : 746;
                for (var x = 675; x <= l; x++) bracket.Add((x, y));
                for (var x = r; x <= 753; x++) bracket.Add((x, y));
            }
            var shadow = new HashSet<(int, int)>();
            for (var y = 183; y <= 220; y++) shadow.Add((675, y));
            for (var x = 682; x <= 743; x++) shadow.Add((x, 183));
            for (var y = 227; y <= 237; y++) shadow.Add((682, y));
            Check(bracket.Count == 225 && shadow.Count == 111 && !bracket.Overlaps(shadow), $"表：支架 {bracket.Count} 格、陰影線 {shadow.Count} 格，不重疊");

            var mismatch = 0;
            var transparent = 0;
            for (var y = Y0; y < Y0 + H; y++)
                for (var x = X0; x < X0 + W; x++)
                {
                    var t = TabletGeometry.IsTransparent(x, y);
                    if (t) transparent++;
                    if (t != (bracket.Contains((x, y)) || shadow.Contains((x, y)))) mismatch++;
                }
            Check(mismatch == 0 && transparent == 336, $"透明 {transparent} 格（和表不同的 {mismatch} 格）、看得到 {W * H - transparent} 格（預期 336／4009）");

            var cases = new (int X, int Y, bool T)[]
            {
                (675, 221, true), (677, 222, true), (678, 222, false), (746, 227, true), (745, 227, false),
                (682, 230, true), (683, 230, false), (744, 183, false), (675, 183, true), (675, 220, true), (676, 220, false),
                (681, 183, false), (682, 183, true), (743, 183, true), (753, 237, true), (753, 220, false),
            };
            var bad = cases.Where(c => TabletGeometry.IsTransparent(c.X, c.Y) != c.T).Select(c => $"({c.X},{c.Y})").ToArray();
            Check(bad.Length == 0, $"邊界格抽查 {cases.Length} 格{(bad.Length > 0 ? "，錯的：" + string.Join("、", bad) : "")}");
            Check(!TabletGeometry.IsTransparent(674, 200) && !TabletGeometry.IsTransparent(754, 237) && !TabletGeometry.IsTransparent(700, 238),
                  "螢幕範圍以外不算");

            // Unity 的排法（由下往上）：索引 r*W+c 的格子是平板圖的 (X0+c, Y0+H-1-r)
            var idx = TabletGeometry.TransparentIndices();
            var set = new HashSet<int>(idx);
            Check(idx.Length == 336 && set.Count == 336, $"透明格的索引 {idx.Length} 個、沒有重複");
            int Index(int x, int y) => (H - 1 - (y - Y0)) * W + (x - X0);
            Check(set.Contains(Index(700, 183)) && !set.Contains(Index(700, 237)),
                  "上下方向：上緣的陰影線 (700,183) 在貼圖的最後一列、同一欄的最下面 (700,237) 不透明");
            Check(set.Contains(0) && set.Contains(W - 1) && set.Contains(Index(675, 183)) && !set.Contains(Index(753, 183)),
                  "四角：下面兩角是支架（貼圖第 0 列的頭尾），左上是陰影線，右上不透明");

            var px = Enumerable.Repeat(FrameConvert.Black, W * H).ToArray();
            FrameConvert.ClearPixels(px, idx);
            Check(px.Count(p => p == FrameConvert.Transparent) == 336 && px.Count(p => (p >> 24) == 0xFF) == 4009, "清完：336 格 alpha 0、4009 格不透明");
        }

        static void TestMouseFire()
        {
            var m = new MouseFire();
            var tap = m.Update(false, true, true, true);
            Check(!m.Held && !tap, "拿焦點的那一下（按下時還沒有焦點）不開火");
            m.Update(true, true, false, true);
            Check(!m.Held, "拿焦點那一下按著不放：照樣不開火");
            m.Update(true, true, false, false);
            tap = m.Update(true, true, true, true);
            Check(m.Held && tap, "已經有焦點時在畫面上按下左鍵 → 開火（回傳新的按下，交給 DoomKeyboard 鎖存）");
            tap = m.Update(true, true, false, true);
            Check(m.Held && !tap, "按著（拖出畫面也一樣：只看左鍵還按著沒有）→ 繼續開火，不算新的按下");
            m.Update(true, true, false, false);
            Check(!m.Held, "放開（在哪裡放開都算）→ 停");
            tap = m.Update(true, true, true, false);
            Check(!m.Held && tap, "同一幀按下又放開 → 照樣回傳按下（交給 DoomKeyboard 補一個 tic）");
            m.Update(true, true, true, true);
            m.Update(true, false, false, true);
            Check(!m.Held, "開火中失去焦點（點別處、切出遊戲…）→ 放開");
            m.Update(false, true, false, true);
            Check(!m.Held, "失去焦點時一直按著、回來後沒有重新按下 → 不開火");
            tap = m.Update(true, true, false, false);
            Check(!m.Held && !tap, "有焦點、這一幀沒有左鍵在畫面上按下（左鍵沒按）→ 不開火（右鍵、中鍵怎麼分在 ScreenInput，碰 Unity，留給實機）");

            var config = new Config();
            Check(config.key_fire.MouseButtons.Contains(DoomMouseButton.Mouse1), $"預設的 key_fire = {config.key_fire}（有 mouse1）");
            var keys = new DoomKeyboard(config);
            var cmd = new TicCmd();
            bool Fire() { keys.BuildTicCmd(cmd); return (cmd.Buttons & TicCmdButtons.Attack) != 0; }
            keys.SetMouse1(true, false);
            Check(Fire(), "左鍵按著 → BuildTicCmd 開火");
            keys.SetMouse1(false, false);
            Check(!Fire(), "放開 → 不開火");
            keys.SetMouse1(false, true);
            var first = Fire();
            var second = Fire();
            keys.TicsRan();
            Check(first && second && !Fire(), "按下又放開：tic 跑過之前都開火（這一幀跑幾個 tic 都算），跑過就停");
            keys.SetMouse1(false, true);
            keys.TicsRan();
            Check(!Fire(), "按下之後已經跑過 tic → 不再開火");

            // 跨兩幀：第 A 幀按下（這一幀沒跑到 tic）、第 B 幀放開，之後才跑 tic → 至少開火一個 tic
            var mf = new MouseFire();
            var kb = new DoomKeyboard(new Config());
            var pressedA = mf.Update(true, true, true, true);
            kb.SetMouse1(mf.Held, pressedA); // 第 A 幀：0 個 tic，不叫 TicsRan
            var pressedB = mf.Update(true, true, false, false);
            kb.SetMouse1(mf.Held, pressedB); // 第 B 幀：放開了，接著跑 tic
            kb.BuildTicCmd(cmd);
            var firedB = (cmd.Buttons & TicCmdButtons.Attack) != 0;
            kb.TicsRan();
            kb.BuildTicCmd(cmd);
            Check(pressedA && !pressedB && !mf.Held && firedB && (cmd.Buttons & TicCmdButtons.Attack) == 0,
                  "第 A 幀按下（沒跑 tic）、第 B 幀放開 → 第 B 幀的 tic 照樣開火一次，跑過就停");

            // 長按：鎖存清掉之後照 held 繼續開火
            kb.SetMouse1(true, true);
            kb.TicsRan();
            kb.SetMouse1(true, false);
            kb.BuildTicCmd(cmd);
            Check((cmd.Buttons & TicCmdButtons.Attack) != 0, "長按：跑過 tic、鎖存清掉之後照樣開火到放開");
            keys.SetMouse1(true, false);
            keys.ReleaseAll();
            Check(!Fire() && !keys.Mouse1Down, "失去焦點時全部放開 → 滑鼠也放開");
            keys.Press(DoomKey.LControl);
            Check(Fire(), "Ctrl 照舊能開火");

            var noMouse = new DoomKeyboard(Config.FromText("key_fire = lcontrol, rcontrol\n"));
            noMouse.SetMouse1(true, false);
            noMouse.BuildTicCmd(cmd);
            Check((cmd.Buttons & TicCmdButtons.Attack) == 0, "綁定拿掉 mouse1 → 左鍵按著也不開火");
            var mouse2 = new DoomKeyboard(Config.FromText("key_fire = mouse2\n"));
            mouse2.SetMouse1(true, false);
            mouse2.BuildTicCmd(cmd);
            Check((cmd.Buttons & TicCmdButtons.Attack) == 0, "綁在 mouse2（只支援左鍵）→ 不開火");
        }

        /// <summary>標記裡在 &lt;nobr&gt; 外面的字（應該只有零寬空白與換行）。</summary>
        static string OutsideNoBreak(string markup)
        {
            var sb = new System.Text.StringBuilder();
            var inside = false;
            for (var i = 0; i < markup.Length; i++)
            {
                if (string.CompareOrdinal(markup, i, TextMarkup.NoBreakOpen, 0, TextMarkup.NoBreakOpen.Length) == 0)
                {
                    inside = true;
                    i += TextMarkup.NoBreakOpen.Length - 1;
                }
                else if (string.CompareOrdinal(markup, i, TextMarkup.NoBreakClose, 0, TextMarkup.NoBreakClose.Length) == 0)
                {
                    inside = false;
                    i += TextMarkup.NoBreakClose.Length - 1;
                }
                else if (!inside) sb.Append(markup[i]);
            }
            return sb.ToString();
        }

        /// <summary>每個零寬空白（可以換行的地方）前面都是 tail＋&lt;/nobr&gt;。</summary>
        static bool BreaksOnlyAfter(string markup, string tail)
        {
            var need = tail + TextMarkup.NoBreakClose;
            for (var i = markup.IndexOf(TextMarkup.BreakHere); i >= 0; i = markup.IndexOf(TextMarkup.BreakHere, i + 1))
                if (i < need.Length || string.CompareOrdinal(markup, i - need.Length, need, 0, need.Length) != 0) return false;
            return true;
        }

        static void TestMarkup()
        {
            var zwsp = TextMarkup.BreakHere.ToString();
            Check(TextMarkup.Sentences(Strings.ZhHant.NoWad, true) ==
                  "<nobr>找不到 WAD。</nobr>\n<nobr>放一個進這個資料夾，</nobr>" + zwsp + "<nobr>再按一次 DOOM：</nobr>",
                  "繁中「找不到 WAD」：每句、每個「，」後面切一段包 <nobr>，段之間放零寬空白");
            foreach (var s in Strings.All)
            {
                foreach (var text in new[] { s.NoWad, s.Error })
                {
                    var m = TextMarkup.Sentences(text, s.Cjk);
                    if (!s.Cjk)
                    {
                        Check(m == text, $"英文照原樣（TMP 在空白換行）：{text.Split('\n')[0]}");
                        continue;
                    }
                    var outside = OutsideNoBreak(m);
                    Check(TextMarkup.Plain(m) == text && outside.All(c => c == TextMarkup.BreakHere || c == '\n') && BreaksOnlyAfter(m, "，"),
                          $"{text.Split('\n')[0]}：拼回來等於原字串、<nobr> 外面只有零寬空白與換行、只在「，」後面能換行");
                }
            }

            const string path = @"%USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen\ModData\dbfoxtw_probably-doom";
            var pm = TextMarkup.Path(path);
            var expect = string.Join(zwsp, new[]
            {
                @"%USERPROFILE%\", @"AppData\", @"LocalLow\", @"Questing Goose Studio\", @"Probably Stolen\", @"ModData\", "dbfoxtw_probably-doom",
            }.Select(seg => "<nobr>" + seg + "</nobr>"));
            Check(pm == expect, "路徑：每段連同後面的「\\」包一個 <nobr>（Questing Goose Studio 不拆）");
            Check(TextMarkup.Plain(pm) == path && OutsideNoBreak(pm).All(c => c == TextMarkup.BreakHere) && BreaksOnlyAfter(pm, "\\"),
                  "路徑拼回來等於原字串、只在「\\」後面能換行");
            const string raw = @"C:\Users\nick\AppData\LocalLow\Questing Goose Studio\Probably Stolen\Player.log";
            Check(TextMarkup.Plain(TextMarkup.Path(raw)) == raw && TextMarkup.Path(raw).Contains(@"<nobr>nick\</nobr>"),
                  "完整路徑（有 \\n 開頭的資料夾名）照原字串，不會被當成換行");
            Check(TextMarkup.Path(@"\\server\share\Player.log").Replace("<nobr>", "").Replace("</nobr>", "").Replace(zwsp, "") == @"\\server\share\Player.log",
                  "網路路徑（\\\\ 開頭）拼回來一樣");
            Check(TextMarkup.Path("") == "" && TextMarkup.Path(null) == "" && TextMarkup.Sentences("", true) == "", "空字串 → 空字串");
        }

        static void TestMessageLayout()
        {
            Check(MessageLayout.ContentWidth == 498 && MessageLayout.PathTextWidth == 470, $"說明寬 {MessageLayout.ContentWidth}、框裡的字寬 {MessageLayout.PathTextWidth}（530 扣掉內縮 16、邊框 2、內距 12）");

            const float textH = 64, pathH = 46, labelW = 80, labelH = 22;
            var r = MessageLayout.Compute(530, 378, textH, true, pathH, true, labelW, labelH);
            Check(r.Scale == 1 && r.Height <= 378, $"預設的列表區 530×378 放得下 → 原大小（整組高 {r.Height}）");
            Check(r.BoxH == pathH + 2 * 8 + 2 * 2 && r.ButtonH == labelH + 2 * 4 + 2 * 2 && r.ButtonW == labelW + 2 * 16 + 2 * 2,
                  $"框高 {r.BoxH}、按鈕 {r.ButtonW}×{r.ButtonH}（內距與邊框）");
            Check(r.TextY == 16 && r.TextY + r.TextH + 12 == r.BoxY && r.BoxY + r.BoxH + 12 == r.ButtonY && r.ButtonY + r.ButtonH + 16 == r.Height,
                  "三件由上往下、間距 12、上下內縮 16，沒有疊在一起");

            var shortArea = MessageLayout.Compute(530, 150, textH, true, pathH, true, labelW, labelH);
            Check(shortArea.Scale < 1 && shortArea.Height * shortArea.Scale <= 150 + 1e-3 && shortArea.Width * shortArea.Scale <= 530 + 1e-3,
                  $"列表區變矮（530×150）→ 整組縮成 {shortArea.Scale:0.###} 倍，放得下");
            Check(shortArea.TextY == r.TextY && shortArea.BoxY == r.BoxY && shortArea.ButtonY == r.ButtonY,
                  "縮小時三件的相對位置不變（只有整組的縮放變，字的比例一致）");
            var narrow = MessageLayout.Compute(265, 378, textH, true, pathH, true, labelW, labelH);
            Check(Math.Abs(narrow.Scale - 0.5f) < 1e-4, $"寬只有一半 → 縮成 {narrow.Scale} 倍");
            var noBox = MessageLayout.Compute(530, 378, textH, false, 0, false, 0, 0);
            Check(noBox.Height == 16 + textH + 16 && noBox.BoxH == 0 && noBox.ButtonH == 0, "沒有路徑框（拿不到 ModData 的路徑）→ 只有說明文字");
            var noButton = MessageLayout.Compute(530, 378, textH, true, pathH, false, labelW, labelH);
            Check(noButton.ButtonH == 0 && noButton.Height == noButton.BoxY + noButton.BoxH + 16, "有框沒有按鈕（出錯畫面拿不到 Player.log 的位置）");
            Check(MessageLayout.Compute(0, 0, textH, true, pathH, true, labelW, labelH).Scale == 0, "畫面區大小是 0 → 縮放 0（不顯示）");
        }

        /// <summary>離線測試用的假音源（MuteLedger 只看參考相等）。</summary>
        sealed class FakeSource
        {
            internal bool Mute;
        }

        static void TestMuteLedger()
        {
            // 目前的遊戲：店裡的背景音樂與收音機是同一個 AudioSource。各記各的會把「原本的 mute」記成 true，還原後一直靜音
            var music = new FakeSource { Mute = false };
            var ledger = new MuteLedger<FakeSource>();
            void Hold(FakeSource src)
            {
                if (ledger.TryAdd(src, src.Mute)) src.Mute = true;
            }
            Hold(music); // 店裡的
            Hold(music); // 收音機的（同一個）
            Check(ledger.Count == 1 && music.Mute, "同一個來源看兩次 → 只記一次、靜音");
            Hold(music); // 下一幀再看一次
            Check(ledger.Count == 1, "有焦點的期間每幀再看：記過的不重記（不會把我們設的 true 當成原本的值）");
            var restored = 0;
            ledger.RestoreAll((src, was) => { src.Mute = was; restored++; });
            Check(!music.Mute && restored == 1 && ledger.Count == 0, "還原：只還原一次，回到原本的沒靜音");

            // 兩個不同的物件（萬一哪個版本分開了）：各自記、各自還原；本來就靜音的照樣還原成靜音
            var store = new FakeSource { Mute = false };
            var radio = new FakeSource { Mute = true };
            Hold(store);
            Hold(radio);
            Check(ledger.Count == 2 && store.Mute && radio.Mute, "兩個不同的來源 → 各記一次");
            ledger.RestoreAll((src, was) => src.Mute = was);
            Check(!store.Mute && radio.Mute, "還原成各自原本的值（本來就靜音的留著靜音）");

            // 還原的順序不影響結果：同一個物件先後被兩個欄位看到，還原時只有一筆
            Hold(music);
            Hold(store);
            Hold(music);
            ledger.RestoreAll((src, was) => src.Mute = was);
            Check(!music.Mute && !store.Mute, "交錯看到同一個來源 → 還原後都回到原本的值");

            // 一個還原出錯不影響其他的，例外在全部試過之後才丟
            Hold(store);
            Hold(music);
            var threw = false;
            try
            {
                ledger.RestoreAll((src, was) =>
                {
                    if (ReferenceEquals(src, store)) throw new InvalidOperationException("壞掉");
                    src.Mute = was;
                });
            }
            catch (InvalidOperationException) { threw = true; }
            Check(threw && !music.Mute && ledger.Count == 0, "一個還原出錯：其他的照樣還原、清單清空，例外最後才丟");
            Check(!ledger.TryAdd(null, false), "null 不記");
        }

        static void TestEngineLog()
        {
            var old = EngineLog.Sink;
            try
            {
                var got = new List<string>();
                EngineLog.Sink = got.Add;
                EngineLog.Write("Open WAD files: ");
                EngineLog.WriteLine("OK (freedoom1.wad)");
                EngineLog.WriteLine("Close WAD files.");
                Check(got.Count == 2 && got[0] == "Open WAD files: OK (freedoom1.wad)" && got[1] == "Close WAD files.",
                      $"Write 的半行接上 WriteLine 才交出一整行：{string.Join("／", got)}");

                got.Clear();
                Config.FromText("key_fire = lcontrol\n");
                Check(got.Count == 1 && got[0] == "Restore settings: OK", $"引擎自己的訊息也經過它（Config.FromText → {string.Join("／", got)}）");

                EngineLog.Sink = _ => throw new InvalidOperationException("sink 壞掉");
                EngineLog.Write("Load map 'E1M1': ");
                EngineLog.WriteLine("OK");
                Check(true, "Sink 丟例外 → 吞掉，不影響引擎");

                EngineLog.Sink = null;
                EngineLog.Write("Load textures: ");
                EngineLog.WriteLine("OK");
                EngineLog.Write("half line ");
                Check(true, "Sink 是 null → 丟掉，不丟例外");
                EngineLog.Sink = got.Add;
                got.Clear();
                EngineLog.WriteLine("next");
                Check(got.Count == 1 && got[0] == "next", "Sink 是 null 時寫的半行不會接到之後的行");
            }
            finally
            {
                EngineLog.Sink = old;
            }
        }

        static void TestLayout()
        {
            var gap = ButtonLayout.Gap(486, 32, 522);
            Check(gap == 4, $"原本按鈕的間距 {gap}（Resize x=486、寬 32，Bargain x=522）");
            Check(ButtonLayout.Gap(486, 32, 300) == ButtonLayout.DefaultGap, "量出來不合理 → 用預設的 4");
            var b = ButtonLayout.DoomButton(486, -8, 32, 32, gap);
            Check(b.X == 418 && b.Y == -8 && b.W == 64 && b.H == 32, $"DOOM 按鈕 {b}：在 Resize 左邊隔 4、寬 64、高 32");

            // button_zoom 貼圖 48×16，三張圖在 x＝0／16／32
            ButtonLayout.SliceUvs(new Box(0, 0, 16, 16), 48, 16, out var l, out var m, out var r);
            Check(Near(l.X, 0) && Near(l.W, 4f / 48) && Near(m.X, 2.5f / 48) && Near(m.W, 1f / 48) && Near(r.X, 12f / 48) && Near(r.W, 4f / 48) && Near(l.H, 1),
                  $"一般的圖：左 {l}、中 {m}、右 {r}（中段取第 2.5～3.5 欄，沒有圖示）");
            ButtonLayout.SliceUvs(new Box(16, 0, 16, 16), 48, 16, out var l1, out _, out var r1);
            Check(Near(l1.X, 16f / 48) && Near(r1.X, 28f / 48), "滑過的圖（x＝16）跟著位移");
            ButtonLayout.SliceWidths(64, 32, new Box(0, 0, 16, 16), out var edge, out var midW);
            Check(edge == 8 && midW == 48, $"左右段各 {edge}、中段 {midW}（放大 2 倍）");
            Check(ButtonLayout.LabelOffset(0, 32, 16) == 0 && ButtonLayout.LabelOffset(1, 32, 16) == -2 && ButtonLayout.LabelOffset(2, 32, 16) == -4,
                  "字的下沉：一般 0、滑過 -2、按下 -4（原圖示 1、2 個圖像素）");

            FrameFit.Fit43(530, 378, out var w1, out var h1);
            Check(Near(w1, 504) && Near(h1, 378), $"列表區 530×378 → 畫面 {w1}×{h1}（左右留黑）");
            FrameFit.Fit43(400, 400, out var w2, out var h2);
            Check(Near(w2, 400) && Near(h2, 300), $"400×400 → {w2}×{h2}（上下留黑）");

            TabletGeometry.ScreenCenter(960, 540, 480, 270, 100, out var x, out var y);
            Check(Near(x, 2.345f) && Near(y, 0.595f), $"平板本體的螢幕中心在本地座標 ({x}, {y})（預期 (2.345, 0.595)）");
        }

        static bool Near(float a, float b) => Math.Abs(a - b) < 1e-4f;

        static void TestKeyboard()
        {
            var config = new Config();
            var keys = new DoomKeyboard(config);
            var cmd = new TicCmd();

            Check(keys.Press(DoomKey.Enter) && !keys.Press(DoomKey.Enter), "Enter 與小鍵盤 Enter 都按：只有第一個送按下");
            Check(!keys.Release(DoomKey.Enter) && keys.Release(DoomKey.Enter), "放開一個還按著；兩個都放開才送放開");
            Check(!keys.Release(DoomKey.Enter), "沒按著的鍵放開不送");

            keys.Press(DoomKey.W);
            keys.BuildTicCmd(cmd);
            Check(cmd.ForwardMove > 0, $"按 W → 往前（ForwardMove {cmd.ForwardMove}）");
            keys.Press(DoomKey.Left);
            keys.BuildTicCmd(cmd);
            Check(cmd.AngleTurn > 0, $"再按 ← → 左轉（AngleTurn {cmd.AngleTurn}）");
            keys.Press(DoomKey.LControl);
            keys.Press(DoomKey.Num3);
            keys.BuildTicCmd(cmd);
            Check((cmd.Buttons & TicCmdButtons.Attack) != 0 && (cmd.Buttons & TicCmdButtons.Change) != 0, "Ctrl 開火、3 換武器");

            var released = keys.ReleaseAll();
            Check(released.Count == 4 && released.Contains(DoomKey.W) && !keys.IsDown(DoomKey.W), $"全部放開：{string.Join("、", released)}");
            keys.BuildTicCmd(cmd);
            Check(cmd.ForwardMove == 0 && cmd.AngleTurn == 0 && cmd.Buttons == 0, "放開後不動、不開火（角色不會一直走）");
        }

        /// <summary>用實際的畫面（標題畫面）跑一次轉置與小畫面，小畫面放大存成 PNG 給人看。</summary>
        static void TestRealFrame(GameContent content, string wadName)
        {
            var config = new Config();
            var video = new FrameBufferVideo(config, content);
            var doom = new Doom(NoArgs(), config, content, video, null, null, null, null);
            for (var i = 0; i < 5; i++) doom.Update();
            video.Render(doom, Fixed.One);
            int w = video.Width, h = video.Height;

            var raw = new uint[w * h];
            Buffer.BlockCopy(video.Data, 0, raw, 0, video.Data.Length);
            var rows = new uint[w * h];
            FrameConvert.ToUnityRows(raw, w, h, rows);
            var bad = 0;
            var rnd = new Random(1);
            for (var i = 0; i < 2000; i++)
            {
                int x = rnd.Next(w), y = rnd.Next(h);
                var unity = rows[(h - 1 - y) * w + x];
                var bytes = BitConverter.GetBytes(unity);
                var o = 4 * (x * h + y);
                if (bytes[0] != video.Data[o] || bytes[1] != video.Data[o + 1] || bytes[2] != video.Data[o + 2] || bytes[3] != video.Data[o + 3]) bad++;
            }
            Check(bad == 0, $"{w}×{h} 的標題畫面：隨機 2000 點，貼圖的 (x, {h}-1-y) 的 RGBA 和引擎的 (x, y) 一樣（uint 搬移沒有弄錯位元組順序）");

            const int TW = DoomSessionThumb.W, TH = DoomSessionThumb.H;
            var thumb = new uint[TW * TH];
            var mask = TabletGeometry.TransparentIndices();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            FrameConvert.Thumbnail(raw, w, h, thumb, TW, TH);
            FrameConvert.ClearPixels(thumb, mask);
            var ms = sw.Elapsed.TotalMilliseconds;
            var nonBlack = thumb.Count(p => p != FrameConvert.Black && p != FrameConvert.Transparent);
            var clear = thumb.Count(p => p == FrameConvert.Transparent);
            Check(nonBlack > TW * TH / 4 && clear == 336, $"小畫面 {TW}×{TH} 有內容（非黑 {nonBlack} 點、透明 {clear} 點）；縮一次 {ms:0.00} ms（.NET 8，只是參考）");

            // 存成 PNG（放大 4 倍）：Images.SaveFrame 吃直行優先的 RGBA（不看 alpha），透明的格子塗成洋紅，看得出來哪些地方露出原圖
            const int S = 4;
            var magenta = FrameConvert.Rgb(255, 0, 255);
            var col = new byte[4 * TW * S * TH * S];
            for (var x = 0; x < TW * S; x++)
                for (var y = 0; y < TH * S; y++)
                {
                    var p = thumb[(TH - 1 - y / S) * TW + x / S];
                    if ((p >> 24) == 0) p = magenta;
                    BitConverter.GetBytes(p).CopyTo(col, 4 * (x * TH * S + y));
                }
            var file = Path.Combine(_out, $"{Path.GetFileNameWithoutExtension(wadName)}_tablet_thumb_x{S}.png");
            Images.SaveFrame(file, col, TW * S, TH * S);
            Info($"小畫面：{file}");
        }

        /// <summary>平板本體的小畫面大小（同 DoomSession.ThumbW／ThumbH，那個類別碰 Unity，這裡用 TabletGeometry 的值）。</summary>
        static class DoomSessionThumb
        {
            internal const int W = TabletGeometry.ScreenW, H = TabletGeometry.ScreenH;
        }
    }
}
