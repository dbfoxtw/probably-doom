# Probably Doom

Probably Stolen（Questing Goose Studio）的 mod：在店裡的平板上玩 Doom。引擎是 [ManagedDoom](https://github.com/sinshu/managed-doom)（C# 寫的 Doom），音樂用 [MeltySynth](https://github.com/sinshu/meltysynth) 與 TimGM6mb 音色庫合成；不附任何 WAD，要玩的人自己放 DOOM1.WAD、DOOM.WAD、DOOM2.WAD 或 Freedoom。授權是 **GPL-2.0-or-later**（[LICENSE](LICENSE)），第三方元件的來源、授權與我們改了什麼見 [VENDOR.md](VENDOR.md)。**還在開發中**：第一版已經接進遊戲（交易面板的 DOOM 按鈕與畫面、平板本體的小畫面、鍵盤、聲音、存讀檔），還沒實機測試。`python tools/install.py build` 編譯 mod、`install.bat` 編譯並裝進遊戲（除錯模式）；`dotnet run --project verify -c Release` 用 `testdata/` 的 Freedoom 跑離線測試（引擎與 `mod/src/Core/` 的純邏輯）。
