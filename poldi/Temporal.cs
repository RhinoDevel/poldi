
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
using System.Diagnostics;
using System.Globalization;

namespace Poldi
{
    /// <summary>
    /// Singleton offering date/time related helper methods.
    /// </summary>
    internal static class Temporal
    {
        internal static string GetDaysDesc(DateTime a, DateTime b)
        {
            Debug.Assert(a < b);

            var ts = b.Date - a.Date;

            if (ts.Days == 0)
            {
                return "not a day ago";
            }
            return $"{ts.Days} days ago";
        }

        /// <summary>
        /// Get string representation of given date, e.g. "2026-04-16".
        /// </summary>
        internal static string GetDate(DateTime d)
        {
            return d.ToString("yyyy-MM-dd");
        }

        /// <summary>
        /// Get string representation of given date and time, minute precision,
        /// e.g. "2026-04-16 23:59".
        /// </summary>
        internal static string GetDateHourMin(DateTime d)
        {
            return d.ToString("yyyy-MM-dd HH:mm");
        }

        /// <summary>
        /// Get string representation of given date and time, minute precision,
        /// including the weekday, e.g. "Thursday, 2026-04-16 23:59".
        /// </summary>
        internal static string GetWeekdayDateHourMin(DateTime d)
        {
            return d.ToString(
                "dddd, yyyy-MM-dd HH:mm", new CultureInfo("en-US"));
        }
    }
}
