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
using System.Diagnostics;
using System.IO;
using System.Linq;
using ManagedDoom;
using ManagedDoom.Audio;
using ManagedDoom.Video;

namespace ProbablyDoom.Verify
{
    /// <summary>
    /// 離線測試：不開遊戲，直接跑移植後的引擎（和 mod 同一批原始碼）。
    /// 1. 載入 WAD、靠檔名判斷遊戲模式；2. 標題畫面與 demo 跑 700 tic、輸出畫面；3. 存檔成 byte[] 再讀回來；
    /// 4. 音色庫＋MeltySynth 合成標題音樂、解音效 lump；5. 設定字串來回、解析度跟著設定；6. Quit Game 只通知宿主、程式不結束。
    /// 7. mod 宿主層的純邏輯（mod\src\Core\，不碰 Unity：WAD 挑選、存檔路徑、畫面轉置與小畫面、焦點、快捷鍵、計時、語言、版面、鍵盤）；
    /// 8. 用實際的畫面跑一次轉置與小畫面（HostTests.cs）。
    /// </summary>
    internal static partial class Program
    {
        static int _failures;
        static string _root, _out;

        static int Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            _root = FindRoot();
            _out = Path.Combine(_root, "verify", "out");
            Directory.CreateDirectory(_out);

            var wads = args.Length > 0
                ? args
                : new[] { Path.Combine(_root, "testdata", "freedoom1.wad"), Path.Combine(_root, "testdata", "freedoom2.wad") };
            var sf2Path = Path.Combine(_root, "mod", "assets", "TimGM6mb.sf2");

            Console.WriteLine($"repo：{_root}");
            Console.WriteLine($"WAD：{string.Join("、", wads)}");
            Console.WriteLine();

            Run("0. 遊戲模式靠檔名判斷（假的空 WAD）", TestModeByName);
            RunHostTests();

            // 引擎的 DeHackEd 與 DoomInfo 是靜態的：同一個程式裡載入第二個 WAD 時，第一個 WAD 的 DEHACKED 改過的表不會還原。
            // 遊戲裡一次只載入一個 WAD（換 WAD 要重開遊戲），這裡照順序跑，第二個 WAD 的結果僅供參考。
            var first = true;
            foreach (var path in wads)
            {
                if (!File.Exists(path))
                {
                    Fail($"找不到 WAD：{path}");
                    continue;
                }
                var name = Path.GetFileName(path);
                var data = File.ReadAllBytes(path);
                Console.WriteLine($"==== {name}（{data.Length:N0} bytes）");

                GameContent content = null;
                Run($"1. 載入 {name}、判斷遊戲模式", () => content = TestLoad(name, data));
                if (content == null) continue;

                Bgm titleBgm = Bgm.NONE;
                Run($"2. {name} 標題畫面與 demo（640×400）", () => titleBgm = TestOpening(content, name, true, measure: true));
                Run($"2. {name} 標題畫面與 demo（320×200）", () => TestOpening(content, name, false, measure: false));
                Run($"3. {name} 開新遊戲、存檔、讀檔", () => TestSaveLoad(content));
                Run($"4. {name} 合成標題音樂、解音效", () => TestAudio(content, name, titleBgm, sf2Path));
                if (first)
                {
                    Run("5. 設定字串來回、解析度跟著設定", () => TestConfig(content));
                    Run("6. Quit Game 只通知宿主", () => TestQuit(content));
                    Run($"8. {name} 實際的畫面轉成貼圖的排法、平板本體的小畫面", () => TestRealFrame(content, name));
                }
                first = false;
                content.Dispose();
            }

            Console.WriteLine();
            Console.WriteLine(_failures == 0 ? "全部通過" : $"失敗 {_failures} 項");
            return _failures == 0 ? 0 : 1;
        }

        // ---------------------------------------------------------------- 0、1

