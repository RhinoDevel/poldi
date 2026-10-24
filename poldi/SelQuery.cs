
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

// Marcel Timm, RhinoDevel, 2025nov13

using System;
using System.Diagnostics;

namespace Poldi
{
    /// <summary>
    /// To be used by <see cref="Sel"/> to create a classifier prompt.
    /// </summary>
    internal static class SelQuery
    {
        /// <remarks>
        /// As long as the numbers used in prompt do not start with zero, but
        /// one, the maximum supported number of options is nine (and not ten).
        /// </remarks>
        public static int MaxOptionsLen = 9;

        /// <remarks>
        /// This is hard-codedly set to match a specific system prompt, e.g.:
        /// "You are given a question.\nAll possible answers are given as options, each has a number.\nFind the correct answer.\nRespond by writing the answer's number, only (no prefix)!"
        /// </remarks>
        internal static string GetSelPrompt(
            string question, string[] options, string? explanation)
        {
            Debug.Assert(2 <= options.Length);
            Debug.Assert(options.Length <= MaxOptionsLen);

            var retVal = string.Empty;

            if(explanation != null)
            {
                Debug.Assert(0 < explanation.Trim().Length);

                retVal += "[EXPLANATION]" + Environment.NewLine;
                retVal += Environment.NewLine;
                retVal += explanation;
                retVal += Environment.NewLine;
                retVal += Environment.NewLine;
            }

            retVal += "[QUESTION]" + Environment.NewLine;
            retVal += Environment.NewLine;
            retVal += question + Environment.NewLine;
            retVal += Environment.NewLine;
            retVal += "[OPTIONS]" + Environment.NewLine;
            for (int i = 0; i < MaxOptionsLen && i < options.Length; ++i)
            {
                retVal += Environment.NewLine;
                retVal += $"{i + 1}: {options[i]}";
            }

            return retVal;
        }
    }
}
