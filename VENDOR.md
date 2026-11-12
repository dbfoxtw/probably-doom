# 第三方元件與本地修改

Probably Doom 整個是 **GPL-2.0-or-later**（全文在 [LICENSE](LICENSE)）：用了 ManagedDoom（GPL v2 or later）與 TimGM6mb.sf2（GPL v2）。各元件的來源、版本、授權與我們改了什麼記在這裡（GPL v2 第 2a 條：改過的檔要有醒目的修改註記與日期；每個改過的檔開頭都有一行「Probably Doom 修改（日期）：…」）。

| 元件 | 放在哪裡 | 來源 | 版本 | 授權 |
|---|---|---|---|---|
| ManagedDoom | `engine/ManagedDoom/` | https://github.com/sinshu/managed-doom 的 `ManagedDoom/src/`（`Silk/` 以外） | commit `9365696eb44326a3aab72c4bab217f7db8a87c96`（2025-11-24） | GPL v2 or later（[licenses/LICENSE_ManagedDoom.txt](licenses/LICENSE_ManagedDoom.txt)） |
| MeltySynth | `engine/MeltySynth/` | https://github.com/sinshu/meltysynth 的 `MeltySynth/src/` | tag `v2.4.1`（commit `1ad7ba0b9f7324461b08aeca75a220ab1253de56`，2023-05-12，最新的 release tag） | MIT（[licenses/LICENSE_MeltySynth.txt](licenses/LICENSE_MeltySynth.txt)） |
| TimGM6mb.sf2（Tim Brechbill 的 GM 音色庫） | `mod/assets/TimGM6mb.sf2` | https://github.com/Hawkbat/ow-mod-doom 的 `Doom/TimGM6mb.sf2` | commit `43c9a7df340c33e4cd07b527c2955e6ffd378a86`；5,969,788 bytes，檔頭 `RIFF`…`sfbk`，git blob `e5f72e60926a3b62264cb5ad79304f7e204ccac7`，SHA-256 `c5378b62028c920cb11e4803327983fee2f2cdff5dc89c708e39da417e51c854` | GPL v2（[licenses/LICENSE_TimGM6mb.txt](licenses/LICENSE_TimGM6mb.txt)，ManagedDoom 的 `licenses/` 附的同一份） |
| modkit.py（建置、安裝、打包） | `tools/modkit.py` | https://github.com/dbfoxtw/probably-stolen-mods 的 `tools/modkit.py` | commit `79aa2d8a302830862109cfd0052237696d42e834`，原樣複製 | MIT（Copyright (c) 2026 dbfoxtw，那個 repo 的 `LICENSE`；檔案本身沒有授權標頭） |

- 原始碼照 upstream 原樣放，只把換行統一成 LF（upstream 的 git 存的也是 LF）、保留 UTF-8 BOM 與每個檔原本的 GPL 標頭；英文註解沒翻。
- ManagedDoom 的 `licenses/` 只放了用得到的三份（ManagedDoom、MeltySynth、TimGM6mb）。Silk.NET、TrippyGL、DrippyAL、GLFW、OpenAL Soft 是平台層 `Silk/` 用的，我們沒有用，所以沒放。
- MeltySynth 的 `ProjectInfo.cs`（只有給它自己測試專案的 `InternalsVisibleTo`）沒放。
- TimGM6mb.sf2：MuseScore 2.x 的 git 沒有這個檔（`v2.0.0`～`v2.3.2` 的 `share/sound/` 查過只有 FluidR3Mono），ManagedDoom 只放在 Release 的 zip 裡、不在 git。所以從 Outer Wilds 的 Doom mod（ow-mod-doom）的 git 取原檔，只取這一個資料檔，沒有下載任何執行檔或 zip。

## 測試用的 WAD（不散布）

離線測試（`verify/`）用 **Freedoom 0.13.0**：https://github.com/freedoom/freedoom/releases/tag/v0.13.0 的 `freedoom-0.13.0.zip`（24,143,781 bytes，BSD 3-Clause 類的授權，見 zip 裡的 `COPYING.txt`），解出 `freedoom1.wad`、`freedoom2.wad` 放 `testdata/`。`testdata/` 與 `*.wad` 都在 `.gitignore`：**不進 repo、不放進 mod**。mod 不附任何 WAD，玩家自己放。

## ManagedDoom：改了什麼（2026-11-12）

