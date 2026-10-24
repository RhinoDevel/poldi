
//    P.O.L.D.I. - Private Offline Local Digital Intelligence
//    Copyright (C) 2026  Marcel Timm, RhinoDevel
//
//    This program is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with this program.  If not, see <https://www.gnu.org/licenses/>.

// Marcel Timm, RhinoDevel, 2026mar29

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Poldi
{
    /// <summary>
    /// Singleton to offer helper methods for some string-related operations.
    /// </summary>
    internal static class Str
    {
        /// <remarks>
        /// Assumes that the paragraphs are ALL separated by exactly two newline
        /// characters.
        /// 
        /// Also removes leading and/or trailing whitespace characters from the
        /// paragraphs.
        /// </remarks>
        internal static List<string> GetParagraphs(string text)
        {
            return text.Split("\n\n")
                .Select(p => p.Trim())
                .ToList();
        }

        /// <remarks>
        /// Given <paramref name="trimmedUnixText"/> must use '\n' for line
        /// breaks, only and must not start or end with any whitespace
        /// characters!
        /// Run <see cref="GetTrimmedWithUnixNewlines"/> before calling this
        /// method.
        /// </remarks>
        internal static string GetWithoutLonelyTitles(string trimmedUnixText)
        {
            StringBuilder sb;
            string[]? lines;

            lines = trimmedUnixText.Split('\n'); // Gets the lines.
            sb = new StringBuilder(); // Used for performance (instead of +=).

            for (int i = 0; i < lines.Length; ++i)
            {
                Debug.Assert(lines[i] != null); // Must never be null, here.

                if (i == 0 && lines.Length == 1)
                {
                    continue;
                }
                if (i + 2 < lines.Length
                    && 0 <= i - 2
                    && lines[i - 2].Length == 0
                    && lines[i - 1].Length == 0
                    && lines[i + 1].Length == 0
                    && lines[i + 2].Length == 0)
                {
                    // NOT adding current line (is lonely title).
                    i += 2; // Skips next two (empty) lines.
                    continue;
                }
                sb.Append(lines[i]);
                if (i + 1 != lines.Length)
                {
                    sb.Append('\n');
                }
            }
            return sb.ToString();
        }

        /// <remarks>
        /// Run this after <see cref="GetWithoutLonelyTitles"/>.
        /// </remarks>
        internal static string GetWithTitlesDirectlyAboveParagraphs(
            string trimmedUnixText)
        {
            StringBuilder sb;
            string[]? lines;

            lines = trimmedUnixText.Split('\n'); // Gets the lines.
            sb = new StringBuilder(); // Used for performance (instead of +=).

            for (int i = 0; i < lines.Length; ++i)
            {
                Debug.Assert(lines[i] != null); // Must never be null, here.

                if (lines[i].Length == 0)
                {
                    // Empty line. Just append.
                    sb.Append('\n');
                    continue;
                }

                if (i + 2 < lines.Length
                    && lines[i + 1].Length == 0
                    && lines[i + 2].Length != 0)
                {
                    if (i == 0
                            || (2 <= i
                                    && lines[i - 1].Length == 0
                                    && lines[i - 2].Length == 0))
                    {
                        sb.Append(lines[i]);
                        sb.Append('\n');
                        ++i; // Skips next empty line.
                        continue;
                    }
                }
                sb.Append(lines[i]);
                if (i + 1 != lines.Length)
                {
                    sb.Append('\n');
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Replaces all "Windows" (CR LF) and "Macintosh" (CR) newlines with
        /// "Unix" newlines (LF) and remove leading and trailing whitespace
        /// characters.
        /// </summary>
        internal static string GetTrimmedWithUnixNewlines(string text)
        {
            return text
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Trim();
        }

        internal static int GetWordCount(string text)
        {
            string[] words;

            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }

            words = text.Split(
                [' ', '\t', '\n', '\r'],
                StringSplitOptions.RemoveEmptyEntries);

            return words.Length;
        }
    }
}
