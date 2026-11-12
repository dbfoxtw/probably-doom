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
    using System.Diagnostics;
    using ManagedDoom;
    using ManagedDoom.Audio;
    using ManagedDoom.Video;
    using UnityEngine;
    using DoomInstance = ManagedDoom.Doom;

    /// <summary>
    /// 一份 Doom（引擎、畫面、聲音、鍵盤、存檔、設定）。**整個遊戲執行期間只建一次**（需求.md 決定 11）：第一次切到 Doom 時才建，
    /// 換場景（回主選單、讀檔）不重建，回來接著玩；交易面板上的 UI 每次載入店面場景重新接上（TabletPanel）。
    /// 引擎的 DeHackEd 與 DoomInfo 是靜態的，同一個程式裡換 WAD 不會還原前一個 WAD 改過的表，所以一次只建一個（換 WAD 要重開遊戲）。
    ///
    /// 狀態：None（還沒建）→ Running；ModData 裡沒有 WAD 時 NoWad（下次按 DOOM 再找一次，不用重開遊戲）；
    /// 建的時候或執行中出錯時 Failed：記一次完整的例外、聲音停掉，之後不再跑（不讓例外丟回遊戲）。
    /// </summary>
    sealed class DoomSession
    {
        internal enum State { None, NoWad, Running, Failed }

        /// <summary>平板本體的小畫面大小（遊戲像素，TabletGeometry）。</summary>
        internal const int ThumbW = TabletGeometry.ScreenW, ThumbH = TabletGeometry.ScreenH;

        internal State Status { get; private set; } = State.None;
        internal bool Running => Status == State.Running;

        /// <summary>Doom 的畫面（Width×Height，Unity 的排法）；交易面板的 RawImage 顯示它。Running 之後才有。</summary>
        internal Texture2D Screen { get; private set; }
        /// <summary>平板本體的小畫面（79×55，RGBA32：支架與陰影線的格子是透明的）。Running 之後才有。</summary>
        internal Texture2D Thumb { get; private set; }

        CommandLineArgs _args;
        Config _config;
        GameContent _content;
        DoomInstance _doom;
        FrameBufferVideo _video;
        UnitySound _sound;
        UnityMusic _music;
        UnityKeyboard _keyboard;
        DoomKeyboard _keys;
        readonly TicClock _clock = new TicClock();

        uint[] _raw, _rows, _thumb;
        /// <summary>小畫面裡要透明的格子（TabletGeometry.TransparentIndices，建 Doom 時算一次）。</summary>
        int[] _thumbMask;
        bool _quit;
        int _ticsSinceThumb;
        /// <summary>音樂有沒有建起來、沒有的話為什麼（「開始 Doom」那行 log 用）。</summary>
        string _musicNote = "沒有";

        // 除錯統計（DoomRunner 每 10 秒取一次）
        internal long StatTics, StatTicTicks, StatTicMaxTicks, StatRenders, StatRenderTicks;

        /// <summary>
        /// 切到 Doom 時呼叫：還沒建（或上次沒有 WAD）就建。WAD、音色庫都在這時才讀（不在 Init 讀：音色庫在 mod 資料夾，
        /// Init 時 ModHelper 還找不到自己；WAD 也只有真的要玩時才讀）。
        /// </summary>
        internal void EnsureStarted(GameObject host)
        {
            if (Status != State.None && Status != State.NoWad) return;
            var sw = Stopwatch.StartNew();
            string wad = null;
            try
            {
                var iwads = WadPicker.FindIwads(ModHelper.ListModDataFiles(ProbablyDoomMod.ModId));
                wad = WadPicker.Choose(iwads, Settings.Wad, out var fellBack);
                if (fellBack) ProbablyDoomMod.Log($"設定選的 WAD「{Settings.Wad}」不在 ModData 裡，改用自動挑的 {wad ?? "（沒有）"}");
                if (wad == null)
                {
                    if (Status != State.NoWad) ProbablyDoomMod.Log($"ModData 裡沒有 WAD（{DisplayFolder()}），等玩家放好再按 DOOM");
                    Status = State.NoWad;
                    return;
                }
                Start(host, wad);
                Status = State.Running;
                ProbablyDoomMod.Log($"開始 Doom：WAD {wad}（{_content.Wad.GameMode}）、解析度 {_video.Width}x{_video.Height}、" +
                                    $"音效 {_sound.ClipCount} 個、音樂{_musicNote}、" +
                                    $"存檔在 ModData 的 saves/{WadPicker.SaveKey(wad)}/；花了 {sw.ElapsedMilliseconds} ms");
            }
            catch (Exception e)
            {
                Fail($"建立 Doom（WAD {wad ?? "未定"}）", e);
            }
        }

        /// <summary>ModData 資料夾的完整路徑（ModHelper 會順手建好資料夾）。</summary>
        internal static string DataFolder()
        {
            try { return ModHelper.GetModDataFolder(ProbablyDoomMod.ModId); }
            catch { return ""; }
        }

        /// <summary>
        /// 給玩家看的 ModData 資料夾路徑（沒有 WAD 時的說明、log）：存檔資料夾在使用者資料夾底下的老位置時開頭換成 %USERPROFILE%，
        /// 不是時照原樣（DataPaths.Display）。ModHelper.GetModDataFolder 用的是 Application.persistentDataPath，
        /// Windows 上是使用者資料夾\AppData\LocalLow\＋app.info 的公司名與產品名（Questing Goose Studio\Probably Stolen）。
        /// </summary>
        internal static string DisplayFolder()
        {
            string home;
            try { home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); }
            catch { home = null; }
            return DataPaths.Display(DataFolder(), home);
        }

        /// <summary>
        /// 出錯畫面的路徑框：Player.log 的位置（Application.consoleLogPath），寫法同 <see cref="DisplayFolder"/>（%USERPROFILE% 開頭）。
        /// 拿不到時回傳空字串（畫面只寫 Player.log、不顯示複製按鈕）。
        /// </summary>
        internal static string DisplayLogFile()
        {
            string log, home;
            try { log = Application.consoleLogPath; }
            catch { log = null; }
            try { home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); }
            catch { home = null; }
            return DataPaths.Display(log, home);
        }

        void Start(GameObject host, string wad)
        {
            var id = ProbablyDoomMod.ModId;
            var bytes = ModHelper.ReadModDataFileBytes(id, wad) ?? throw new InvalidOperationException($"讀不到 {wad}");

            // Doom 自己的設定（決定 21）：玩家可以關著遊戲改 doom.cfg；解析度照 mod 設定（決定 5）。
            // 還沒有 doom.cfg（或是空檔）時拿掉 Alt 側移（DoomKeyboard.ApplyNewConfigDefaults）；已經有的照玩家設的
            var cfgText = ModHelper.ReadModDataFileText(id, DataPaths.ConfigFile);
            _config = Config.FromText(cfgText);
            if (string.IsNullOrEmpty(cfgText)) DoomKeyboard.ApplyNewConfigDefaults(_config);
            _config.video_highresolution = Settings.HighResolution;

            _args = new CommandLineArgs(new string[0]);
            _content = new GameContent(_args, new WadFile(WadPicker.EngineFileName(wad), bytes));
            _video = new FrameBufferVideo(_config, _content);
            _sound = new UnitySound(_config, _content, host);
            _music = StartMusic(id, host);

            _keys = new DoomKeyboard(_config);
            _keyboard = new UnityKeyboard(_keys);
            _doom = new DoomInstance(_args, _config, _content, _video, _sound, _music, _keys, new ModDataSaveStore(wad));
            _doom.QuitRequested += () => _quit = true;

            var w = _video.Width;
            var h = _video.Height;
            _raw = new uint[w * h];
            _rows = new uint[w * h];
            _thumb = new uint[ThumbW * ThumbH];
            _thumbMask = TabletGeometry.TransparentIndices();
            Screen = NewTexture(w, h, "ProbablyDoom.Screen");
            Thumb = NewTexture(ThumbW, ThumbH, "ProbablyDoom.Thumb");

            // 先跑一個 tic、畫一格（連小畫面）：還沒點進去時，「點一下繼續」底下是標題畫面而不是一片黑，
            // 小畫面第一次出現時也不會是沒初始化的貼圖。音樂這時還沒開始播（沒有焦點）
            _doom.Update();
            Render(true);
        }

        /// <summary>
        /// 音樂（決定 15）：音色庫讀不到或壞掉（MeltySynth 解析時丟例外）時只是沒有音樂，Doom 照常（不讓整個 Doom 變成 Failed）。
        /// 失敗時回傳 null，原因記一行 log，也記在 _musicNote 給「開始 Doom」那行 log 用。
        /// </summary>
        UnityMusic StartMusic(string id, GameObject host)
        {
            try
            {
                var sf2 = ModHelper.ReadModFileBytes(id, "TimGM6mb.sf2");
                if (sf2 == null)
                {
                    _musicNote = "沒有（讀不到 TimGM6mb.sf2）";
                    ProbablyDoomMod.Warning("mod 資料夾裡讀不到 TimGM6mb.sf2，沒有音樂");
                    return null;
                }
                var synth = new SynthMusic(_config, _content, sf2);
                var music = new UnityMusic(synth, host);
                _musicNote = "有";
                return music;
            }
            catch (Exception e)
            {
                _musicNote = "沒有（建立音樂失敗）";
                ProbablyDoomMod.Warning($"建立音樂失敗（音色庫壞掉？），沒有音樂、Doom 照常：{e.GetType().Name}: {e.Message}");
                return null;
            }
        }

        static Texture2D NewTexture(int w, int h, string name)
        {
            // 像素畫面：Point（不模糊），Clamp（邊緣不接到另一邊）。不要 mipmap。實機看三種尺寸再決定要不要改
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
        }

        /// <summary>
        /// 焦點變了（DoomRunner）：拿到時從現在起算時間、繼續播暫停的音效；失去時放開所有按著的鍵（送 KeyUp）、聲音暫停、存 doom.cfg
        /// （Doom 的選單改的音量、按鍵；決定 21：暫停時與遊戲結束時存）。
        /// </summary>
        internal void SetFocus(bool focused)
        {
            if (Status != State.Running) return;
            try
            {
                if (focused)
                {
                    _clock.Reset();
                    _ticsSinceThumb = 2; // 小畫面一出現就是最新的
                    _sound.HostResume();
                }
                else
                {
                    _keyboard.ReleaseAll(_doom);
                    _sound.HostPause();
                    _music?.Pause();
                    SaveConfig();
                }
            }
            catch (Exception e)
            {
                Fail(focused ? "取得焦點" : "失去焦點", e);
            }
        }

        /// <summary>
        /// 有焦點時每幀呼叫：讀鍵盤、照經過的時間跑 tic（35 Hz）、有跑才重畫一次（不插值，每個 tic 一格）並上傳貼圖；
        /// thumb＝平板本體的小畫面要不要更新（每兩個 tic 一次）；mouseHeld／mousePressed＝滑鼠左鍵開火（MouseFire，決定 26）。
        /// 回傳 true＝玩家在 Doom 選單選了 Quit Game（決定 20）。
        /// </summary>
        internal bool Tick(double seconds, bool thumb, bool mouseHeld, bool mousePressed)
        {
            if (Status != State.Running) return false;
            try
            {
                _keyboard.Poll(_doom);
                _keys.SetMouse1(mouseHeld, mousePressed);
                var n = _clock.Advance(seconds);
                for (var i = 0; i < n; i++)
                {
                    var t0 = Stopwatch.GetTimestamp();
                    _doom.Update();
                    var dt = Stopwatch.GetTimestamp() - t0;
                    StatTics++;
                    StatTicTicks += dt;
                    if (dt > StatTicMaxTicks) StatTicMaxTicks = dt;
                    if (_quit) break;
                }
                if (n > 0)
                {
                    _keys.TicsRan(); // 按下的鎖存至少跑過一個 tic 了
                    _ticsSinceThumb += n;
                    var doThumb = thumb && _ticsSinceThumb >= 2;
                    if (doThumb) _ticsSinceThumb = 0;
                    Render(doThumb);
                }
            }
            catch (Exception e)
            {
                Fail("Doom 執行中", e);
                return false;
            }
            var q = _quit;
            _quit = false;
            return q;
        }

        /// <summary>畫一格、轉成 Unity 的排法、上傳貼圖（FrameConvert；引擎的 byte[] 整塊搬進 uint[]，一個畫素一次搬 4 bytes）。</summary>
        void Render(bool thumb)
        {
            var t0 = Stopwatch.GetTimestamp();
            _video.Render(_doom, Fixed.One);
            var w = _video.Width;
            var h = _video.Height;
            Buffer.BlockCopy(_video.Data, 0, _raw, 0, _video.Data.Length);
            FrameConvert.ToUnityRows(_raw, w, h, _rows);
            Screen.SetPixelData(_rows, 0);
            Screen.Apply(false);
            if (thumb)
            {
                FrameConvert.Thumbnail(_raw, w, h, _thumb, ThumbW, ThumbH);
                FrameConvert.ClearPixels(_thumb, _thumbMask); // 支架與陰影線露出原圖（決定 28）
                Thumb.SetPixelData(_thumb, 0);
                Thumb.Apply(false);
            }
            StatRenders++;
            StatRenderTicks += Stopwatch.GetTimestamp() - t0;
        }

        /// <summary>每幀：音樂只在有焦點、Doom 在跑時播；音量跟遊戲的音樂音量。</summary>
        internal void AudioTick(bool focused)
        {
            if (_music == null) return;
            try { _music.Tick(focused && Status == State.Running, GameInfo.MusicVolume()); }
            catch (Exception e) { Fail("播音樂", e); }
        }

        /// <summary>把 Doom 的設定寫回 ModData 的 doom.cfg（失去焦點時、遊戲結束時）。還沒建 Doom 時不寫。</summary>
        internal void SaveConfig()
        {
            if (_config == null) return;
            try { ModHelper.WriteModDataFileText(ProbablyDoomMod.ModId, DataPaths.ConfigFile, _config.ToText()); }
            catch (Exception e) { ProbablyDoomMod.Warning($"存 {DataPaths.ConfigFile} 失敗：{e.Message}"); }
        }

        /// <summary>
        /// mod 被停掉時（ProbablyDoomMod.OnDisable → DoomRunner.Shutdown）：音效、音樂的 AudioSource 都 Stop。
        /// 遊戲只 Destroy 我們型別的 MonoBehaviour，AudioSource 留著；不停的話暫停中的聲音還掛著、串流的音樂還會叫合成。
        /// 音效、音樂各自包 try：一邊出錯另一邊照樣停。
        /// </summary>
        internal void StopAudio()
        {
            try { if (_sound != null) _sound.StopAll(); }
            catch (Exception e) { ProbablyDoomMod.Warning($"停音效失敗：{e.Message}"); }
            try { if (_music != null) _music.Stop(); }
            catch (Exception e) { ProbablyDoomMod.Warning($"停音樂失敗：{e.Message}"); }
        }

        /// <summary>出錯：停掉 Doom（之後不再跑），完整的例外記一次，聲音停掉。不丟回遊戲。</summary>
        void Fail(string where, Exception e)
        {
            var wasRunning = Status == State.Running;
            Status = State.Failed;
            ProbablyDoomMod.Error($"{where}出錯，Doom 停止（之後不再跑；重開遊戲才會再試）：{e}");
            try
            {
                if (_sound != null) _sound.HostPause();
                if (_music != null) _music.Pause();
                if (wasRunning) SaveConfig();
            }
            catch (Exception e2) { ProbablyDoomMod.Warning($"停掉聲音時又出錯：{e2.Message}"); }
        }
    }
}
