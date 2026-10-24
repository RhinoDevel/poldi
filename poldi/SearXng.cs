
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

// Marcel Timm, RhinoDevel, 2026mar15

using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace Poldi
{
    /// <summary>
    /// Singleton to offer methods to retrieve data from a SearXNG instance.
    /// </summary>
    internal static class SearXng
    {
        private static readonly string _baseUrl =
            C.D<string>("www:searXngBase");

        private const string _postfixSearch = "search?format=json&q=";

        /// <remarks>
        /// Returned tuples are ordered by search score (best match first).
        /// </remarks>
        internal static async Task<List<Tuple<string, string>>?>
            GetUrlAndTitles(string query)
        {
            List<Tuple<string, string>>? l;
            string searchUrl;
            string? jsonSearch;

            if (query.Trim().Length == 0)
            {
                Log.Line("Error: Whitespace-only or empty query given!");
                return null;
            }

            // *****************************************************************
            // *** Search for websites via given query:                      ***
            // *****************************************************************

            searchUrl =
                $"{_baseUrl}{_postfixSearch}{WebUtility.UrlEncode(query)}";
            //Log.Line($"Search URL: \"{searchUrl}\"");

            jsonSearch = await Http.GetStrAsync(searchUrl);
            if (jsonSearch == null)
            {
                return null; // (called method logs on error)
            }

            // *****************************************************************
            // *** Extract results:                                          ***
            // *****************************************************************

            try
            {
                var results = JsonDocument.Parse(jsonSearch!)
                    .RootElement
                    .GetProperty("results");

                l = new();
                for(int i = 0; i < results.GetArrayLength(); ++i)
                {
                    l.Add(
                        new(
                            results[i].GetProperty("url").GetString()!,
                            results[i].GetProperty("title").GetString()!));
                }
            }
            catch (Exception e)
            {
                Log.Line($"Error: Results extraction from JSON failed with exception (msg.: \"{e.Message}\"!");
                return null;
            }

            return l;
        }
    }
}
