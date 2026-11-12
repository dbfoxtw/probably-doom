//
// Probably Doom 修改（2026-11-12）：設定檔改成宿主讀寫：拿掉 Config(string path)（File.ReadLines）與 Save(string path)（StreamWriter），改成 FromText／ToText 只處理文字，因為遊戲的 mod 掃描不准 mod 自己開檔案。
// Probably Doom 修改（2026-11-12）：引擎的訊息（Console.Write／WriteLine）改經過 EngineLog，宿主只在除錯模式寫進 log、帶自己的前綴（Unity 會把 Console 的輸出原樣寫進 Player.log）。
//
// Copyright (C) 1993-1996 Id Software, Inc.
// Copyright (C) 2019-2020 Nobuaki Tanaka
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
//



using System;
using System.Collections.Generic;
using System.Text;

namespace ManagedDoom
{
    public sealed class Config
    {
        public KeyBinding key_forward;
        public KeyBinding key_backward;
        public KeyBinding key_strafeleft;
        public KeyBinding key_straferight;
        public KeyBinding key_turnleft;
        public KeyBinding key_turnright;
        public KeyBinding key_fire;
        public KeyBinding key_use;
        public KeyBinding key_run;
        public KeyBinding key_strafe;

        public int mouse_sensitivity;
        public bool mouse_disableyaxis;

        public bool game_alwaysrun;

        public int video_screenwidth;
        public int video_screenheight;
        public bool video_fullscreen;
        public bool video_highresolution;
        public bool video_displaymessage;
        public int video_gamescreensize;
        public int video_gammacorrection;
        public int video_fpsscale;

        public int audio_soundvolume;
        public int audio_musicvolume;
        public bool audio_randompitch;
        public string audio_soundfont;
        public bool audio_musiceffect;

        private bool isRestoredFromFile;

        // Default settings.
        public Config()
        {
            key_forward = new KeyBinding(
                new DoomKey[]
                {
                    DoomKey.Up,
                    DoomKey.W
                });
            key_backward = new KeyBinding(
                new DoomKey[]
                {
                    DoomKey.Down,
                    DoomKey.S
                });
            key_strafeleft = new KeyBinding(
                new DoomKey[]
                {
                    DoomKey.A
                });
            key_straferight = new KeyBinding(
                new DoomKey[]
                {
                    DoomKey.D
                });
            key_turnleft = new KeyBinding(
                new DoomKey[]
                {
                    DoomKey.Left
                });
            key_turnright = new KeyBinding(
                new DoomKey[]
                {
                    DoomKey.Right
                });
            key_fire = new KeyBinding(
                new DoomKey[]
                {
                    DoomKey.LControl,
                    DoomKey.RControl
                },
                new DoomMouseButton[]
                {
                    DoomMouseButton.Mouse1
                });
            key_use = new KeyBinding(
                new DoomKey[]
                {
                    DoomKey.Space
                },
                new DoomMouseButton[]
                {
                    DoomMouseButton.Mouse2
                });
            key_run = new KeyBinding(
                new DoomKey[]
                {
                    DoomKey.LShift,
                    DoomKey.RShift
                });
            key_strafe = new KeyBinding(
                new DoomKey[]
                {
                    DoomKey.LAlt,
                    DoomKey.RAlt
                });

            mouse_sensitivity = 8;
            mouse_disableyaxis = false;

            game_alwaysrun = true;

            video_screenwidth = 640;
            video_screenheight = 400;
            video_fullscreen = false;
            video_highresolution = true;
            video_gamescreensize = 7;
            video_displaymessage = true;
            video_gammacorrection = 2;
            video_fpsscale = 2;

            audio_soundvolume = 8;
            audio_musicvolume = 8;
            audio_randompitch = true;
            audio_soundfont = "TimGM6mb.sf2";
            audio_musiceffect = true;

            isRestoredFromFile = false;
        }

        // Probably Doom：設定檔改成宿主讀寫（存檔資料夾的 ModData，用遊戲的 ModHelper），引擎只處理文字：
        // FromText 從設定檔的內容建 Config，ToText 輸出成設定檔的內容（格式同上游的 managed-doom.cfg，一行一個「名稱 = 值」）。
        // 原本的 Config(string path) 用 File.ReadLines 讀、Save(string path) 用 StreamWriter 寫，遊戲的 mod 掃描不准。
        // text 是 null 或空字串時全部用預設值（IsRestoredFromFile 是 false）；沒寫到的鍵、讀不懂的值也用預設值。
        public static Config FromText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new Config();
            }

