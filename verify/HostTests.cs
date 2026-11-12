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
            Run("7c. 平板本體的小畫面（78×55，4:3、box filter）", TestThumbnail);
            Run("7d. 焦點規則", TestFocus);
            Run("7e. 擋快捷鍵的判斷", TestHotkeys);
            Run("7f. 35 Hz 計時", TestTicClock);
            Run("7g. 語言與畫面上的字", TestStrings);
            Run("7h. DOOM 按鈕的位置與三段拼法、畫面 4:3、平板本體的螢幕位置", TestLayout);
            Run("7i. 鍵盤（按著的鍵、BuildTicCmd、全部放開）", TestKeyboard);
            Run("7j. 不給 Doom 的 Alt、新的 doom.cfg 的側移鍵", TestAltAndNewConfig);
            Run("7k. Doom 模式（DOOM 按鈕、找不到 WAD 時重新找、關掉平板回到結帳）", TestModeRules);
            Run("7l. 沒有 WAD 時顯示的資料夾路徑（%USERPROFILE% 開頭、不是時退回完整路徑）", TestDisplayPath);
            Run("7m. 熱路徑不配置記憶體（小畫面、BuildTicCmd）", TestNoAllocations);
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
            FrameConvert.ContentSize(78, 55, out var cw, out var ch);
            Check(cw == 73 && ch == 55, $"78×55 裡 4:3 的範圍是 {cw}×{ch}（左右留黑）");
            FrameConvert.ContentSize(40, 40, out var cw2, out var ch2);
            Check(cw2 == 40 && ch2 == 30, $"40×40 裡是 {cw2}×{ch2}（上下留黑）");

            const int W = 640, H = 400, TW = 78, TH = 55;
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
            var leftBlack = Enumerable.Range(0, TH).All(y => At(0, y) == FrameConvert.Black && At(1, y) == FrameConvert.Black);
            var rightBlack = Enumerable.Range(0, TH).All(y => At(75, y) == FrameConvert.Black && At(76, y) == FrameConvert.Black && At(77, y) == FrameConvert.Black);
            Check(leftBlack && rightBlack && At(2, 30) != FrameConvert.Black && At(74, 30) != FrameConvert.Black,
                  "黑邊：左 2 欄、右 3 欄（第 2～74 欄是畫面）");
            Check(At(2, 0) == green && At(74, 0) == green, "最上面一列是畫面的上方（綠）：由下往上的排法沒有上下顛倒");
            Check(At(2, TH - 1) == red && At(74, TH - 1) == blue, "最下面一列：左半紅、右半藍");
            var mid = At(2 + 36, TH - 1);
            var r = mid & 0xFF;
            var b = (mid >> 16) & 0xFF;
            Check(r > 0 && b > 0, $"跨過紅藍交界的那一欄是平均（R {r}、B {b}），不是只取一點");
            Check(dst.All(p => (p >> 24) == 0xFF), "每個畫素都不透明");
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
            var ascii = new[] { en.ButtonTooltip, en.NoWad, en.Paused, en.Error }.All(s => s.All(ch => ch < 128));
            Check(ascii, "英文的字只用 ASCII（正式版的英文字型缺很多符號）");
            const string path = @"%USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen\ModData\dbfoxtw_probably-doom";
            foreach (var s in Strings.All)
            {
                var msg = string.Format(s.NoWad, path);
                var lines = msg.Split('\n');
                Check(lines.Length == 2 && lines[1] == path && !string.IsNullOrEmpty(s.ButtonTooltip) && !string.IsNullOrEmpty(s.Paused) && !string.IsNullOrEmpty(s.Error),
                      $"沒有 WAD 的說明：一句話＋換行＋資料夾路徑（{lines[0]}）");
            }
            // 使用者 2026-11-12 定的最短版
            Check(Strings.En.NoWad == "No WAD found. Put one in this folder, then press DOOM again:\n{0}", "英文的「找不到 WAD」是定稿的字");
            Check(Strings.ZhHant.NoWad == "找不到 WAD。放一個進這個資料夾，再按一次 DOOM：\n{0}", "繁中的「找不到 WAD」是定稿的字");
            Check(Strings.ZhHans.NoWad == "找不到 WAD。放一个进这个文件夹，再按一次 DOOM：\n{0}", "簡中的「找不到 WAD」是定稿的字");
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

            var keys = new DoomKeyboard(new Config());
            var cmd = new TicCmd();
            keys.Press(DoomKey.W);
            keys.Press(DoomKey.Left);
            keys.BuildTicCmd(cmd);
            before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 35; i++) keys.BuildTicCmd(cmd);
            bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(bytes == 0, $"BuildTicCmd 跑 35 次（1 秒）不配置記憶體（{bytes} bytes）");
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
            Check(Near(x, 2.35f) && Near(y, 0.595f), $"平板本體的螢幕中心在本地座標 ({x}, {y})（預期 (2.35, 0.595)）");
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
            var sw = System.Diagnostics.Stopwatch.StartNew();
            FrameConvert.Thumbnail(raw, w, h, thumb, TW, TH);
            var ms = sw.Elapsed.TotalMilliseconds;
            var nonBlack = thumb.Count(p => p != FrameConvert.Black);
            Check(nonBlack > TW * TH / 4, $"小畫面 {TW}×{TH} 有內容（非黑 {nonBlack} 點）；縮一次 {ms:0.00} ms（.NET 8，只是參考）");

            // 存成 PNG（放大 4 倍）：Images.SaveFrame 吃直行優先的 RGBA
            const int S = 4;
            var col = new byte[4 * TW * S * TH * S];
            for (var x = 0; x < TW * S; x++)
                for (var y = 0; y < TH * S; y++)
                {
                    var p = thumb[(TH - 1 - y / S) * TW + x / S];
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
