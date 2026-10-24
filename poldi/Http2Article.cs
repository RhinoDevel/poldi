
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

// Marcel Timm, RhinoDevel, 2026sep29

using AngleSharp;
using AngleSharp.Js;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Poldi
{
    // Tested:
    // - https://github.com/RhinoDevel/cbmtapepi
    // - https://www.tagesschau.de/wirtschaft/technologie/space-x-raketenstart-100.html
    // - https://www.bbc.com/travel/article/20260924-the-1000-year-old-story-behind-bluetooth
    // - https://www.zenrows.com/blog/anglesharp
    // - https://www.nachdenkseiten.de/?p=156731
    // - https://www.einhell-service.com/de_DE/3413246-ge-cm-36-43-li-m-solo.html
    // - https://jorenar.com/c-esoterica
    // - https://www.espruino.com/ReferenceBANGLEJS2
    // - https://forum.classic-computing.de/forum/index.php?thread/35710-pet-9-video-theorie-und-reparatur/&postID=634038#post634038
    // - https://tamiyabase.com/forum/33-vintage-tamiya-original-build-or-restoration/19081-lunchbox-rear-axle-springs-are-they-right
    // - https://www.dwd.de/DE/leistungen/klimadatendeutschland/mittelwerte/sonne_8110_fest_html.html?view=nasPublication
    // - http://www.mainbyte.com/ts1000/
    // - https://pi-hole.net/

    internal static class Http2Article
    {
        /// <remarks>
        /// Original source:
        /// 
        /// https://github.com/mozilla/readability/blob/d7949dc47dd9ed9ee1d3b34ffdcf3bce28cde435/Readability.js
        /// 
        /// Note: The original source was patched, search for "RhinoDevel" in
        ///       the JavaScript file.
        /// </remarks>
        private static readonly string _pathReadabilityJs = Path.Combine(
            AppContext.BaseDirectory, "Readability.js");

        private static readonly string _readabilityJs = File.ReadAllText(
            _pathReadabilityJs);

        /// <summary>
        /// Loads HTML (assuming, it is HTML..) from given URL and tries to
        /// extract the text-only content for the "main article" found in that
        /// HTML.
        /// </summary>
        /// <remarks>
        /// Does not support websites that lazy-load their content.
        /// </remarks>
        internal static string? Get(string url)
        {
            string? html;
            string article;

            Log.Line($"URL: \"{url}\"");

            // *****************************************************************
            // *** Load HTML from given URL:                                 ***
            // *****************************************************************

            html = Http.GetStrAsync(url).Result;
            if (html is null)
            {
                Log.Line($"Error: Failed to load from given URL!");
                return null;
            }

            // *****************************************************************
            // *** Get rid of any scripts (with JavaScript disabled):        ***
            // *****************************************************************

            {
                using var ctx = BrowsingContext.New(); // Default context conf.
                using var doc = ctx
                    .OpenAsync(r => r.Content(html).Address(url))
                    .Result;

                foreach (var script in doc.QuerySelectorAll("script").ToList())
                {
                    script.Remove(); // In-place
                }

                html = "<!DOCTYPE html>" + doc.DocumentElement.OuterHtml;
            }

            // *****************************************************************
            // *** Try to get the "main article" (HTML) via Readability.js:  ***
            // *****************************************************************

            {
                var extractJs = @"
(function() // IIFE
{
    let o = { text: null };

    try
    {
        const res = new Readability(document, { serializer: el => el }).parse();

        o.text = res.content.innerText; // Throws an exception on error.
    }
    catch
    {
        // Nothing to do.
    }
    return JSON.stringify(o);
})();";
                using var ctx = BrowsingContext.New(
                    Configuration.Default.WithCss().WithJs());
                using var doc = ctx
                    .OpenAsync(r => r.Content(html).Address(url))
                    .Result;
                var json = doc.ExecuteScript(
                        _readabilityJs + "\n;\n" + extractJs)
                            ?.ToString();
                using var result = JsonDocument.Parse(json!);
                var text = result.RootElement.GetProperty("text").GetString();

                if (text == null)
                {
                    Log.Line("Error: Failed to extract article!");
                    return null;
                }
                article = text;
            }
            return article;
        }
    }
}
