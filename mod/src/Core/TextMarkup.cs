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

using System.Text;

namespace ProbablyDoom
{
    /// <summary>
    /// 沒有 WAD 與出錯畫面的字要在哪裡換行（需求.md 決定 29），做成 TextMeshPro 的標記。不碰 Unity，離線測試測同一份。
    ///
    /// TMP 的中文每個字後面都能換行、英文在空白後面換，所以中文句子會在「DOOM：」「詳情」中間被拆開，
    /// 路徑會在「Questing Goose Studio」的空白被拆開（看不出中間有空白）。做法：
    /// - 不能拆的一段用 &lt;nobr&gt;…&lt;/nobr&gt; 包起來（遊戲的 Unity.TextMeshPro.dll 認得這個標籤：
    ///   TMP_Text.ValidateHtmlTag 的標籤雜湊 43969／31169 設 m_isNonBreakingSpace、156816／144016 清掉，2026-11-12 反編譯確認）。
    /// - 段與段之間放一個零寬空白（U+200B）。TMP 一行裡還沒遇到能換行的地方時，每個字都當成可以換行（一個字比一行長時的退路，
    ///   TextMeshProUGUI.GenerateTextMesh 存換行點的那段），&lt;nobr&gt; 裡的字也一樣；整行都是 &lt;nobr&gt; 的話等於沒包。
    ///   零寬空白在 &lt;nobr&gt; 外面，TMP 把它當成空白（可以在它後面換行），有了它之後同一行的 &lt;nobr&gt; 才真的不拆。
    ///   它不佔寬度，也不會變成缺字的方框：TMP_FontAsset.AddSynthesizedCharactersAndFaceMetrics 替每個字型補一個 U+200B 的空字。
    /// 只有每一段本身比一行還長時才會在段裡面拆（版面 MessageLayout 照基準寬排，一段放得下一行）。
    /// </summary>
    internal static class TextMarkup
    {
        internal const string NoBreakOpen = "<nobr>", NoBreakClose = "</nobr>";
        /// <summary>零寬空白：段與段之間可以換行的地方。</summary>
        internal const char BreakHere = '​';

        /// <summary>中文的逗號：說明文字只在它後面換行（逗號留在前一段的結尾，不會跑到下一行的開頭）。</summary>
        const char Comma = '，';

        /// <summary>
        /// 說明文字（每一行是一句，行之間是 '\n'）。中文（cjk）：每一行在「，」後面切段，每段用 &lt;nobr&gt; 包起來、段之間放零寬空白，
        /// 只在「，」後面能換行；英文照原樣（TMP 在空白換行就好）。字串裡不能有 &lt; &gt;（我們自己的定稿字，沒有）。
        /// </summary>
        internal static string Sentences(string text, bool cjk)
        {
            if (string.IsNullOrEmpty(text) || !cjk) return text ?? "";
            var sb = new StringBuilder(text.Length + 32);
            var lines = text.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                if (i > 0) sb.Append('\n');
                AppendSegments(sb, lines[i], Comma);
            }
            return sb.ToString();
        }

        /// <summary>
        /// 路徑框裡的路徑：只在「\」後面換行，每段連同後面的「\」包一個 &lt;nobr&gt;（「Questing Goose Studio」這種中間有空白的不拆）。
        /// 路徑不會有 &lt; &gt;（Windows 的檔名不允許），不用另外跳脫；路徑的 TMP 要關 parseCtrlCharacters（字面的 \n、\u… 才不會被轉掉）。
        /// </summary>
        internal static string Path(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            var sb = new StringBuilder(path.Length + 64);
            AppendSegments(sb, path, '\\');
            return sb.ToString();
        }

        /// <summary>拿掉標記（&lt;nobr&gt;、&lt;/nobr&gt;、零寬空白），回到原本的字（離線測試核對沒有漏字）。</summary>
        internal static string Plain(string markup)
        {
            if (string.IsNullOrEmpty(markup)) return "";
            return markup.Replace(NoBreakOpen, "").Replace(NoBreakClose, "").Replace(BreakHere.ToString(), "");
        }

        /// <summary>一行字在每個 sep 後面切段（sep 留在前一段的結尾）：每段包 &lt;nobr&gt;，段之間放零寬空白。</summary>
        static void AppendSegments(StringBuilder sb, string line, char sep)
        {
            var start = 0;
            var first = true;
            while (start < line.Length)
            {
                var cut = line.IndexOf(sep, start);
                var end = cut < 0 ? line.Length : cut + 1;
                if (!first) sb.Append(BreakHere);
                sb.Append(NoBreakOpen).Append(line, start, end - start).Append(NoBreakClose);
                first = false;
                start = end;
            }
        }
    }
}