為什麼要改：遊戲（Probably Stolen）的 mod 掃描（`ModScanner`）不准 mod 自己開檔案（`System.IO.File`、`FileStream`、`Directory`、帶路徑的 `StreamReader`／`StreamWriter`）、開程序（`Process`）、開執行緒，也不准關掉遊戲。所以檔案存取全部改成宿主（mod 的 Unity 那一層，用遊戲的 `ModHelper` 讀寫）給資料，選單的 Quit Game 改成通知宿主。另外，上游用 `Console.Write`／`WriteLine` 印的訊息（「Open WAD files: OK」…）在 Unity 裡會原樣寫進 Player.log、沒有前綴，改成經過 `EngineLog` 交給宿主決定寫不寫（宿主只在除錯模式寫、帶自己的前綴）。

改過的檔（開頭都有修改註記）：

| 檔案 | 改了什麼 |
|---|---|
| `CommandLineArgs.cs` | 拿掉「把 WAD／.deh 拖到執行檔上」的判斷（要 `ConfigUtilities` 找檔）。參數改由宿主用程式碼建；`-iwad`、`-file`、`-deh` 仍然解析但不再使用 |
| `Config.cs` | 拿掉 `Config(string path)`（`File.ReadLines`）與 `Save(string path)`（`StreamWriter`），改成 `Config.FromText(string)`、`ToText()`：格式同 `managed-doom.cfg`，讀寫檔案由宿主做；`null`／空字串時全用預設值。訊息改經過 `EngineLog` |
| `Doom/Doom.cs` | 建構子多一個 `ISaveStore`（舊的 7 個參數的建構子留著，等於傳 null）；Quit 不結束程式：觸發 `QuitRequested` 事件、那次 `Update()` 回傳 `Completed`、清掉旗標，之後可以接著跑；`-playdemo` 播完回標題畫面（上游是結束程式） |
| `Doom/DeHackEd.cs` | 拿掉讀外部 `.deh` 檔（`-deh`）；WAD 裡的 `DEHACKED` lump 照舊。訊息改經過 `EngineLog` |
| `Doom/Common/DoomDebug.cs` | 拿掉寫檔的 `DumpMobjCsv`（上游也沒人呼叫） |
| `Doom/Game/Demo.cs` | 拿掉從檔案讀 demo 的建構子 |
| `Doom/Game/DoomGame.cs` | 存讀檔經過 `options.SaveStore`（原本讀寫執行檔旁的 `doomsav<N>.dsg`）；讀空的格子照上游丟例外 |
| `Doom/Game/GameContent.cs` | `GameContent(CommandLineArgs, params WadFile[])`：WAD 由宿主讀好交進來；`CreateDummy` 同樣改吃 `WadFile` |
| `Doom/Game/GameOptions.cs` | 加 `SaveStore` 屬性（預設 `NullSaveStore`） |
| `Doom/Game/SaveAndLoad.cs` | `Save` 回傳 `byte[]`、`Load` 吃 `byte[]`；加 `GetDescription(byte[])` |
| `Doom/Menu/DoomMenu.cs` | `new SaveSlots(doom.Options.SaveStore)` |
| `Doom/Menu/QuitConfirm.cs` | 確認離開後關掉確認畫面、計數歸零（上游到這裡程式就結束了；不歸零的話玩家回來時會卡在會吃掉所有按鍵的確認畫面） |
| `Doom/Menu/SaveSlots.cs` | 各格的說明從 `ISaveStore` 讀（原本讀檔，還用了 .NET 7 才有的 `Stream.ReadExactly`）；加 `SlotCount` |
| `Doom/Opening/DemoPlayback.cs` | 拿掉讀 demo 檔的分支，只找 WAD 裡的 lump |
| `Doom/Wad/Wad.cs` | `Wad(params WadFile[])`：`MemoryStream` 代替 `FileStream`；遊戲模式照舊靠檔名判斷（doom1、doom、doom2、freedoom1、freedoom2、plutonia、tnt）。訊息改經過 `EngineLog` |
| `Doom/Map/Map.cs`、`Doom/Graphics/ColorMap.cs`、`FlatLookup.cs`、`Palette.cs`、`SpriteLookup.cs`、`TextureAnimation.cs`、`TextureLookup.cs` | 只把 `Console.Write`／`WriteLine` 換成 `EngineLog.Write`／`WriteLine`（2026-11-12 第一次實機測試後；Probably Doom 需求.md 決定 30） |

刪掉的檔：`ConfigUtilities.cs`（`Process.GetCurrentProcess()`、`File`、`Directory`，找執行檔旁的 WAD、設定檔、音色庫）。整個 `Silk/`（平台層：視窗、OpenGL、OpenAL、輸入、`DllImport`、`Thread.Sleep`、`unsafe`）不編、沒放進來。

從 `Silk/` 搬出來改寫的檔（GPL，標頭照原檔，開頭有修改註記）：

