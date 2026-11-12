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
    /// 焦點（FocusRules）、Doom 的 tic 與畫面（DoomSession）、交易面板的 UI（TabletPanel）、平板本體的小畫面（TabletScreen）、
    /// 音樂的播放與音量、語言、除錯統計。全部在 LateUpdate：EventSystem 在它的 Update 送出「點到 Doom 畫面」，這時已經送完，
    /// 同一幀就看得到；算完的焦點設給 InputBlocker，下一幀遊戲的 Update 就照它擋快捷鍵。
    /// 這裡出錯時同一個錯誤只記一次 log，不丟回遊戲。
    /// </summary>
    sealed class DoomRunner : MonoBehaviour
    {
        internal static DoomRunner Instance { get; private set; }

        readonly DoomSession _session = new DoomSession();

        TabletPanel _panel;
        TabletScreen _tabletScreen;
        StoreTablet _storeTablet;
        /// <summary>交易面板切到 Doom 了（DOOM 按鈕按下去的樣子）。每次載入店面場景（新的面板）從結帳畫面開始。</summary>
        bool _doomMode;
        bool _focused;

        Strings _strings = Strings.En;
        float _nextLangCheck;
        string _noWadMessage;

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
            UpdateLanguage(true);
            _panel = TabletPanel.Attach(frame, uiObject, OnDoomButton, () => _strings.ButtonTooltip);
            _doomMode = false;
            // 平板本體：同一個店面場景裡（CreateWindow 在 NegociationUIManager.Start 裡，場景已經載入）。小畫面等 Doom 建好、要顯示時才建
            _storeTablet = FindObjectOfType<StoreTablet>();
            if (_storeTablet == null) ProbablyDoomMod.Warning("店面場景裡找不到平板本體（StoreTablet），不加小畫面");
            _tabletScreen = null;
            if (_panel != null) ProbablyDoomMod.DebugLog("交易面板加上 DOOM 按鈕");
        }

        /// <summary>按 DOOM：切換 Doom 與結帳畫面。切到 Doom 時還沒建（或上次沒有 WAD）就建；焦點要再點一下 Doom 畫面才給（決定 10）。</summary>
        void OnDoomButton()
        {
            try
            {
                _doomMode = !_doomMode;
                if (_doomMode) _session.EnsureStarted(gameObject);
                ProbablyDoomMod.DebugLog(_doomMode ? $"切到 Doom（{_session.Status}）" : "切回結帳畫面");
                // 和遊戲的調整大小按鈕一樣的按鍵聲（NegociationUIManager.OnResizeClick）
                var audio = EmporiumAudioPlayer.Instance;
                if (audio != null && audio.audioButton1 != null) audio.audioButton1.Play();
            }
            catch (Exception e) { LogOnce("按 DOOM", e); }
        }

        void LateUpdate()
        {
            if (!ProbablyDoomMod.Ready) return;
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
            if (!panelAlive)
            {
                _panel = null;
                _doomMode = false;
            }
            var shown = panelAlive && _panel.Open && _doomMode;

            // 焦點（FocusRules）：點 Doom 畫面拿到、點別處或切走就還給遊戲
            var frame = new FocusRules.Frame
            {
                DoomShown = shown,
                Running = _session.Running,
                MouseDown = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2),
                PointerDownOnScreen = panelAlive && _panel.PointerDownThisFrame,
            };
            SetFocus(FocusRules.Next(_focused, frame));

            if (_focused)
            {
                var wantThumb = Settings.TabletScreen && _tabletScreen != null && _tabletScreen.Alive;
                if (_session.Tick(Time.unscaledDeltaTime, wantThumb))
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

            if (_panel != null) _panel.Sync(_doomMode, ViewTexture(), _session.Running && !_focused, Message(), _strings);
            UpdateTabletScreen();
            Report();
        }

        void SetFocus(bool focused)
        {
            if (focused == _focused) return;
            _focused = focused;
            InputBlocker.Focused = focused;
            _focusChanges++;
            _session.SetFocus(focused);
            ProbablyDoomMod.DebugLog(focused ? "Doom 拿到焦點（鍵盤給 Doom）" : "Doom 失去焦點（暫停）");
        }

        /// <summary>Doom 畫面區要顯示的畫面：在跑（含暫停中）時是 Doom 的最後一格；沒有 WAD、出錯時不顯示。</summary>
        Texture ViewTexture() => _session.Running ? _session.Screen : null;

        /// <summary>Doom 畫面區的說明：沒有 WAD（附 ModData 的完整路徑）、出錯。</summary>
        string Message()
        {
            switch (_session.Status)
            {
                case DoomSession.State.NoWad:
                    return _noWadMessage ?? (_noWadMessage = string.Format(_strings.NoWad, DataPaths.Display(DoomSession.DataFolder())));
                case DoomSession.State.Failed:
                    return _strings.Error;
                default:
                    return null;
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
            _strings = s;
            _noWadMessage = null;
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

        /// <summary>遊戲結束時（ProbablyDoomMod.OnDisable）：存 Doom 的設定。</summary>
        internal void Shutdown()
        {
            _session.SaveConfig();
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