        static void TestModeByName()
        {
            // 最小的合法 WAD：只有檔頭、沒有 lump
            var empty = new byte[12];
            System.Text.Encoding.ASCII.GetBytes("PWAD").CopyTo(empty, 0);
            BitConverter.GetBytes(0).CopyTo(empty, 4);
            BitConverter.GetBytes(12).CopyTo(empty, 8);

            var cases = new (string File, GameMode Mode, MissionPack Pack)[]
            {
                ("DOOM1.WAD", GameMode.Shareware, MissionPack.Doom2),
                ("DOOM.WAD", GameMode.Retail, MissionPack.Doom2),
                ("doom2.wad", GameMode.Commercial, MissionPack.Doom2),
                ("PLUTONIA.WAD", GameMode.Commercial, MissionPack.Plutonia),
                ("TNT.WAD", GameMode.Commercial, MissionPack.Tnt),
                ("freedoom1.wad", GameMode.Retail, MissionPack.Doom2),
                ("freedoom2.wad", GameMode.Commercial, MissionPack.Doom2),
                (@"C:\some\folder\Doom2.WAD", GameMode.Commercial, MissionPack.Doom2),
                ("mywad.wad", GameMode.Indetermined, MissionPack.Doom2),
            };
            foreach (var c in cases)
            {
                var wad = new Wad(new WadFile(c.File, empty));
                Check(wad.GameMode == c.Mode && wad.MissionPack == c.Pack,
                      $"{c.File} → {wad.GameMode}／{wad.MissionPack}（預期 {c.Mode}／{c.Pack}）");
            }
        }

        static GameContent TestLoad(string name, byte[] data)
        {
            var content = new GameContent(new CommandLineArgs(Array.Empty<string>()), new WadFile(name, data));
            var wad = content.Wad;
            var expected = ExpectedMode(name);
            Check(wad.GameMode == expected,
                  $"遊戲模式 {wad.GameMode}（預期 {expected}）、{wad.MissionPack}、{wad.GameVersion}、lump {wad.LumpInfos.Count} 個");
            return content;
        }

        static GameMode ExpectedMode(string fileName)
        {
            switch (Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant())
            {
                case "doom1": return GameMode.Shareware;
                case "doom": case "freedoom1": return GameMode.Retail;
                case "doom2": case "freedoom2": case "plutonia": case "tnt": return GameMode.Commercial;
                default: return GameMode.Indetermined;
            }
        }

        // ---------------------------------------------------------------- 2

        static readonly int[] SnapshotTics = { 1, 150, 300, 450, 700 };

        /// <summary>標題畫面與 demo 跑 35×20 個 tic，輸出幾張畫面；回傳標題畫面時引擎要放的音樂（給第 4 項合成）。</summary>
        static Bgm TestOpening(GameContent content, string wadName, bool highRes, bool measure)
        {
            var config = new Config();
            config.video_highresolution = highRes;
            var video = new FrameBufferVideo(config, content);
            var sound = new RecordingSound();
            var music = new RecordingMusic();
            var doom = new Doom(NoArgs(), config, content, video, sound, music, null, null);

            var expectedW = highRes ? 640 : 320;
            var expectedH = highRes ? 400 : 200;
            Check(video.Width == expectedW && video.Height == expectedH && video.Data.Length == 4 * expectedW * expectedH,
                  $"畫面 {video.Width}×{video.Height}，緩衝 {video.Data.Length:N0} bytes");

            var tics = GameConst.TicRate * 20;
            var states = new List<string>();
            var updateTicks = 0L;
            var renderTicks = 0L;
            var renders = 0;
            var sw = new Stopwatch();
            var files = new List<string>();
            var demoTic = -1;
            for (var tic = 1; tic <= tics; tic++)
            {
                sw.Restart();
                var result = doom.Update();
                updateTicks += sw.ElapsedTicks;
                if (result == UpdateResult.Completed)
                {
                    Fail($"第 {tic} tic 回傳 Completed（標題畫面不該要求離開）");
                }

                var state = doom.State == DoomState.Opening ? "Opening/" + doom.Opening.State : doom.State.ToString();
                if (states.Count == 0 || states[states.Count - 1] != state) states.Add(state);
                if (demoTic < 0 && doom.State == DoomState.Opening && doom.Opening.State == OpeningSequenceState.Demo) demoTic = tic;

                if (measure || Array.IndexOf(SnapshotTics, tic) >= 0)
                {
                    sw.Restart();
                    video.Render(doom, Fixed.One);
                    renderTicks += sw.ElapsedTicks;
                    renders++;
                }

                if (Array.IndexOf(SnapshotTics, tic) >= 0)
                {
                    var file = Path.Combine(_out, $"{Path.GetFileNameWithoutExtension(wadName)}_{video.Width}x{video.Height}_tic{tic:D4}.png");
                    Images.SaveFrame(file, video.Data, video.Width, video.Height);
                    files.Add(file);
                }
            }

            Check(true, $"跑了 {tics} tic 沒有例外；狀態經過 {string.Join(" → ", states)}");
            Check(demoTic > 0, demoTic > 0 ? $"第 {demoTic} tic 開始播 WAD 裡的標題 demo" : "沒播到標題 demo");
            Check(NotBlank(video.Data), "最後一張畫面不是全黑或單色");
            Info($"引擎要放的音樂：{(music.Started.Count == 0 ? "（無）" : string.Join("、", music.Started))}；" +
                 $"要播的音效 {sound.StartCount} 次（{sound.Seen.Count} 種）");
            foreach (var f in files) Info($"畫面：{f}");

            if (measure)
            {
                var ms = 1000.0 / Stopwatch.Frequency;
                Info($"效能（.NET 8，只是參考）：每 tic Update 平均 {updateTicks * ms / tics:0.000} ms；" +
                     $"{video.Width}×{video.Height} 每次 Render 平均 {renderTicks * ms / renders:0.000} ms（{renders} 次）");
            }

            return music.Started.Count > 0 ? music.Started[0] : Bgm.NONE;
        }