| 檔案 | 來源 | 內容 |
|---|---|---|
| `Audio/MusicDecoders.cs` | `Silk/SilkMusic.cs` | `MusDecoder`、`MidiDecoder` 照搬（原本是巢狀私有類別，改成公開；介面 `IDecoder` 改名 `IMusicDecoder`）；`MusicDecoders.Create(byte[], bool)` 同 `SilkMusic.ReadData` |
| `Audio/SynthMusic.cs` | `Silk/SilkMusic.cs` | `IMusic` 的實作：選曲、MeltySynth 合成；拿掉 DrippyAL 的播放，改成宿主呼叫 `Render` 要 PCM（pull 式），音色庫由宿主給 `byte[]` |
| `Audio/DmxSound.cs` | `Silk/SilkSound.cs` | 解 WAD 音效 lump（DMX）的 `GetSamples`、`ContainsDmxPadding`、`GetAmplitude`；多檢查一下檔頭寫的長度超過 lump 的情況（上游會丟例外） |
| `Video/FrameBufferVideo.cs` | `Silk/SilkVideo.cs` | `IVideo` 的實作：拿掉 OpenGL，只把 `Renderer` 畫進一塊 RGBA 緩衝給宿主 |

我們自己新增的檔（GPL-2.0-or-later，Copyright (C) 2026 dbfoxtw）：`Doom/Game/ISaveStore.cs`、`Doom/Game/NullSaveStore.cs`、`Doom/Wad/WadFile.cs`、`EngineLog.cs`（引擎的訊息：`Write` 的半行存著、`WriteLine` 湊成一整行交給宿主設的 `Sink`，`Sink` 是 null 時丟掉；不用 `Console.SetOut`，那是整個程序共用的）。

## MeltySynth：改了什麼（2026-11-12）

| 檔案 | 改了什麼 |
|---|---|
| `SoundFont.cs` | 拿掉 `SoundFont(string path)`（`FileStream`），用 `SoundFont(Stream)` |
| `MidiFile.cs` | 拿掉吃路徑的三個建構子，用吃 `Stream` 的 |
| `Synthesizer.cs` | 拿掉吃音色庫路徑的兩個建構子，用吃 `SoundFont` 的 |

其他照原樣（`Span<T>`、`MemoryMarshal`、`Vector<T>` 都留著：mod 編成 netstandard2.1，遊戲的 Mono 都有）。

## 引擎和宿主之間（給接進遊戲的那一層）

- **建立**：`new GameContent(new CommandLineArgs(new string[0]), new WadFile("DOOM2.WAD", bytes))` → `new Doom(args, config, content, video, sound, music, userInput, saveStore)`（`null` 的用 Null 版本）。`DeHackEd` 與 `DoomInfo` 是靜態的：同一個程式裡換 WAD 不會還原前一個 WAD 的 `DEHACKED` 改過的表，所以一次只載入一個 WAD。
- **計時**：35 Hz（`GameConst.TicRate`），每滿 1/35 秒呼叫一次 `doom.Update()`，時間由宿主累加；算圖時用 `frameFrac` 插值（上游 `SilkDoom`）。
- **畫面**：`FrameBufferVideo.Render(doom, frameFrac)` 畫進 `Data`：RGBA32、**直行優先**（畫素 (x, y) 在 `4 * (x * Height + y)`），640×400（`video_highresolution = true`，預設）或 320×200。詳見 `FrameBufferVideo` 的註解。
- **輸入**：`doom.PostEvent(new DoomEvent(EventType.KeyDown / KeyUp, DoomKey.X))`；玩家的移動由 `IUserInput.BuildTicCmd` 產生（上游 `SilkUserInput` 照設定的按鍵綁定與按住的鍵算）。
- **音效**：`ISound`（上游介面）；WAD 的音效用 `DmxSound.FromWad(wad, "DS" + 名稱)` 解。
- **音樂**：`SynthMusic`（`IMusic`）：建構時給音色庫的 `byte[]`；音訊執行緒呼叫 `Render(float[] buffer, int offset, int frameCount)`，44100 Hz、立體聲左右交錯、-1～1。
- **存檔**：`ISaveStore`（`byte[] Read(int slot)`、`void Write(int slot, byte[] data)`，6 格，內容是原版的 `.dsg` 格式）。
- **設定**：`Config.FromText(string)`、`config.ToText()`。
- **離開**：`doom.QuitRequested` 事件（同一次 `Update()` 回傳 `UpdateResult.Completed`），程式不結束、之後可以接著跑。
- **訊息**：`EngineLog.Sink = line => …`（一整行、不含換行；null＝丟掉）。
