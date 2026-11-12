//
// Probably Doom 新增（2026-11-12）：引擎的訊息（上游用 Console.Write／WriteLine 印的「Open WAD files: OK」那些）改交給宿主決定寫不寫。
//
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
//



using System;
using System.Text;

namespace ManagedDoom
{
    /// <summary>
    /// 引擎自己的訊息。上游直接 Console.Write，在 Unity 裡會原樣寫進 Player.log、沒有前綴，看 log 的人不知道是誰印的
    /// （Probably Doom 需求.md 決定 30）。這裡先把 Write 的半行存著，湊成一整行（WriteLine）才交給 <see cref="Sink"/>；
    /// Sink 是 null 時丟掉。宿主（mod）只在除錯模式寫、帶自己的前綴；離線測試可以印到主控台或收集起來檢查。
    /// 不用 Console.SetOut：那是整個程序共用的，會吃掉別的 mod 的輸出。
    /// 引擎只在主執行緒（宿主每幀呼叫 Doom.Update 的地方）印，不用鎖。
    /// </summary>
    public static class EngineLog
    {
        private static readonly StringBuilder line = new StringBuilder();

        /// <summary>收整行訊息的地方（不含換行）；null＝丟掉。它丟出的例外會被吞掉，不影響引擎。</summary>
        public static Action<string> Sink;

        /// <summary>同 Console.Write：先存著，等 WriteLine 湊成一整行。</summary>
        public static void Write(string value)
        {
            if (Sink == null)
            {
                line.Clear();
                return;
            }
            line.Append(value);
        }

        /// <summary>同 Console.WriteLine：前面 Write 的半行接上 value，交給 Sink。</summary>
        public static void WriteLine(string value)
        {
            var sink = Sink;
            if (sink == null)
            {
                line.Clear();
                return;
            }
            line.Append(value);
            var text = line.ToString();
            line.Clear();
            try
            {
                sink(text);
            }
            catch
            {
                // 記 log 失敗不能讓引擎跟著失敗（例如讀 WAD 的 try 會把它當成讀檔失敗）
            }
        }
    }
}
