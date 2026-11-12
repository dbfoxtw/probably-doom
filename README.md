# Probably DOOM（可能是 DOOM）

*Probably Stolen*（Questing Goose Studio）的 mod：在店裡的平板電腦上玩 DOOM。遊戲官方格式的 mod；不附任何 DOOM 的遊戲資料（WAD），要自己準備。

> A mod for *Probably Stolen*: play DOOM on the store's Tablet. Bring your own WAD. See [English](#english) below.

下載：[Steam 工作坊](https://steamcommunity.com/sharedfiles/filedetails/?id=3838535176)（訂閱就好）。這個 repo 是它的原始碼。

程式由 AI（Claude）撰寫，功能設計與實機測試由作者進行。有問題歡迎回報：[Issues](https://github.com/dbfoxtw/probably-doom/issues) 或工作坊頁面的留言。請回報給本 mod，不要回報給遊戲開發者。

## 功能

- **DOOM 按鈕**：平板電腦畫面上方的按鈕列多一顆 DOOM，按一下切到 DOOM，再按一下回到原本的畫面。平板電腦的三種大小都能玩。
- **玩法**：點一下 DOOM 的畫面開始玩。用鍵盤玩，在畫面上按住滑鼠左鍵也能開火。玩的時候遊戲的快捷鍵暫停；點畫面以外的地方 DOOM 就暫停，快捷鍵恢復。DOOM 選單的 Quit Game 會回到平板電腦原本的畫面。
- **平板電腦本體也在播**：玩的時候，桌上那台平板電腦的螢幕也跟著播小畫面。
- **音效與音樂**：音樂跟著遊戲的音樂音量。玩的時候遊戲的音樂和環境音自動靜音，DOOM 暫停就恢復。
- **存讀檔**：DOOM 自己的存檔，不會動到遊戲的存檔。
- 支援原版的 DOOM1.WAD（共享版）、DOOM.WAD、DOOM2.WAD，以及免費的 Freedoom。

## 怎麼用

1. 在[工作坊](https://steamcommunity.com/sharedfiles/filedetails/?id=3838535176)訂閱，開遊戲後在主選單的「模組」畫面啟用「Probably DOOM」，照提示重開遊戲。
2. 準備 DOOM 的遊戲資料（WAD）：
   - 免費的 Freedoom：到 [freedoom.github.io](https://freedoom.github.io/) 下載，解壓出 `freedoom1.wad` 或 `freedoom2.wad`。
   - Steam 版 DOOM + DOOM II：在收藏庫的遊戲上按右鍵 →「管理」→「瀏覽本機檔案」，`base` 資料夾裡的 `DOOM.WAD`、`base\doom2` 資料夾裡的 `DOOM2.WAD`（用 base 裡的原版，不是 `rerelease` 資料夾裡的）。
   - 共享版的 `DOOM1.WAD` 也可以。
3. 把 WAD 放進這個資料夾（整串貼到檔案總管的網址列就能打開）：
   `%USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen\ModData\dbfoxtw_probably-doom`
   還沒放 WAD 時，按 DOOM 會顯示這個路徑，按「複製路徑」就能直接貼上。
4. 在店裡點桌上的平板電腦，按 DOOM，再點一下畫面開始玩。

## 設定

主選單「模組」畫面的齒輪：WAD（放了好幾個時玩哪一個）、解析度、平板電腦本體也顯示畫面（預設開）、玩的時候遊戲的音樂和環境音靜音（預設開）。

## 按鍵

DOOM、DOOM II、Freedoom 都一樣：

- 前進、後退：方向鍵上下或 W、S；轉向：方向鍵左右
- 側移：A、D（或逗號、句號）
- 開火：Ctrl，或在 DOOM 畫面上按住滑鼠左鍵
- 開門、按開關：空白鍵
- 跑步：預設一直在跑，按住 Shift 變成走路
- 換武器：數字鍵 1～7
- 地圖：Tab
- 選單：Esc；存檔 F2、讀檔 F3、快速存檔 F6、快速讀檔 F9

想改按鍵的話，關掉遊戲後用記事本改上面那個資料夾裡的 `doom.cfg`。

## 相容性與注意事項

- 玩 DOOM 的時候，其他 mod 的快捷鍵也可能暫時不動作。
- 遊戲的中文只有簡體，這個 mod 的介面也是簡體字；裝了[繁體中文 mod](https://steamcommunity.com/sharedfiles/filedetails/?id=3828909380) 會變成繁體。DOOM 本身的文字是英文，不會翻譯。
- 換 WAD、改解析度要重開遊戲才生效。DOOM 的存檔每個 WAD 分開放。
- 滑鼠只能開火，不能轉向。
- Alt 鍵不會傳給 DOOM（免得按到 Alt+F4 關掉遊戲），所以側移用 A、D。
- 不會動到遊戲的存檔，也不需要額外權限。

## 開發

- 需要 .NET SDK 8 以上、Python 3.10 以上，和裝好的 Probably Stolen：編譯時參考遊戲的 `Probably Stolen_Data\Managed`（只讀，不附進 mod）。遊戲不在預設位置時加 `--game "遊戲資料夾"`，或設環境變數 `PS_GAME_DIR`。
- `python tools/install.py build` 編譯；`install` 裝到遊戲的 `Mods\dbfoxtw.probably-doom\`（`install.bat` 是除錯模式的安裝：每 10 秒把 DOOM 的耗時寫進 `Player.log`）；`uninstall` 移除；`package` 打包成 `build\ProbablyStolen-ProbablyDoom-<版本>.zip`。
- 離線測試：把 Freedoom 0.13.0 的 `freedoom1.wad`、`freedoom2.wad` 放進 `testdata/`（不進 repo），執行 `dotnet run --project verify -c Release`。
- 結構：`engine/ManagedDoom`、`engine/MeltySynth` 是上游的原始碼（改了什麼見 [VENDOR.md](VENDOR.md)）；`mod/src` 是接進遊戲的那一層，`mod/src/Core` 是不碰 Unity 的邏輯（離線測試也用）；`verify` 是離線測試；`Localization` 是設定視窗的文字；`package/README.txt` 是 zip 附的說明；`tools` 是建置、安裝、打包。

## 授權與致謝

- Probably DOOM 是自由軟體，授權是 **GPL-2.0-or-later**（[LICENSE](LICENSE)）。歡迎拿去改造，改過的版本發布時也要用 GPL 公開原始碼。
- DOOM 引擎是 sinshu 的 [ManagedDoom](https://github.com/sinshu/managed-doom)（GPL v2 or later）；音樂用 [MeltySynth](https://github.com/sinshu/meltysynth)（MIT）與 Tim Brechbill 的 TimGM6mb 音色庫（GPL v2）。來源、版本與我們改了什麼見 [VENDOR.md](VENDOR.md)。
- DOOM 是 id Software 的註冊商標。本 mod 與 id Software、ZeniMax、Bethesda 無關，也不含任何 DOOM 的遊戲資料。
- 遊戲的文字、名稱與素材，權利屬於 Questing Goose Studio。

---

## English

A mod for *Probably Stolen* (Questing Goose Studio) in the game's official mod format: play DOOM on the store's Tablet. It includes no DOOM game data (WAD); bring your own.

Download: [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3838535176) (just subscribe). This repository is its source code.

The code was written by AI (Claude); the design and in-game testing were done by the author. Bugs: [Issues](https://github.com/dbfoxtw/probably-doom/issues) or the Workshop comments. Please report problems here, not to the game developers.

### Features

- **DOOM button**: a DOOM button joins the buttons at the top of the Tablet screen. Press it to switch to DOOM, press it again to go back. Works at all three Tablet sizes.
- **Playing**: click the DOOM screen to start playing. Play with the keyboard; holding the left mouse button on the screen also fires. While you play, the game's hotkeys are paused. Click anywhere outside the DOOM screen to pause DOOM and get the hotkeys back. Quit Game in DOOM's menu takes you back to the Tablet's usual screen.
- **The Tablet itself plays along**: while you play, the Tablet on the desk shows a small copy of the DOOM screen.
- **Sound and music**: the music follows the game's music volume. While you play, the game's music and background sound are muted; they come back when DOOM pauses.
- **Saves**: DOOM's own save slots; your game saves are not touched.
- Supports the original DOOM1.WAD (shareware), DOOM.WAD and DOOM2.WAD, and the free Freedoom.

### How to use

1. Subscribe on the [Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3838535176), start the game, activate "Probably DOOM" on the Mods screen of the main menu, and restart as prompted.
2. Get a DOOM WAD (the game data):
   - Free: download Freedoom from [freedoom.github.io](https://freedoom.github.io/) and unzip `freedoom1.wad` or `freedoom2.wad`.
   - DOOM + DOOM II on Steam: right-click it in your Library → Manage → Browse local files. `DOOM.WAD` is in the `base` folder and `DOOM2.WAD` in `base\doom2` (use these originals, not the ones in the `rerelease` folder).
   - The shareware `DOOM1.WAD` works too.
3. Put the WAD in this folder (paste the whole line into File Explorer's address bar to open it):
   `%USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen\ModData\dbfoxtw_probably-doom`
   Before you add a WAD, pressing DOOM shows this path with a Copy path button, so you can paste it right away.
4. In the store, click the Tablet on the desk, press DOOM, then click the screen to play.

### Settings

The gear on the main menu's Mods screen: WAD (which one to play when there are several), Resolution, Show DOOM on the Tablet itself (on by default), Mute the game's music and background sound while playing (on by default).

### Controls

The same for DOOM, DOOM II and Freedoom:

- Move forward/back: Up/Down arrows or W/S; turn: Left/Right arrows
- Strafe: A/D (or comma/period)
- Fire: Ctrl, or hold the left mouse button on the DOOM screen
- Open doors, use switches: Space
- Run: you always run; hold Shift to walk
- Weapons: number keys 1–7
- Map: Tab
- Menu: Esc; save F2, load F3, quicksave F6, quickload F9

To change the keys, close the game and edit `doom.cfg` in the folder above with Notepad.

### Compatibility and notes

- While you play DOOM, other mods' hotkeys may not work either.
- Changing the WAD or the resolution takes effect after restarting the game. DOOM saves are kept separately for each WAD.
- The mouse only fires; it doesn't turn.
- Alt isn't passed to DOOM (so you can't close the game with Alt+F4 by accident); strafe with A/D instead.
- Doesn't touch your saves, and needs no extra permissions.

### Building

- Needs .NET SDK 8+, Python 3.10+ and an installed copy of Probably Stolen: the build references the game's `Probably Stolen_Data\Managed` (read-only, not shipped with the mod). If the game isn't in the default place, add `--game "game folder"` or set the `PS_GAME_DIR` environment variable.
- `python tools/install.py build` builds; `install` installs into the game's `Mods\dbfoxtw.probably-doom\` (`install.bat` installs in debug mode, which writes DOOM's timings to `Player.log` every 10 seconds); `uninstall` removes it; `package` makes `build\ProbablyStolen-ProbablyDoom-<version>.zip`.
- Offline tests: put `freedoom1.wad` and `freedoom2.wad` from Freedoom 0.13.0 in `testdata/` (not in the repository) and run `dotnet run --project verify -c Release`.
- Layout: `engine/ManagedDoom` and `engine/MeltySynth` are the upstream sources (what we changed is in [VENDOR.md](VENDOR.md), in Chinese); `mod/src` hooks into the game, and `mod/src/Core` is the logic that doesn't touch Unity (also used by the offline tests); `verify` holds the offline tests; `Localization` holds the settings window's text; `package/README.txt` is the readme in the zip; `tools` builds, installs and packages.

### License and credits

- Probably DOOM is free software under **GPL-2.0-or-later** ([LICENSE](LICENSE)). Feel free to reuse or modify it; modified versions you share must also be released under the GPL with their source code.
- The DOOM engine is [ManagedDoom](https://github.com/sinshu/managed-doom) by sinshu (GPL v2 or later); the music uses [MeltySynth](https://github.com/sinshu/meltysynth) (MIT) and Tim Brechbill's TimGM6mb sound font (GPL v2). Sources, versions and our changes are in [VENDOR.md](VENDOR.md).
- DOOM is a registered trademark of id Software. This mod is not affiliated with id Software, ZeniMax or Bethesda, and includes no DOOM game data.
- The game's text, names and assets belong to Questing Goose Studio.
