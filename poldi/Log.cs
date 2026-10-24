
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

// Marcel Timm, RhinoDevel, 2024sep30

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace Poldi
{
    /// <summary>
    /// Logs to file and optionally also to console. Thread-safe.
    /// </summary>
    internal static class Log
    {
        private static bool _logToConsoleToo = false;

        private static readonly string _absFilePath =
            Path.Combine(AppContext.BaseDirectory, "mt_log.txt");

        private static readonly object _lockObj = new();

        private static string GetCallerInfo(
            string? path, int lineNr, string? name)
        {
            var retVal = string.Empty;

            if (path != null)
            {
                retVal += Path.GetFileName(path);
            }

            if (0 <= lineNr)
            {
                if (0 < retVal.Length)
                {
                    retVal += " ";
                }
                retVal += $"({lineNr})";
            }

            if (name != null)
            {
                if (0 < retVal.Length)
                {
                    retVal += " / ";
                }
                retVal += name;
            }

            return retVal;
        }

        /// <remarks>
        /// Thread-safe.
        /// </remarks>
        internal static void Line(
            string text,
            [CallerFilePath] string? callerPath = null,
            [CallerLineNumber] int callerLineNr = -1,
            [CallerMemberName] string? callerName = null)
        {
            Debug.Assert(text.Trim().Length != 0);

            var line = GetCallerInfo(callerPath, callerLineNr, callerName);

            if(line.Length != 0)
            {
                line += ": "; // There is at least some (preceding) caller info.
            }

            line = $"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ffffzzz")}] {line}{text}";

            lock (_lockObj)
            {
                if (_logToConsoleToo)
                {
                    Console.WriteLine(line);
                }

                File.AppendAllText(
                    _absFilePath, $"{line}{Environment.NewLine}");
            }
        }

        /// <remarks>
        /// Thread-safe.
        /// </remarks>
        internal static void SetLogToConsole(bool isEnabled)
        {
            lock (_lockObj)
            {
                _logToConsoleToo = isEnabled;
            }
        }
    }
}