        static bool NotBlank(byte[] rgba)
        {
            var first = BitConverter.ToUInt32(rgba, 0);
            for (var i = 4; i < rgba.Length; i += 4)
            {
                if (BitConverter.ToUInt32(rgba, i) != first) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- 3

        struct Snapshot
        {
            public int Episode, Map, LevelTime, Health, Armor;
            public int X, Y, Z, MomX, MomY;
            public uint Angle;

            public override string ToString() =>
                $"第 {Episode} 集第 {Map} 關 tic {LevelTime}、位置 ({X / 65536.0:0.00}, {Y / 65536.0:0.00}, {Z / 65536.0:0.00})、" +
                $"角度 {Angle / 4294967296.0 * 360:0.0}°、體力 {Health}、護甲 {Armor}";

            public bool SameAs(Snapshot o) =>
                Episode == o.Episode && Map == o.Map && LevelTime == o.LevelTime && Health == o.Health && Armor == o.Armor &&
                X == o.X && Y == o.Y && Z == o.Z && MomX == o.MomX && MomY == o.MomY && Angle == o.Angle;
        }

        static Snapshot Take(DoomGame game)
        {
            var world = game.World;
            var player = world.ConsolePlayer;
            var mobj = player.Mobj;
            return new Snapshot
            {
                Episode = world.Options.Episode,
                Map = world.Options.Map,
                LevelTime = world.LevelTime,
                Health = player.Health,
                Armor = player.ArmorPoints,
                X = mobj.X.Data,
                Y = mobj.Y.Data,
                Z = mobj.Z.Data,
                MomX = mobj.MomX.Data,
                MomY = mobj.MomY.Data,
                Angle = mobj.Angle.Data,
            };
        }

        static void Tick(Doom doom, int count)
        {
            for (var i = 0; i < count; i++) doom.Update();
        }

        static void TestSaveLoad(GameContent content)
        {
            var store = new MemorySaveStore();
            var input = new ScriptedInput();
            var doom = new Doom(NoArgs(), new Config(), content, null, null, null, input, store);

            doom.NewGame(GameSkill.Medium, 1, 1);
            var waited = 0;
            while (!(doom.State == DoomState.Game && doom.Game.State == GameState.Level && !doom.Wiping) && waited < 300)
            {
                doom.Update();
                waited++;
            }
            Check(doom.State == DoomState.Game && doom.Game.State == GameState.Level, $"開新遊戲後 {waited} tic 進到關卡");
            var start = Take(doom.Game);
            Info($"開始：{start}");

            // 往前走、轉彎，讓玩家的位置與角度都變
            input.Forward = 25;
            Tick(doom, 40);
            input.Turn = 640;
            Tick(doom, 20);
            input.Forward = 0;
            input.Turn = 0;

            var atSave = Take(doom.Game);
            Check(atSave.X != start.X || atSave.Y != start.Y, $"走動後：{atSave}");

            // 經過 ISaveStore 存檔：存檔在下一次 Update 一開始做（存的是 atSave），之後那個 tic 照常跑
            Check(doom.SaveGame(0, "VERIFY SAVE"), "doom.SaveGame(0) 接受");
            doom.Update();
            var afterSaveTick = Take(doom.Game);
            Check(store.Writes == 1 && store.Slots.ContainsKey(0), $"ISaveStore.Write 叫了 {store.Writes} 次，第 0 格 {(store.Slots.TryGetValue(0, out var saved) ? saved.Length : 0):N0} bytes");
            Check(SaveAndLoad.GetDescription(saved) == "VERIFY SAVE", $"存檔開頭的說明文字「{SaveAndLoad.GetDescription(saved)}」");
            Check(new SaveSlots(store)[0] == "VERIFY SAVE" && new SaveSlots(store)[1] == null, "選單的存檔格從 ISaveStore 列出說明（第 0 格有、第 1 格空）");

            // 再走一段，狀態跑掉
            input.Forward = 40;
            input.Turn = -500;
            Tick(doom, 60);
            input.Forward = 0;
            input.Turn = 0;
            var diverged = Take(doom.Game);
            Check(!diverged.SameAs(afterSaveTick), $"存檔後再走一段：{diverged}");

            // 經過 ISaveStore 讀檔：讀檔在下一次 Update 一開始做，之後同樣跑一個 tic（輸入同樣是不動），應該和存檔後那個 tic 一樣
            var readsBefore = store.Reads;
            doom.LoadGame(0);
            doom.Update();
            var loaded = Take(doom.Game);
            Check(store.Reads > readsBefore, "讀檔經過 ISaveStore.Read");
            Check(loaded.SameAs(afterSaveTick), $"讀檔後跑一個 tic：{loaded}（預期 {afterSaveTick}）");

            // 直接用 byte[] 讀（不跑 tic）：應該和存檔那一刻完全一樣；再存一次和原本的存檔比
            SaveAndLoad.Load(doom.Game, saved);
            var direct = Take(doom.Game);
            Check(direct.SameAs(atSave), $"SaveAndLoad.Load(byte[]) 後：{direct}（預期 {atSave}）");
            var resaved = SaveAndLoad.Save(doom.Game, "VERIFY SAVE");
            var same = resaved.AsSpan().SequenceEqual(saved);
            Info($"讀回來馬上再存一次：{resaved.Length:N0} bytes，和原本的存檔{(same ? "完全相同" : "不同（原版的存檔不含亂數狀態等，僅供參考）")}");

            // 讀空的格子：照上游丟例外（選單本來就不讓玩家讀空的格子）。
            // 上面讀檔觸發了換畫面的特效（wipe），特效跑完之前引擎不跑遊戲的 tic，所以多跑幾次 Update
            try
            {
                doom.LoadGame(3);
                Tick(doom, 200);
                Fail("讀空的第 3 格沒有丟例外");
            }
            catch (Exception e)
            {
                Check(true, $"讀空的第 3 格丟例外：{e.Message}");
            }
        }

        // ---------------------------------------------------------------- 4

        static void TestAudio(GameContent content, string wadName, Bgm bgm, string sf2Path)
        {
            if (bgm == Bgm.NONE)
            {
                bgm = content.Wad.GameMode == GameMode.Commercial ? Bgm.DM2TTL : Bgm.INTRO;
                Info($"標題畫面沒要音樂，改用 {bgm}");
            }

            var lumpName = "D_" + DoomInfo.BgmNames[(int)bgm].ToString().ToUpper();
            var lump = content.Wad.ReadLump(lumpName);
            var format = lump.Length >= 4 && lump[0] == 'M' && lump[1] == 'U' && lump[2] == 'S' ? "MUS"
                : lump.Length >= 4 && lump[0] == 'M' && lump[1] == 'T' && lump[2] == 'h' && lump[3] == 'd' ? "MIDI" : "不明";
            Info($"{lumpName}：{lump.Length:N0} bytes，{format} 格式");

            var sf2 = File.ReadAllBytes(sf2Path);
            var sw = Stopwatch.StartNew();
            var music = new SynthMusic(new Config(), content, sf2);
            Info($"音色庫 {sf2.Length:N0} bytes，載入 {sw.ElapsedMilliseconds} ms");

            music.StartMusic(bgm, true);
            var seconds = 5;
            var frames = SynthMusic.SampleRate * seconds;
            var pcm = new float[frames * SynthMusic.ChannelCount];
            // 照 Unity 的音訊回呼那樣一小段一小段要（1024 個取樣框）
            sw.Restart();
            for (var pos = 0; pos < frames; pos += 1024)
            {
                music.Render(pcm, pos * 2, Math.Min(1024, frames - pos));
            }
            var synthMs = sw.Elapsed.TotalMilliseconds;

            var peak = 0f;
            var sum = 0.0;
            foreach (var s in pcm)
            {
                peak = Math.Max(peak, Math.Abs(s));
                sum += s * s;
            }
            var rms = Math.Sqrt(sum / pcm.Length);
            Check(peak > 0.01f && rms > 0.001, $"合成 {seconds} 秒（{SynthMusic.SampleRate} Hz 立體聲）：最大振幅 {peak:0.000}、RMS {rms:0.0000}（不是靜音）");
            Info($"合成 {seconds} 秒花 {synthMs:0} ms（.NET 8，只是參考）");
            var wav = Path.Combine(_out, $"{Path.GetFileNameWithoutExtension(wadName)}_{lumpName}.wav");
            Images.SaveWav(wav, pcm, SynthMusic.SampleRate);
            Info($"音樂：{wav}");

            // MUS 解碼器：Freedoom 的音樂都是 MIDI，原版 Doom 的是 MUS，這裡用一段自己組的 MUS（中央 C 響 0.5 秒）確認 MUS 那條路也出得了聲
            var mus = new byte[]
            {
                (byte)'M', (byte)'U', (byte)'S', 0x1A,
                8, 0,       // 樂譜長度
                16, 0,      // 樂譜從第 16 byte 開始
                1, 0,       // 主要聲道數
                0, 0,       // 次要聲道數
                0, 0,       // 樂器數
                0, 0,       // 保留
                0x90, 0x80 | 60, 100, 70,   // 最後一個事件、按下音符（第 0 聲道）：音高 60、力度 100；之後等 70/140 秒
                0x80, 60, 70,               // 最後一個事件、放開音符 60；之後等 70/140 秒
                0x60,                       // 樂譜結束
            };
            var decoder = MusicDecoders.Create(mus, false);
            var settings = new MeltySynth.SynthesizerSettings(MusDecoder.SampleRate) { BlockSize = MusDecoder.BlockLength };
            var synth = new MeltySynth.Synthesizer(new MeltySynth.SoundFont(new MemoryStream(sf2, false)), settings);
            var musLeft = new float[MusDecoder.SampleRate];
            var musRight = new float[MusDecoder.SampleRate];
            decoder.RenderWaveform(synth, musLeft, musRight);
            var musPeak = musLeft.Concat(musRight).Max(Math.Abs);
            Check(decoder is MusDecoder && musPeak > 0.01f, $"MUS 解碼（{decoder.GetType().Name}）合成 1 秒：最大振幅 {musPeak:0.000}");

            // 音效：DS* lump（DMX 格式）
            var pistol = DmxSound.FromWad(content.Wad, "DSPISTOL");
            Check(pistol != null && pistol.SampleRate > 0 && pistol.Samples.Length > 0,
                  pistol == null ? "DSPISTOL 解不出來" : $"DSPISTOL：取樣率 {pistol.SampleRate} Hz、{pistol.Samples.Length:N0} 個取樣（{pistol.Duration:0.000} 秒）、振幅 {pistol.Amplitude:0.00}");
            var decoded = 0;
            var missing = 0;
            for (var i = 1; i < DoomInfo.SfxNames.Length; i++)
            {
                var name = "DS" + DoomInfo.SfxNames[i].ToString().ToUpper();
                if (content.Wad.GetLumpNumber(name) == -1)
                {
                    missing++;
                    continue;
                }
                if (DmxSound.FromWad(content.Wad, name) != null) decoded++;
            }
            Info($"音效 lump：解出 {decoded} 個，WAD 裡沒有的 {missing} 個（共 {DoomInfo.SfxNames.Length - 1} 種）");
        }

        // ---------------------------------------------------------------- 5

        static void TestConfig(GameContent content)
        {
            var defaults = Config.FromText(null);
            Check(!defaults.IsRestoredFromFile && defaults.video_highresolution && defaults.audio_musicvolume == 8,
                  "沒有字串時用預設值（640×400、音樂音量 8）");

            var c1 = new Config();
            c1.video_highresolution = false;
            c1.audio_musicvolume = 11;
            c1.audio_soundvolume = 3;
            c1.game_alwaysrun = false;
            c1.key_fire = KeyBinding.Parse("f, space");
            var text = c1.ToText();
            var c2 = Config.FromText(text);
            Check(c2.IsRestoredFromFile && text == c2.ToText(), "輸出 → 讀回 → 再輸出，字串完全一樣");
            Check(!c2.video_highresolution && c2.audio_musicvolume == 11 && c2.audio_soundvolume == 3 && !c2.game_alwaysrun &&
                  c2.key_fire.ToString() == c1.key_fire.ToString(),
                  $"讀回的值一致（key_fire = {c2.key_fire}）");
            var crlf = Config.FromText(text.Replace("\r\n", "\n").Replace("\n", "\r\n"));
            Check(crlf.ToText() == text, "Windows 換行（CRLF）的設定檔也讀得懂");
            var partial = Config.FromText("audio_musicvolume = 4\nvideo_highresolution = nonsense\n");
            Check(partial.audio_musicvolume == 4 && partial.video_highresolution, "只寫一部分、值讀不懂時其他用預設值");

            var low = new FrameBufferVideo(c2, content);
            var high = new FrameBufferVideo(Config.FromText("video_highresolution = true"), content);
            Check(low.Width == 320 && low.Height == 200 && high.Width == 640 && high.Height == 400,
                  $"video_highresolution = false → {low.Width}×{low.Height}；true → {high.Width}×{high.Height}");
            Info("設定檔內容：" + text.Replace("\r", "").Replace("\n", "｜"));
        }

        // ---------------------------------------------------------------- 6

        static void TestQuit(GameContent content)
        {
            var doom = new Doom(NoArgs(), new Config(), content, null, null, null, null, null);
            var requested = 0;
            doom.QuitRequested += () => requested++;
            Tick(doom, 5);

            // F10 → 確認畫面 → Y
            var completed = Press(doom, DoomKey.F10, 1) + Press(doom, DoomKey.Y, 70);
            Check(requested == 1 && completed == 1, $"F10 → Y：QuitRequested {requested} 次、Update 回傳 Completed {completed} 次");
            Check(!doom.Menu.Active, "離開後選單已經關掉（玩家回來時不會卡在確認畫面）");
            // 回來後按鍵照常有反應：Esc 開選單、再按 Esc 關掉
            completed = Press(doom, DoomKey.Escape, 1);
            var opened = doom.Menu.Active;
            completed += Press(doom, DoomKey.Escape, 1);
            Tick(doom, 35);
            Check(opened && !doom.Menu.Active, "回來後按 Esc 開得了選單、再按 Esc 關得掉");
            Check(requested == 1 && completed == 0, "之後照常跑 35 tic，沒有再要求離開；程式也沒有結束");

            // 主選單的 Quit Game：Esc 開選單 → 上（從 New Game 繞到最後一項 Quit Game）→ Enter → Y
            // 標題畫面換頁的特效（wipe）跑的時候引擎不處理按鍵、選單也不前進（上游就是這樣），所以按 Y 之後多跑一些 tic
            completed = Press(doom, DoomKey.Escape, 1) + Press(doom, DoomKey.Up, 1) + Press(doom, DoomKey.Enter, 1) + Press(doom, DoomKey.Y, 200);
            Check(requested == 2 && completed == 1, $"選單的 Quit Game：QuitRequested 共 {requested} 次、這次 Completed {completed} 次");

            // N 取消：不通知
            completed = Press(doom, DoomKey.F10, 1) + Press(doom, DoomKey.N, 200);
            Check(requested == 2 && completed == 0 && !doom.Menu.Active, "F10 → N 取消時不通知宿主");
        }

        /// <summary>按一下鍵（KeyDown＋KeyUp），之後再跑 tics 個 tic；回傳這段期間 Update 回傳 Completed 的次數。</summary>
        static int Press(Doom doom, DoomKey key, int tics)
        {
            var completed = 0;
            doom.PostEvent(new DoomEvent(EventType.KeyDown, key));
            if (doom.Update() == UpdateResult.Completed) completed++;
            doom.PostEvent(new DoomEvent(EventType.KeyUp, key));
            for (var i = 0; i < tics; i++)
            {
                if (doom.Update() == UpdateResult.Completed) completed++;
            }
            return completed;
        }

        static CommandLineArgs NoArgs() => new CommandLineArgs(Array.Empty<string>());

        // ---------------------------------------------------------------- 共用

        static void Run(string title, Action test)
        {
            Console.WriteLine($"[{title}]");
            try
            {
                test();
            }
            catch (Exception e)
            {
                Fail($"丟出例外：{e}");
            }
        }

        static void Check(bool ok, string msg)
        {
            Console.WriteLine($"  {(ok ? "✓" : "✗")} {msg}");
            if (!ok) _failures++;
        }

        static void Fail(string msg) => Check(false, msg);

        static void Info(string msg) => Console.WriteLine($"  · {msg}");

        static string FindRoot()
        {
            for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            {
                if (Directory.Exists(Path.Combine(d.FullName, "engine")) && File.Exists(Path.Combine(d.FullName, "VENDOR.md")))
                {
                    return d.FullName;
                }
            }
            return Directory.GetCurrentDirectory();
        }
    }
}
