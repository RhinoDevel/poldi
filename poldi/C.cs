
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

// Marcel Timm, RhinoDevel, 2024sep18

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Tomlyn;
using Tomlyn.Model;

namespace Poldi
{
    /// <summary>
    /// Singleton to get read access to the application's configuration file.
    /// </summary>
    internal static class C
    {
        private static readonly string _configPath =
            Path.Combine(AppContext.BaseDirectory, "mt_config.toml");

        /// <summary>
        /// Defines the language to use.
        /// </summary>
        private const string _globalLangPath = "global:lang";

        private const string _langPathMiddle = "_lang_";

        private static readonly string _lang;
        private static readonly string _langPathPostfix;
        private static readonly TomlTable _d;

        static C()
        {
            _d = Load();
            _lang = GetVal<string>(_globalLangPath);
            _langPathPostfix = _langPathMiddle + _lang;
        }

        private static TomlTable Load()
        {
            using var input = File.OpenRead(_configPath);

            return TomlSerializer.Deserialize<TomlTable>(input)!;
        }

        /// <remarks>
        /// Still throws an exceptoon, if the found value is of another type
        /// than the wanted type and cannot be converted.
        /// </remarks>
        private static bool TryGetVal<T>(string path, out T val)
        {
            object cur = _d;

            foreach (var part in path.Split(':'))
            {
                if (cur is not TomlTable table)
                {
                    // Current part canot be accessed, because the path does
                    // not point to a TOML table.
                    val = default!;
                    return false;
                }
                if (!table.TryGetValue(part, out cur!))
                {
                    // The configuration key was not found.
                    val = default!;
                    return false;
                }
                if (cur is T value)
                {
                    // Value is of wanted type.
                    val = (T)cur;
                    return true;
                }
            }

            // Value was found, but is not of wanted type.

            try
            {
                val = (T)Convert.ChangeType(cur, typeof(T));
                return true;
            }
            catch (Exception e)
            {
                throw new InvalidCastException(
                    $"Configuration key \"{path}\" contains a \"{cur.GetType().Name}\", which cannot be converted to \"{typeof(T).Name}\"!",
                    e);
            }
        }
        //
        private static T GetVal<T>(string path)
        {
            object cur = _d;

            foreach (var part in path.Split(':'))
            {
                if (cur is not TomlTable table)
                {
                    throw new InvalidOperationException($"'{part}' cannot be accessed, because \"{path}\" does not point to a TOML table!");
                }

                if (!table.TryGetValue(part, out cur!))
                {
                    throw new KeyNotFoundException($"Configuration key \"{path}\" was not found!");
                }

                if (cur is T value)
                {
                    return value;
                }
            }

            // Value was found, but is not of wanted type.

            try
            {
                return (T)Convert.ChangeType(cur, typeof(T));
            }
            catch (Exception e)
            {
                throw new InvalidCastException(
                    $"Configuration key \"{path}\" contains a \"{cur.GetType().Name}\", which cannot be converted to \"{typeof(T).Name}\"!",
                    e);
            }
        }

        internal static T D<T>(string path)
        {
            if (path == _globalLangPath)
            {
                Debug.Assert(false); // Should not get here.
                return GetVal<T>(path);
            }

            {
                // Try to get wanted value as language-specific, first.

                T buf;

                if (TryGetVal(path + _langPathPostfix, out buf))
                {
                    return buf;
                }
            }

            // Setting seems to be language-independent.

            return GetVal<T>(path);
        }
    }
}
