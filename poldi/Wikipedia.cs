
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
    /// Singleton to offer methods to retrieve data from Wikipedia online.
    /// </summary>
    internal static class Wikipedia
    {
        /// <remarks>
        /// Documentation: https://en.wikipedia.org/w/api.php?action=help&modules=query%2Bsearch
        /// 
        /// srwhat=title is disabled at Wikipedia (2026mar15).
        /// 
        /// srlimit= can be used to set the count of returned search results
        /// (default is 10).
        /// </remarks>
        private const string _httpPrefix_searchPageId
            = "https://en.wikipedia.org/w/api.php?action=query&list=search&format=json&srsearch=";

        /// <remarks>
        /// Documentation: https://en.wikipedia.org/w/api.php?action=help&modules=query%2Bextracts
        /// 
        /// pageprops holds disambiguation property, if it is a disambiguation
        /// entry.
        /// </remarks>
        private const string _httpPrefix_getArticle
            = "https://en.wikipedia.org/w/api.php?action=query&prop=extracts|pageprops&explaintext&exsectionformat=plain&format=json&pageids=";

        /// <summary>
        /// Find out, if page (ID) is a "disambiguation page".
        /// 
        /// Is there one .properties array entry with the property "name" set to
        /// "disambiguation"?
        /// </summary>
        private const string _httpPrefix_getProperties
            = "https://en.wikipedia.org/w/api.php?action=parse&prop=properties&format=json&pageid=";

        // Get sections for a page (ID):
        // https://en.wikipedia.org/w/api.php?action=parse&prop=sections&pageid=1878645&format=json

        // Get HTML (plain text does not seem to be possible, here) of a
        // specific section (from the sections overview, see above):
        // https://en.wikipedia.org/w/api.php?action=parse&section=2&pageid=1878645&format=json&prop=text

        /// <returns>
        /// null on error. Empty list, if no usable search results where
        /// retrieved.
        /// </returns>
        /// <remarks>
        /// Does not return disambiguation entries/"articles" or entries with
        /// their page IDs included in the given blacklist.
        /// 
        /// There may be more page IDs and titles, but we are using the first
        /// ones returned, only.
        /// </remarks>
        internal static async Task<List<Tuple<int, string>>?>
            GetPageIdAndTitles(string topic, List<int> pageIdBlacklist)
        {
            List<Tuple<int, string>>? l;
            JsonElement searchProp;
            string searchUrl;
            string? jsonSearch;

            if (topic.Trim().Length == 0)
            {
                Log.Line($"Error: Whitespace-only or empty topic given!");
                return null;
            }

            // *****************************************************************
            // *** Search for articles via given topic:                      ***
            // *****************************************************************

            searchUrl = $"{_httpPrefix_searchPageId}{WebUtility.UrlEncode(topic)}";
            //Log.Line($"Search URL: \"{searchUrl}\"");

            jsonSearch = await Http.GetStrAsync(searchUrl);
            if (jsonSearch == null)
            {
                return null; // (called method logs on error)
            }

            // *****************************************************************
            // *** Extract (first) results' titles and page IDs from resp.:  ***
            // *****************************************************************

            // There may be more search results, but we are using the first
            // returned results, only.

            try
            {
                searchProp = JsonDocument.Parse(jsonSearch!)
                    .RootElement
                    .GetProperty("query")
                    .GetProperty("search");
            }
            catch (Exception e)
            {
                Log.Line($"Error: Failed to find property \"query.search\" with exception (msg.: \"{e.Message}\"!");
                return null;
            }

            // => Something like this ("snippet" strings where reduced for
            //    readability of this file):
            //
            // [
            //    {
            //        "ns": 0,
            //        "title": "Elephant",
            //        "pageid": 9279,
            //        "size": 135657,
            //        "wordcount": 14630,
            //        "snippet": "<span class=\"searchmatch\">Elephants</span> are...",
            //        "timestamp": "2026-02-24T18:11:56Z"
            //    },
            //    {
            //        "ns": 0,
            //        "title": "Elephant (disambiguation)",
            //        "pageid": 652549,
            //        "size": 5545,
            //        "wordcount": 686,
            //        "snippet": "Look up <span class=\"searchmatch\">Elephant</span>, <span class=\"searchmatch\">elephant</span>, ...",
            //        "timestamp": "2025-09-26T22:02:41Z"
            //    },
            //    {
            //        "ns": 0,
            //        "title": "Asian elephant",
            //        "pageid": 379035,
            //        "size": 108852,
            //        "wordcount": 11236,
            //        "snippet": "The Asian <span class=\"searchmatch\">elephant</span> (Elephas maximus), also...",
            //        "timestamp": "2026-03-01T03:55:58Z"
            //    }
            // ]

            l = new();
            for (int i = 0; i < searchProp.GetArrayLength(); ++i)
            {
                int pageId;
                string? title;

                // *************************************************************
                // *** Load article:                                         ***
                // *************************************************************

                try
                {
                    pageId = searchProp[i].GetProperty("pageid").GetInt32();
                }
                catch (Exception e)
                {
                    Log.Line($"Error: Failed to find property \"pageid\" with exception (msg.: \"{e.Message}\"!");
                    return null;
                }
                //Log.Line($"Page ID at index {i}: {pageId}");

                try
                {
                    title = searchProp[i].GetProperty("title").GetString();
                }
                catch (Exception e)
                {
                    Log.Line($"Error: Failed to find property \"title\" with exception (msg.: \"{e.Message}\"!");
                    return null;
                }
                if (string.IsNullOrWhiteSpace(title))
                {
                    Log.Line("Error: Retrieved null, whitespace-only or empty string as title!");
                    return null;
                }
                //Log.Line($"Title at index {i}: {title}");

                l.Add(new Tuple<int, string>(pageId, title));
            }

            // *****************************************************************
            // *** Remove disambiguation and blacklisted entries:            ***
            // *****************************************************************

            {
                var orgPageIdAndTitles = l;

                l = new();
                foreach (var pageIdAndTitle in orgPageIdAndTitles)
                {
                    bool? isDisambiguation;

                    if (pageIdBlacklist.Contains(pageIdAndTitle.Item1))
                    {
                        continue; // Skip blacklisted articles.
                    }

                    isDisambiguation = await GetIsDisambiguationEntry(
                        pageIdAndTitle.Item1);
                    if (isDisambiguation == null)
                    {
                        return null; // (called method logs)
                    }
                    if (isDisambiguation == true)
                    {
                        continue; // Skip disambiguation entry.
                    }

                    l.Add(pageIdAndTitle);
                }
            }

            return l;
        }

        private static async Task<bool?> GetIsDisambiguationEntry(int pageId)
        {
            string url;
            string? json;
            JsonElement prop;

            // *****************************************************************
            // *** Load properties:                                          ***
            // *****************************************************************

            url = $"{_httpPrefix_getProperties}{WebUtility.UrlEncode(pageId.ToString())}";
            //Log.Line($"URL: {url}");

            json = await Http.GetStrAsync(url);
            if (json == null)
            {
                return null; // (called method logs on error)
            }

            // => Something like:
            //
            // {
            //    "parse": {
            //        "title": "Justified",
            //        "pageid": 28692294,
            //        "properties": [
            //            {
            //                "name": "wikibase-shortdesc",
            //                "*": "Topics referred to by the same term"
            //            },
            //            {
            //                "name": "disambiguation",
            //                "*": ""
            //            },
            //            {
            //                "name": "wikibase_item",
            //                "*": "Q416968"
            //            }
            //        ]
            //    }
            // }

            // *****************************************************************
            // *** Extract data:                                             ***
            // *****************************************************************

            try
            {
                prop = JsonDocument.Parse(json!)
                    .RootElement
                    .GetProperty("parse")
                    .GetProperty("properties");
            }
            catch (Exception e)
            {
                Log.Line($"Error: Failed to find property \"parse.properties\" with exception (msg.: \"{e.Message}\"!");
                return null;
            }

            // *****************************************************************
            // *** Is this a disambiguation entry?                           ***
            // *****************************************************************

            for (int i = 0; i < prop.GetArrayLength(); ++i)
            {
                string? name;

                try
                {
                    name = prop[i].GetProperty("name").GetString();
                }
                catch (Exception e)
                {
                    Log.Line($"Error: Failed to find property \"name\" with exception (msg.: \"{e.Message}\"!");
                    return null;
                }
                if (string.IsNullOrWhiteSpace(name))
                {
                    Log.Line("Error: Retrieved null, whitespace-only or empty string as name!");
                    return null;
                }

                if(name.Trim().ToLower() == "disambiguation")
                {
                    return true; // Disambiguation "article"/entry.
                }
            }
            return false; // NOT a disambiguation "article"/entry.
        }

        /// <returns>
        /// null on error, empty string, if
        /// <paramref name="skipDisambiguation"/> is true and the "article" is a
        /// disambiguation entry.
        /// </returns>
        internal static async Task<string?> GetArticle(
            int pageId, bool skipDisambiguation)
        {
            string? article;
            string url;
            string? json;
            JsonElement prop;

            // *****************************************************************
            // *** Load article:                                             ***
            // *****************************************************************

            url = $"{_httpPrefix_getArticle}{WebUtility.UrlEncode(pageId.ToString())}";
            //Log.Line($"URL: {url}");

            json = await Http.GetStrAsync(url);
            if (json == null)
            {
                return null; // (called method logs on error)
            }

            // => Something like this (I reduced the "extract" string!):
            //
            // {
            //    "batchcomplete": "",
            //    "query": {
            //        "pages": {
            //            "9279": {
            //                "pageid": 9279,
            //                "ns": 0,
            //                "title": "Elephant",
            //                "extract": "Elephants are the largest...",
            //                "pageprops": {
            //                    "page_image_free": "African_Bush_Elephant.jpg",
            //                    "wikibase-badge-Q17437796": "",
            //                    "wikibase-shortdesc": "Largest living land animal",
            //                    "wikibase_item": "Q7378"
            //                }
            //            }
            //        }
            //    }
            // }

            // *****************************************************************
            // *** Extract data:                                             ***
            // *****************************************************************

            try
            {
                prop = JsonDocument.Parse(json!)
                    .RootElement
                    .GetProperty("query")
                    .GetProperty("pages")
                    .GetProperty(pageId.ToString());
            }
            catch (Exception e)
            {
                Log.Line($"Error: Failed to find property \"query.pages.<page ID>\" with exception (msg.: \"{e.Message}\"!");
                return null;
            }

            if (skipDisambiguation)
            {
                // *************************************************************
                // *** Skip, if this is a disambiguation entry:              ***
                // *************************************************************

                try
                {
                    if (prop
                            .GetProperty("pageprops")
                            .TryGetProperty("disambiguation", out _))
                    {
                        //Log.Line($"Found disambiguation entry, skipping..");
                        return string.Empty; // Do NOT return null, here!
                    }
                }
                catch (Exception e)
                {
                    Log.Line($"Error: Failed to search for property \"pageprobs.disambiguation\" with exception (msg.: \"{e.Message}\"!");
                    return null;
                }
            }

            // *****************************************************************
            // *** Extract the actual article:                               ***
            // *****************************************************************

            try
            {
                article = prop.GetProperty("extract").GetString();
            }
            catch (Exception e)
            {
                Log.Line($"Error: Failed to find property \"extract\" with exception (msg.: \"{e.Message}\"!");
                return null;
            }
            if(string.IsNullOrWhiteSpace(article))
            {
                return null;
            }
            return article;
        }

        internal static async Task<string?> GetFirstNonDisambiguationArticle(
            List<Tuple<int, string>> pageIdAndTitles)
        {
            string? article;

            article = null;
            for (int i = 0; i < pageIdAndTitles.Count; ++i)
            {
                var curArticle = await GetArticle(
                        pageIdAndTitles[i].Item1, true);

                if(curArticle == null)
                {
                    return null; // (called method logs on error)
                }

                if(curArticle == string.Empty)
                {
                    continue; // Disambiguation entry detect. Skipping.
                }
                break; // Done
            }
            return article; // Can be null, here.
        }
    }
}
