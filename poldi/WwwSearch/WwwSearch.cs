
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

// Marcel Timm, RhinoDevel, 2026sep30

using Poldi.Search;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Poldi.WwwSearch
{
    internal static class WwwSearch
    {
        private static readonly int _maxSearchResults =
            C.D<int>("www:maxSearchResults");

        internal static async Task<string?> GetResponse(
            string topic,
            string query,
            string? begOfAnswer,
            float[] embVec,
            WwwSearchParams p)
        {
            List<Tuple<string, string>>? urlAndTitles;

            // Use SearXNG interface to retrieve results.

            urlAndTitles = await SearXng.GetUrlAndTitles(topic);
            if (urlAndTitles is null)
            {
                return null; // (called method logs on error)
            }

            if (urlAndTitles.Count == 0)
            {
                Log.Line($"No results retrieved for topic \"{topic}\".");
                return null;
            }

            if (_maxSearchResults < urlAndTitles.Count)
            {
                urlAndTitles = urlAndTitles.Take(_maxSearchResults).ToList();
            }

            // For each of the top results.
            foreach (var urlAndTitle in urlAndTitles)
            {
                string? article;
                string preparedArticle;
                List<string> paragraphs;
                List<string> chunks;
                int maxI;
                List<int> indices;
                string prompt;
                string response;

                // Get article via Http2Article.
                article = Http2Article.Get(urlAndTitle.Item1);
                if (article is null)
                {
                    continue; // (called method logged on error)
                }

                // Prepare article.
                preparedArticle = Str.GetTrimmedWithUnixNewlines(article);
                preparedArticle = Str.GetWithoutLonelyTitles(preparedArticle);
                preparedArticle = Str.GetWithTitlesDirectlyAboveParagraphs(
                    preparedArticle);

                // Also trims the paragraphs.
                paragraphs = Str.GetParagraphs(preparedArticle);

                // Create chunks from paragraphs.
                chunks = SearchHelper.GetChunks(
                    paragraphs,
                    urlAndTitle.Item2,
                    p.MinChunkWordCount,
                    p.EmbContextLength,
                    [],
                    p.GetTokenCount!);
                if(chunks.Count == 0)
                {
                    Log.Line($"No chunks.");
                    return null;
                }

                if (p.UseReranker)
                {
                    var ordMostSimIndices = Sim.GetOrdMostSimIndices(
                            begOfAnswer ?? query,
                            chunks
                                .Select(
                                    chunk =>
                                        new SimDoc(
                                                chunk,
                                                p.CreateEmbeddings!(chunk)!))
                                .ToList(),
                            p.ChunkCountToInject)!;

                    maxI = ordMostSimIndices.Count - 1;

                    indices = ordMostSimIndices;
                }
                else
                {
                    var embLimitedParagraphs = chunks
                            .Select(p.CreateEmbeddings!)
                            .ToList();
                    var descDotProdIndices = Calc.GetDescDotProdIndices(
                            embVec, embLimitedParagraphs!);

                    maxI = Math.Min(
                        p.ChunkCountToInject, chunks.Count) - 1;

                    indices = descDotProdIndices.Select(i => i.Item1).ToList();
                }

                prompt = $"### UNSORTED SOURCES FROM WEBSITE WITH TITLE \"{urlAndTitle.Item2}\" ###";
                for (int i = maxI; 0 <= i; --i)
                {
                    var chunk = chunks[indices[i]];

                    prompt += Environment.NewLine;
                    prompt += Environment.NewLine;
                    prompt += $"- Source {maxI - i + 1}:";
                    prompt += Environment.NewLine;
                    prompt += Environment.NewLine;
                    prompt += chunk;
                }
                prompt += Environment.NewLine;
                prompt += Environment.NewLine;
                prompt += string.Format(
                    SearchHelper.PromptTemplate_getAnswerPostfix,
                    SearchHelper.NotFoundResponse,
                    DateTime.Now.ToString("D", CultureInfo.InvariantCulture),
                    query);

                response = p.Infer!(SearchHelper.SysPrompt, prompt)!;

                response = response.Trim();

                if (response.ToLower().Contains(
                        SearchHelper.NotFoundResponse.ToLower()))
                {
                    continue; // Try next top candidate (if there is one).
                }

                // Smaller LLMs sometimes return with an "answer" like "The answer
                // to the question could not be found in the provided sources."
                // anyway, so we check via classification.
                if (!SearchHelper.IsRealAnswer(response))
                {
                    continue;
                }

                Debug.Assert(!string.IsNullOrWhiteSpace(response));
                return response;
            }
            return null; // Not found.
        }

        /// <returns>
        /// null on error OR, if the answer could not be found.
        /// </returns>
        internal static async Task<SearchResult?> Exec(
            WwwSearchParams p, string query)
        {
            Stopwatch stopwatch;
            int embContextLengthToUse;
            string? begOfAnswer;
            float[]? embVec;
            string? response;

            stopwatch = Stopwatch.StartNew();

            // Because we are using the LLM (not the embedding model) to check,
            // how many tokens are there per paragraph.
            embContextLengthToUse =
                (int)(4.0 * (double)p.EmbContextLength / 5.0); // Tokens

            if (embContextLengthToUse <= 0)
            {
                Debug.Assert(false);
                return null;
            }

            begOfAnswer = null;
            if(query.EndsWith("?"))
            {
                // The query is an actual question.

                begOfAnswer = p.Infer!(
                    SearchHelper.SysPrompt,
                    string.Format(
                        SearchHelper.PromptTemplate_getBeginningOfAnswerFromQuestion,
                        query));
                if (string.IsNullOrWhiteSpace(begOfAnswer))
                {
                    Debug.Assert(false); // Should not happen.
                    Log.Line($"Failed to get beginning-of-answer from given question \"{query}\"!");
                    return null;
                }
                begOfAnswer = begOfAnswer.Trim();
            }

            embVec = p.CreateEmbeddings!(begOfAnswer ?? query);
            if (embVec == null)
            {
                return null;
            }

            response = await GetResponse(query, query, begOfAnswer, embVec, p);
            begOfAnswer = null;
            embVec = null;

            if (response is null)
            {
                // Extract topic from given query and use it with search engine
                // and do the same, as above.

                var topic = SearchHelper.GetSingleTopic(query, p.Infer!);

                if (topic is null)
                {
                    Log.Line($"No topic found in query \"{query}\"!");
                    return null;
                }

                Debug.Assert(0 < topic.Trim().Length);

                response = await GetResponse(
                    topic, query, null, p.CreateEmbeddings(topic)!, p);
            }

            if (response is null)
            {
                return null;
            }

            stopwatch.Stop();
            return new SearchResult
            {
                Answer = response,
                Attempts = -1, // Not counted, here..
                Seconds = stopwatch.Elapsed.TotalSeconds // How long it took.
            };
        }
    }
}
