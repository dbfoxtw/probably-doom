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
    using UnityEngine;

    /// <summary>
    /// 每幀的工作（官方的 IMod 沒有每幀的回呼，掛在 Init 建的 DontDestroyOnLoad 物件上）：
    /// 焦點（FocusRules）、滑鼠左鍵開火（MouseFire）、Doom 的 tic 與畫面（DoomSession）、交易面板的 UI（TabletPanel）、
    /// 平板本體的小畫面（TabletScreen）、音樂的播放與音量、遊戲背景音樂的靜音（GameMusicMute）、語言、除錯統計。
    /// 全部在 LateUpdate：EventSystem 在它的 Update 送出「點到 Doom 畫面」，這時已經送完，
    /// 同一幀就看得到；算完的焦點設給 InputBlocker，下一幀遊戲的 Update 就照它擋快捷鍵。
    /// 這裡出錯時同一個錯誤只記一次 log，不丟回遊戲。
    /// </summary>
    sealed class DoomRunner : MonoBehaviour
    {
        internal static DoomRunner Instance { get; private set; }

        readonly DoomSession _session = new DoomSession();
        readonly MouseFire _mouseFire = new MouseFire();
        readonly GameMusicMute _musicMute = new GameMusicMute();

        TabletPanel _panel;
        TabletScreen _tabletScreen;
        StoreTablet _storeTablet;
        /// <summary>還沒找到平板本體時，下一次找的時間（Time.unscaledTime）。</summary>
        float _nextTabletSearch;
        /// <summary>
        /// 交易面板切到 Doom 了（DOOM 按鈕按下去的樣子）。每次載入店面場景（新的面板）與關掉面板再打開時從結帳畫面開始
        /// （ModeRules.KeepDoomMode）。
        /// </summary>
        bool _doomMode;
        bool _focused;
        /// <summary>Shutdown 跑過了（mod 被停掉）：之後什麼都不做。</summary>
        bool _shutDown;

        Strings _strings = Strings.En;
        float _nextLangCheck;
        /// <summary>說明畫面的路徑框（DataPaths.Display 的寫法），第一次要用時才算、之後照用（路徑在遊戲執行中不會變）。</summary>
        string _noWadPath, _logPath;

        float _nextReport;
        int _focusChanges;
        string _lastError;

        void Awake()
        {
            Instance = this;
        }

        /// <summary>交易面板建好了（Patches.cs 的 CreateWindow postfix，每次載入店面場景一次）。</summary>
        internal static void OnWindowCreated(NegotiationTabletFrame frame, GameObject uiObject)
        {
            var self = Instance;
            if (!ProbablyDoomMod.Ready || self == null) return;
            try { self.Attach(frame, uiObject); }
            catch (Exception e) { ProbablyDoomMod.Error($"在交易面板加 DOOM 按鈕失敗：{e}"); }
        }

        void Attach(NegotiationTabletFrame frame, GameObject uiObject)
        {
            if (_shutDown) return;
            UpdateLanguage(true);
            _panel = TabletPanel.Attach(frame, uiObject, OnDoomButton, () => _strings.ButtonTooltip);
            _doomMode = false;
            // 平板本體：同一個店面場景裡（CreateWindow 在 NegociationUIManager.Start 裡，場景已經載入）。小畫面等 Doom 建好、要顯示時才建
            _storeTablet = null;
            _tabletScreen = null;
            FindStoreTablet(true);
            if (_panel != null) ProbablyDoomMod.DebugLog("交易面板加上 DOOM 按鈕");
        }

        /// <summary>
        /// 找店面場景裡的平板本體（StoreTablet）。FindObjectOfType 只找得到 active 的物件，交易面板建好時平板本體可能還沒啟用，
        /// 所以找不到時面板還在的期間每隔約 1 秒再找一次（Step），找到才建小畫面。只在第一次找不到、之後找到時各記一行。
        /// </summary>
        void FindStoreTablet(bool first)
        {
            _nextTabletSearch = Time.unscaledTime + 1f;
            _storeTablet = FindObjectOfType<StoreTablet>();
            if (_storeTablet != null)
            {
                if (!first) ProbablyDoomMod.Log("找到店面的平板本體（StoreTablet），可以加小畫面了");
            }
            else if (first)
            {
                ProbablyDoomMod.Log("店面場景裡還找不到平板本體（StoreTablet，可能還沒啟用），之後每秒再找一次");
            }
        }

        /// <summary>
        /// 按 DOOM（ModeRules.OnDoomButton）：結帳畫面 → 切到 Doom（還沒建就建）；Doom 模式中 → 切回結帳，
        /// 但畫面是「找不到 WAD」時先重新找一次，找到就開始、留在 Doom 模式，還是沒有才切回結帳（決定 12 的說明寫「放好再按一次 DOOM」）。
        /// 焦點要再點一下 Doom 畫面才給（決定 10）。
        /// </summary>
        void OnDoomButton()
        {
            if (_shutDown) return;
            try
            {
                switch (ModeRules.OnDoomButton(_doomMode, _session.Status == DoomSession.State.NoWad))
                {
                    case ModeRules.Press.EnterDoom:
                        _doomMode = true;
                        _session.EnsureStarted(gameObject);
                        ProbablyDoomMod.DebugLog($"切到 Doom（{_session.Status}）");
                        break;
                    case ModeRules.Press.RetryWad:
                        _session.EnsureStarted(gameObject);
                        _doomMode = ModeRules.StayAfterRetry(_session.Status != DoomSession.State.NoWad);
                        ProbablyDoomMod.DebugLog(_doomMode ? $"重新找 WAD：找到了（{_session.Status}）" : "重新找 WAD：還是沒有，切回結帳畫面");
                        break;
                    default:
                        _doomMode = false;
                        ProbablyDoomMod.DebugLog("切回結帳畫面");
                        break;
                }
                TabletPanel.PlayButtonSound(); // 和遊戲的調整大小按鈕一樣的按鍵聲
            }
            catch (Exception e) { LogOnce("按 DOOM", e); }
        }

        void LateUpdate()
        {
            if (!ProbablyDoomMod.Ready || _shutDown) return;
            try
            {
                Step();
            }
            catch (Exception e)
            {
                LogOnce("每幀的更新", e);
                SetFocus(false);
            }
        }

        void Step()
        {
            UpdateLanguage(false);

            var panelAlive = _panel != null && _panel.Alive;
            if (!panelAlive) _panel = null;
            // 面板沒了或關掉了就回到結帳畫面（決定 25）：下次打開平板看到結帳，要玩再按 DOOM
            var keep = ModeRules.KeepDoomMode(_doomMode, panelAlive, panelAlive && _panel.ActiveSelf);
            if (_doomMode && !keep) ProbablyDoomMod.DebugLog(panelAlive ? "關掉平板：回到結帳畫面" : "交易面板沒了（離開店面場景）：回到結帳畫面");
            _doomMode = keep;
            var shown = panelAlive && _panel.Open && _doomMode;
            var pointerDownOnScreen = panelAlive && _panel.PointerDownThisFrame;
            var leftDownOnScreen = panelAlive && _panel.LeftDownThisFrame;

            // 平板本體還沒找到（交易面板建好時還沒啟用）：每秒再找一次
            if (panelAlive && _storeTablet == null && Time.unscaledTime >= _nextTabletSearch) FindStoreTablet(false);

            // 「找不到 WAD」的畫面上點一下：重新找一次，找到就開始（同一下點擊接著拿焦點）
            if (ModeRules.RetryOnScreenClick(_doomMode, _session.Status == DoomSession.State.NoWad, pointerDownOnScreen))
            {
                _session.EnsureStarted(gameObject);
                ProbablyDoomMod.DebugLog($"點了 Doom 畫面區，重新找 WAD：{_session.Status}");
            }

            // 焦點（FocusRules）：點 Doom 畫面拿到、點別處、切走或切出遊戲視窗就還給遊戲
            var frame = new FocusRules.Frame
            {
                DoomShown = shown,
                Running = _session.Running,
                MouseDown = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2),
                PointerDownOnScreen = pointerDownOnScreen,
                AppFocused = Application.isFocused,
            };
            // 滑鼠開火看「按下時已經有焦點」：用這一幀算焦點之前的值（拿焦點的那一下不開火，MouseFire）
            var focusedBefore = _focused;
            SetFocus(FocusRules.Next(_focused, frame));
            var mousePressed = _mouseFire.Update(focusedBefore, _focused, leftDownOnScreen, Input.GetMouseButton(0));

            if (_focused)
            {
                // 有焦點的期間每幀再看一次：中途才出現的背景音樂來源也靜音（拿到焦點那一下在 SetFocus 裡）。
                // 出錯只是不靜音，Doom 照常玩：自己接住，不讓 LateUpdate 的 catch 把焦點踢掉
                try { _musicMute.Update(true); }
                catch (Exception e) { LogOnce("把遊戲的背景音樂靜音", e); }
                var wantThumb = Settings.TabletScreen && _tabletScreen != null && _tabletScreen.Alive;
                if (_session.Tick(Time.unscaledDeltaTime, wantThumb, _mouseFire.Held, mousePressed))
                {
                    // Doom 選單的 Quit Game（決定 20）：切回結帳畫面、暫停
                    ProbablyDoomMod.DebugLog("Doom 選單選了離開：切回結帳畫面");
                    _doomMode = false;
                    frame.QuitRequested = true;
                    SetFocus(FocusRules.Next(_focused, frame));
                }
                else if (!_session.Running)
                {
                    SetFocus(false); // 執行中出錯停掉了
                }
            }
            _session.AudioTick(_focused);

            if (_panel != null)
            {
                var message = Message(out var path);
                _panel.Sync(_doomMode, ViewTexture(), _session.Running && !_focused, message, path, _strings);
            }
            UpdateTabletScreen();
            Report();
        }

        void SetFocus(bool focused)
        {
            if (focused == _focused) return;
            _focused = focused;
            InputBlocker.Focused = focused;
            _focusChanges++;
            if (!focused) _mouseFire.Release();
            _session.SetFocus(focused);
            // 遊戲的背景音樂：有焦點時靜音，失去焦點（任何原因）就還原（決定 27）
            try { _musicMute.Update(focused); }
            catch (Exception e) { LogOnce(focused ? "把遊戲的背景音樂靜音" : "還原遊戲的背景音樂", e); }
            ProbablyDoomMod.DebugLog(focused ? "Doom 拿到焦點（鍵盤給 Doom）" : "Doom 失去焦點（暫停）");
        }

        /// <summary>Doom 畫面區要顯示的畫面：在跑（含暫停中）時是 Doom 的最後一格；沒有 WAD、出錯時不顯示。</summary>
        Texture ViewTexture() => _session.Running ? _session.Screen : null;

        /// <summary>
        /// Doom 畫面區的說明與路徑框的路徑（決定 29）：沒有 WAD＝ModData 資料夾、出錯＝Player.log 的位置，都是 %USERPROFILE% 開頭的寫法。
        /// </summary>
        TabletPanel.Message Message(out string path)
        {
            switch (_session.Status)
            {
                case DoomSession.State.NoWad:
                    path = _noWadPath ?? (_noWadPath = DoomSession.DisplayFolder());
                    return TabletPanel.Message.NoWad;
                case DoomSession.State.Failed:
                    path = _logPath ?? (_logPath = DoomSession.DisplayLogFile());
                    return TabletPanel.Message.Error;
                default:
                    path = null;
                    return TabletPanel.Message.None;
            }
        }

        /// <summary>
        /// 平板本體的小畫面（決定 6～8）：設定開著才建（Doom 建好之後、店面場景裡），只在正在玩（Doom 模式＋有焦點）時顯示；
        /// 設定關掉就拿掉（設定視窗只在主選單，回到店裡時就是新的值）。
        /// </summary>
        void UpdateTabletScreen()
        {
            if (_tabletScreen != null && !_tabletScreen.Alive) _tabletScreen = null;
            if (!Settings.TabletScreen)
            {
                if (_tabletScreen != null)
                {
                    _tabletScreen.Destroy();
                    _tabletScreen = null;
                }
                return;
            }
            if (_tabletScreen == null && _storeTablet != null && _session.Running && _session.Thumb != null)
                _tabletScreen = TabletScreen.Create(_storeTablet, _session.Thumb);
            if (_tabletScreen != null) _tabletScreen.SetVisible(_focused && _doomMode && _session.Running);
        }

        /// <summary>語言（每秒看一次就好）：遊戲語言或繁中 mod 變了就換字。</summary>
        void UpdateLanguage(bool force)
        {
            if (!force && Time.unscaledTime < _nextLangCheck) return;
            _nextLangCheck = Time.unscaledTime + 1f;
            var s = Strings.For(Strings.PickLang(GameInfo.LocaleCode(), GameInfo.ZhHantModRunning()));
            if (s == _strings) return;
            _strings = s; // 交易面板的說明看到 Strings 換了就重建（TabletPanel.SyncMessage）
        }

        /// <summary>除錯模式：每 10 秒一行（有跑 tic 或焦點有變時才寫，閒著不洗版）。</summary>
        void Report()
        {
            if (Time.unscaledTime < _nextReport) return;
            _nextReport = Time.unscaledTime + 10f;
            if (!ProbablyDoomMod.Debug) return;
            var s = _session;
            if (s.StatTics == 0 && _focusChanges == 0) return;
            double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
            var tic = s.StatTics > 0 ? $"{s.StatTics} 個 tic，平均 {Ms(s.StatTicTicks) / s.StatTics:0.00} ms、最慢 {Ms(s.StatTicMaxTicks):0.00} ms" : "沒跑 tic";
            var render = s.StatRenders > 0 ? $"畫面＋上傳 {s.StatRenders} 次，平均 {Ms(s.StatRenderTicks) / s.StatRenders:0.00} ms" : "沒畫";
            ProbablyDoomMod.Log($"最近 10 秒：{tic}；{render}；焦點切換 {_focusChanges} 次（現在{(_focused ? "有" : "沒有")}）；{InputBlocker.TakeStats()}");
            s.StatTics = s.StatTicTicks = s.StatTicMaxTicks = s.StatRenders = s.StatRenderTicks = 0;
            _focusChanges = 0;
        }

        /// <summary>
        /// mod 停下來時（ProbablyDoomMod.OnDisable：遊戲結束，或遊戲在執行中停掉這個 mod，例如官方信任清單封鎖、暫停 mod）：
        /// 遊戲先呼叫 OnDisable，之後才拆掉 patch、Destroy 我們型別的 MonoBehaviour（ModLoader.StopRunningMod）；
        /// AudioSource、Image 這些 Unity 的元件與 GameObject 會留著，所以這裡自己收尾：
        /// 失去焦點（送 KeyUp、停音效、還原遊戲的背景音樂）、音效與音樂的 AudioSource 都 Stop、拿掉交易面板上的 DOOM 按鈕與 Doom 畫面區、
        /// 拿掉平板本體的小畫面、存 doom.cfg，最後 Destroy 自己的 GameObject（AudioSource 都掛在上面）。
        /// 每一步各自包 try：一步出錯不影響後面的。之後 LateUpdate、按鈕都不再做事。
        /// </summary>
        internal void Shutdown()
        {
            if (_shutDown) return;
            _shutDown = true;
            try { SetFocus(false); }
            catch (Exception e) { ProbablyDoomMod.Warning($"停止時放開焦點失敗：{e.Message}"); }
            InputBlocker.Focused = false;
            // 遊戲的背景音樂一定要還原（SetFocus 裡也還原過；那裡出錯時這裡再試一次）
            try { _musicMute.Restore(); }
            catch (Exception e) { ProbablyDoomMod.Warning($"停止時還原遊戲的背景音樂失敗：{e.Message}"); }
            _session.StopAudio(); // 音效、音樂各自包 try
            try
            {
                if (_panel != null) _panel.Teardown(); // 按鈕、畫面區各自包 try
            }
            catch (Exception e) { ProbablyDoomMod.Warning($"停止時拿掉交易面板上的東西失敗：{e.Message}"); }
            _panel = null;
            _doomMode = false;
            try
            {
                if (_tabletScreen != null)
                {
                    _tabletScreen.SetVisible(false);
                    _tabletScreen.Destroy();
                }
            }
            catch (Exception e) { ProbablyDoomMod.Warning($"停止時拿掉平板本體的小畫面失敗：{e.Message}"); }
            _tabletScreen = null;
            _session.SaveConfig(); // 自己包 try
            try { Destroy(gameObject); }
            catch (Exception e) { ProbablyDoomMod.Warning($"停止時拿掉 ProbablyDoom 物件失敗：{e.Message}"); }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>每幀都會跑到的地方出錯時，同一個錯誤只記一次，免得洗版。</summary>
        void LogOnce(string what, Exception e)
        {
            var msg = $"{what}出錯：{e}";
            if (msg == _lastError) return;
            _lastError = msg;
            ProbablyDoomMod.Error(msg);
        }
    }
}