            return new Config(text.Split('\n'));
        }

        private Config(string[] lines) : this()
        {
            try
            {
                EngineLog.Write("Restore settings: ");

                var dic = new Dictionary<string, string>();
                foreach (var line in lines)
                {
                    var split = line.Split('=', StringSplitOptions.RemoveEmptyEntries);
                    if (split.Length == 2)
                    {
                        dic[split[0].Trim()] = split[1].Trim();
                    }
                }

                key_forward = GetKeyBinding(dic, nameof(key_forward), key_forward);
                key_backward = GetKeyBinding(dic, nameof(key_backward), key_backward);
                key_strafeleft = GetKeyBinding(dic, nameof(key_strafeleft), key_strafeleft);
                key_straferight = GetKeyBinding(dic, nameof(key_straferight), key_straferight);
                key_turnleft = GetKeyBinding(dic, nameof(key_turnleft), key_turnleft);
                key_turnright = GetKeyBinding(dic, nameof(key_turnright), key_turnright);
                key_fire = GetKeyBinding(dic, nameof(key_fire), key_fire);
                key_use = GetKeyBinding(dic, nameof(key_use), key_use);
                key_run = GetKeyBinding(dic, nameof(key_run), key_run);
                key_strafe = GetKeyBinding(dic, nameof(key_strafe), key_strafe);

                mouse_sensitivity = GetInt(dic, nameof(mouse_sensitivity), mouse_sensitivity);
                mouse_disableyaxis = GetBool(dic, nameof(mouse_disableyaxis), mouse_disableyaxis);

                game_alwaysrun = GetBool(dic, nameof(game_alwaysrun), game_alwaysrun);

                video_screenwidth = GetInt(dic, nameof(video_screenwidth), video_screenwidth);
                video_screenheight = GetInt(dic, nameof(video_screenheight), video_screenheight);
                video_fullscreen = GetBool(dic, nameof(video_fullscreen), video_fullscreen);
                video_highresolution = GetBool(dic, nameof(video_highresolution), video_highresolution);
                video_displaymessage = GetBool(dic, nameof(video_displaymessage), video_displaymessage);
                video_gamescreensize = GetInt(dic, nameof(video_gamescreensize), video_gamescreensize);
                video_gammacorrection = GetInt(dic, nameof(video_gammacorrection), video_gammacorrection);
                video_fpsscale = GetInt(dic, nameof(video_fpsscale), video_fpsscale);

                audio_soundvolume = GetInt(dic, nameof(audio_soundvolume), audio_soundvolume);
                audio_musicvolume = GetInt(dic, nameof(audio_musicvolume), audio_musicvolume);
                audio_randompitch = GetBool(dic, nameof(audio_randompitch), audio_randompitch);
                audio_soundfont = GetString(dic, nameof(audio_soundfont), audio_soundfont);
                audio_musiceffect = GetBool(dic, nameof(audio_musiceffect), audio_musiceffect);

                isRestoredFromFile = true;

                EngineLog.WriteLine("OK");
            }
            catch
            {
                EngineLog.WriteLine("Failed");
            }
        }

        public string ToText()
        {
            var sb = new StringBuilder();

            sb.AppendLine(nameof(key_forward) + " = " + key_forward);
            sb.AppendLine(nameof(key_backward) + " = " + key_backward);
            sb.AppendLine(nameof(key_strafeleft) + " = " + key_strafeleft);
            sb.AppendLine(nameof(key_straferight) + " = " + key_straferight);
            sb.AppendLine(nameof(key_turnleft) + " = " + key_turnleft);
            sb.AppendLine(nameof(key_turnright) + " = " + key_turnright);
            sb.AppendLine(nameof(key_fire) + " = " + key_fire);
            sb.AppendLine(nameof(key_use) + " = " + key_use);
            sb.AppendLine(nameof(key_run) + " = " + key_run);
            sb.AppendLine(nameof(key_strafe) + " = " + key_strafe);

            sb.AppendLine(nameof(mouse_sensitivity) + " = " + mouse_sensitivity);
            sb.AppendLine(nameof(mouse_disableyaxis) + " = " + BoolToString(mouse_disableyaxis));

            sb.AppendLine(nameof(game_alwaysrun) + " = " + BoolToString(game_alwaysrun));

            sb.AppendLine(nameof(video_screenwidth) + " = " + video_screenwidth);
            sb.AppendLine(nameof(video_screenheight) + " = " + video_screenheight);
            sb.AppendLine(nameof(video_fullscreen) + " = " + BoolToString(video_fullscreen));
            sb.AppendLine(nameof(video_highresolution) + " = " + BoolToString(video_highresolution));
            sb.AppendLine(nameof(video_displaymessage) + " = " + BoolToString(video_displaymessage));
            sb.AppendLine(nameof(video_gamescreensize) + " = " + video_gamescreensize);
            sb.AppendLine(nameof(video_gammacorrection) + " = " + video_gammacorrection);
            sb.AppendLine(nameof(video_fpsscale) + " = " + video_fpsscale);

            sb.AppendLine(nameof(audio_soundvolume) + " = " + audio_soundvolume);
            sb.AppendLine(nameof(audio_musicvolume) + " = " + audio_musicvolume);
            sb.AppendLine(nameof(audio_randompitch) + " = " + BoolToString(audio_randompitch));
            sb.AppendLine(nameof(audio_soundfont) + " = " + audio_soundfont);
            sb.AppendLine(nameof(audio_musiceffect) + " = " + BoolToString(audio_musiceffect));

            return sb.ToString();
        }

        private static int GetInt(Dictionary<string, string> dic, string name, int defaultValue)
        {
            string stringValue;
            if (dic.TryGetValue(name, out stringValue))
            {
                int value;
                if (int.TryParse(stringValue, out value))
                {
                    return value;
                }
            }

            return defaultValue;
        }

        private static string GetString(Dictionary<string, string> dic, string name, string defaultValue)
        {
            string stringValue;
            if (dic.TryGetValue(name, out stringValue))
            {
                return stringValue;
            }

            return defaultValue;
        }

        private static bool GetBool(Dictionary<string, string> dic, string name, bool defaultValue)
        {
            string stringValue;
            if (dic.TryGetValue(name, out stringValue))
            {
                if (stringValue == "true")
                {
                    return true;
                }
                else if (stringValue == "false")
                {
                    return false;
                }
            }

            return defaultValue;
        }

        private static KeyBinding GetKeyBinding(Dictionary<string, string> dic, string name, KeyBinding defaultValue)
        {
            string stringValue;
            if (dic.TryGetValue(name, out stringValue))
            {
                return KeyBinding.Parse(stringValue);
            }

            return defaultValue;
        }

        private static string BoolToString(bool value)
        {
            return value ? "true" : "false";
        }

        public bool IsRestoredFromFile => isRestoredFromFile;
    }
}
