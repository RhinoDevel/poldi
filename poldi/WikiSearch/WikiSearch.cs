
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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Poldi.Search;

// Some test questions:
//
//"Who played Indy's father in Last Crusade?",
//"What is special about the Kenbak-1's memory?",
//"Who killed JFK?",
//"What is the capital of Germany?",
//"Is it possible for a diabetic to eat sugar?",
//"Who is the arch enemy of He-Man?",
//"Can penguins swim?",
//"How long is the equator?",
//"Who was Blink-182's first drummer?",
//"Who was the first singer in AC/DC?",
//"Is a hare the same as a rabbit?",
//"When did Jimmy Hendrix die?",
//"What is a Beluga whale's diet?",
//"How old is Kevin Costner?",
//"How many years ago did the Titanic sink?",
//"Which company built the first smartphone?",
//"Did dinosaurs have feathers?",
//"What is the real name of H.P.Baxxter?",
//"Who is the current world chess champion?",
//"How much RAM has the Atari 2600?",
//"Who played Raylan Givens in Justified?",
//"What is a druid?",
//"What is a droid?",
//"What is the most famous book from Isaac Asimov?",
//"Who invented JavaScript?",
//"Who was Albert King?",
//"What did B.B. King call his guitar?",
//"How large can a Blue whale be?",
//"How do the Navajos call themselves?",
//"Who played the son of Indy in Crystal Skull?",
//"What was the first computer?",
//"Which whale is the smallest?",
//"What is Leslie Nielson most famous for?",
//"What is a White whale's diet?",
//"Is Richard Chamberlain still alive?",
//"Where does Dua Lipa come from?",
//"Is coffee bad for my health?",
//"Does tea include caffeine?",
//"Who created Mia and Me?",
//"What is a Xircom Rex 6000?",
//"How many eggs can a Platypus lay?",
//"Which countries still hunt whales?",
//"Which country is the current European football champion?",
//
//"Can the sun shine while it is raining?", // <- Difficult
//"Who plays the main antagonist in Justified?", // <- Difficult

namespace Poldi.WikiSearch
{
    internal class WikiSearch
    {
        // *********************************************************************
        // *** Initialized via static constructor:                           ***
        // *********************************************************************
        //
        private static readonly string _promptTemplate_getSecondTopic;
        private static readonly string[] _paragraph_skipStartsWith;

        static WikiSearch()
        {
            string relPath = AppContext.BaseDirectory
                + "WikiSearch" // <- Hard-coded!
                + Path.DirectorySeparatorChar.ToString();

            _promptTemplate_getSecondTopic = File.ReadAllText(
                relPath + "promptTemplate_getSecondTopic.txt");
            _paragraph_skipStartsWith = File.ReadAllText(
                relPath + "paragraph_skipStartsWith.txt")
                    .Split(Environment.NewLine);
        }
        
        private static string? GetSecondTopic(
            string question,
            string firstTopic,
            Func<string, string, string?> infer)
        {
            Debug.Assert(0 < question.Trim().Length);
            Debug.Assert(0 < firstTopic.Trim().Length);

            var secondTopic = infer(
                    SearchHelper.SysPrompt,
                    string.Format(
                        _promptTemplate_getSecondTopic,
                        question,
                        firstTopic,
                        SearchHelper.NotFoundResponse));
            string lowerSecondTopic;

            if(string.IsNullOrWhiteSpace(secondTopic))
            {
                // Whitespace-only or empty should not happen, here..
                Debug.Assert(secondTopic == null);

                return null;
            }

            secondTopic = secondTopic.Trim();

            lowerSecondTopic = secondTopic.ToLower();

            if (lowerSecondTopic.Contains(SearchHelper.NotFoundResponse.ToLower()))
            {
                return null; // No second topic given by LLM response.
            }

            if (lowerSecondTopic == firstTopic.ToLower())
            {
                return null; // Same as given 1st topic, happens sometimes.
            }

            return secondTopic;
        }

        /// <summary>
        /// Search at Wikipedia for articles, via the given topic, select the
        /// best matching non-disambiguation result and return the list and the
        /// index of the best match into that list.
        /// </summary>
        /// <returns>
        /// null, if no non-disambiguation article found or an error occurred.
        /// </returns>
        private static async Task<Tuple<int, List<Tuple<int, string>>>?>
            GetArticleIndexAndPageIdAndTitles(
                string topic,
                string question,
                WikiSearchParams p,
                bool useFirstNonBlacklistedSkipLlm,
                List<int> pageIdBlacklist)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(topic));

            List<Tuple<int, string>>? pageIdAndTitles;
            int articleIndex;

            // Alternative to using disambiguation removal and LLM selection:
            // 
            //article = await Wikipedia.GetFirstNonDisambiguationArticle(
            //    pageIdAndTitles);

            // *****************************************************************
            // *** Search for pages (articles) via given topic (method also  ***
            // *** ignores disambiguation and blacklisted entries):          ***
            // *****************************************************************

            pageIdAndTitles = await Wikipedia.GetPageIdAndTitles(
                topic, pageIdBlacklist);
            if(pageIdAndTitles == null)
            {
                return null; // (called method logs on error)
            }

            if(pageIdAndTitles.Count == 0)
            {
                Log.Line("No usable articles found."); // Not an error.
                return null;
            }

            if(SelQuery.MaxOptionsLen < pageIdAndTitles.Count)
            {
                pageIdAndTitles = pageIdAndTitles.Take(
                    SelQuery.MaxOptionsLen).ToList();
            }

            articleIndex = 0;
            if (!useFirstNonBlacklistedSkipLlm
                    && 1 < pageIdAndTitles.Count) // Shortcut
            {
                // *************************************************************
                // *** Let the LLM select, which article to use:             ***
                // *************************************************************

                var selQuestion = // TODO: Put in text file!
                        $"Articles with the following titles are available; based on the titles, which article has the highest probability to hold an answer to the question \"{question}\"?";

                articleIndex = p.GetSelectIndex!(
                        string.Format(selQuestion, question),
                        pageIdAndTitles
                            .Select(pageIdAndTitle => pageIdAndTitle.Item2)
                            .ToArray());

                if (articleIndex < 0 || pageIdAndTitles.Count - 1 < articleIndex)
                {
                    Log.Line("Error: Failed to retrieve valid result from LLM selection for article!");
                    return null;
                }
            }
            Log.Line($"Selected article at index {articleIndex}: \"{pageIdAndTitles[articleIndex].Item2}\"");

            return new Tuple<int, List<Tuple<int, string>>>(
                articleIndex, pageIdAndTitles);
        }

        private static async Task<PreparedArticle?> GetPreparedArticle(
            string topic,
            string question,
            WikiSearchParams p,
            bool useFirstNonBlacklistedSkipLlm,
            List<int> pageIdBlacklist)
        {
            string? article;
            string preparedArticle;
            var articleIndexAndPageIdAndTitles =
                    await GetArticleIndexAndPageIdAndTitles(
                        topic,
                        question,
                        p,
                        useFirstNonBlacklistedSkipLlm,
                        pageIdBlacklist);
            List<Tuple<int, string>> pageIdAndTitles;
            int pageId;
            int articleIndex;
            string articleTitle;

            if(articleIndexAndPageIdAndTitles == null)
            {
                return null; // Failed to retrieve usable data from Wikipedia.
            }

            // Stupid. Better use a class for this data:
            pageIdAndTitles = articleIndexAndPageIdAndTitles.Item2;
            articleIndex = articleIndexAndPageIdAndTitles.Item1;
            pageId = pageIdAndTitles[articleIndex].Item1;
            articleTitle = pageIdAndTitles[articleIndex].Item2;

            // *****************************************************************
            // *** Get the article:                                          ***
            // *****************************************************************

            article = await Wikipedia.GetArticle(pageId, false);
            if (string.IsNullOrWhiteSpace(article))
            {
                return null; // (called method logs on error)
            }

            // *****************************************************************
            // *** Prepare the article:                                      ***
            // *****************************************************************

            preparedArticle = Str.GetTrimmedWithUnixNewlines(article);
            preparedArticle = Str.GetWithoutLonelyTitles(preparedArticle);
            preparedArticle = Str.GetWithTitlesDirectlyAboveParagraphs(
                preparedArticle);

            Log.Line($"\"{topic}\" (article title: \"{articleTitle}\") prepared article length: {preparedArticle.Length}");
            Log.Line($"\"{topic}\" (article title: \"{articleTitle}\") prepared article word count: {Str.GetWordCount(preparedArticle)}");
            Log.Line($"\"{topic}\" (article title: \"{articleTitle}\") prepared article token count: {p.GetTokenCount!(preparedArticle, false)}");

            return new PreparedArticle
            {
                PageId = pageId,
                PageIdAndTitles = pageIdAndTitles,
                Text = preparedArticle,
                Title = articleTitle
            };
        }

        /// <returns>
        /// Return values <see cref="Answer.Text"/> is null, if no answer was
        /// found (but the page ID is still valid, then).
        /// </returns>
        private static async Task<Answer?> GetAnswer(
            string question,
            string begOfAnswer,
            string topic,
            float[] begOfAnswerEmbVec,
            WikiSearchParams p,
            bool useFirstNonBlacklistedSkipLlm,
            List<int> pageIdBlacklist)
        {
            string? answer;
            PreparedArticle? preparedArticle;
            string prompt;
            List<string> paragraphs;
            List<string> chunks;
            int embContextLengthToUse;
            List<int> indices;
            int maxI;

            // *****************************************************************
            // *** Retrieve the article:                                     ***
            // *****************************************************************

            preparedArticle = await GetPreparedArticle(
                topic,
                question,
                p,
                useFirstNonBlacklistedSkipLlm,
                pageIdBlacklist);
            if(preparedArticle == null)
            {
                return null;
            }

            // *****************************************************************
            // ***                                                           ***
            // *****************************************************************

            // Because we are using the LLM (not the embedding model) to check,
            // how many tokens are there per paragraph.
            embContextLengthToUse =
                (int)(4.0 * (double)p.EmbContextLength / 5.0); // Tokens

            if(embContextLengthToUse <= 0)
            {
                Debug.Assert(false);
                return null;
            }

            // Also trims the paragraphs.
            paragraphs = Str.GetParagraphs(preparedArticle.Text);

            chunks = SearchHelper.GetChunks(
                paragraphs,
                preparedArticle.Title,
                p.MinChunkWordCount,
                embContextLengthToUse,
                _paragraph_skipStartsWith,
                p.GetTokenCount!);

            if(p.UseReranker)
            {
                var ordMostSimIndices = Sim.GetOrdMostSimIndices(
                        begOfAnswer,
                        chunks
                            .Select(
                                chunk =>
                                    new SimDoc(
                                            chunk, p.CreateEmbeddings!(chunk)!))
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
                        begOfAnswerEmbVec, embLimitedParagraphs!);

                maxI = Math.Min(
                    p.ChunkCountToInject, chunks.Count) - 1;

                indices = descDotProdIndices.Select(i => i.Item1).ToList();
            }

            prompt = $"### UNSORTED SOURCES ABOUT THE TOPIC \"{preparedArticle.Title}\" ###";
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
                question);

            answer = p.Infer!(SearchHelper.SysPrompt, prompt);

            answer = answer!.Trim();

            //Log.Line($"Answer from article about \"{titleAndArticle.Item1}\": \"{answer}\"");

            if (answer.ToLower().Contains(SearchHelper.NotFoundResponse.ToLower()))
            {
                return new Answer
                {
                    Text = null, // We need the page ID for the blacklist.
                    PageId = preparedArticle.PageId,
                    PageIdAndTitles = preparedArticle.PageIdAndTitles
                };
            }

            // Smaller LLMs sometimes return with an "answer" like "The answer
            // to the question could not be found in the provided sources."
            // anyway, so we check via classification.
            if(!SearchHelper.IsRealAnswer(answer))
            {
                return new Answer
                {
                    Text = null, // We need the page ID for the blacklist.
                    PageId = preparedArticle.PageId,
                    PageIdAndTitles = preparedArticle.PageIdAndTitles
                };
            }

            return new Answer
            {
                Text = answer,
                PageId = preparedArticle.PageId,
                PageIdAndTitles = preparedArticle.PageIdAndTitles
            };
        }

        /// <returns>
        /// null on error OR, if the answer could not be found.
        /// </returns>
        internal static async Task<SearchResult?> Exec(
            WikiSearchParams p, string question)
        {
            string? begOfAnswer;
            List<string>? topics;
            Answer? answer;
            List<Tuple<int, string>>? firstTopicPageIdAndTitles;
            float[]? begOfAnswerEmbVec;
            List<int> pageIdBlacklist;
            Stopwatch stopwatch;
            int attempts;

            stopwatch = Stopwatch.StartNew();

            if (question.Trim().Length == 0)
            {
                return null;
            }

            // *****************************************************************
            // *** Get single topic of question from LLM:                    ***
            // *****************************************************************

            //GetTopics(question);
            {
                var singleTopic = SearchHelper.GetSingleTopic(
                        question, p.Infer!);

                if(singleTopic == null)
                {
                    return null;
                }
                topics = [singleTopic];
            }

            // *****************************************************************
            // *** Try to find another topic via LLM:                        ***
            // *****************************************************************

            {
                var secondTopic = GetSecondTopic(question, topics[0], p.Infer!);

                if(secondTopic != null)
                {
                    Debug.Assert(0 < secondTopic.Trim().Length);
                    topics.Add(secondTopic);
                }
            }

            // *****************************************************************
            // *** Get beginning of answer (for better embeddings) from LLM: ***
            // *****************************************************************

            begOfAnswer = p.Infer!(
                SearchHelper.SysPrompt,
                string.Format(
                    SearchHelper.PromptTemplate_getBeginningOfAnswerFromQuestion,
                    question));
            if (string.IsNullOrWhiteSpace(begOfAnswer))
            {
                Debug.Assert(false); // Should not happen.
                Log.Line($"Failed to get beginning-of-answer from given question \"{question}\"!");
                return null;
            }
            begOfAnswer = begOfAnswer.Trim();

            begOfAnswerEmbVec = p.CreateEmbeddings!(begOfAnswer);
            if(begOfAnswerEmbVec == null)
            {
                return null;
            }

            // *****************************************************************
            // *** Get the answer via topics, etc. from the LLM:             ***
            // *****************************************************************

            pageIdBlacklist = new();
            answer = null;
            firstTopicPageIdAndTitles = null;
            attempts = 0;
            foreach(var topic in topics)
            {
                ++attempts;
                answer = await GetAnswer(
                    question,
                    begOfAnswer,
                    topic,
                    begOfAnswerEmbVec,
                    p,
                    false,
                    pageIdBlacklist);
                if(answer == null)
                {
                    // TODO: Check, if this is always an error!
                    Log.Line("Error: Retrieved null from get-answer method (1)!");
                    return null;
                }
                Debug.Assert(!pageIdBlacklist.Contains(answer.PageId));

                if(firstTopicPageIdAndTitles == null)
                {
                    // First topic. => Remember page IDs and titles.
                    firstTopicPageIdAndTitles = answer.PageIdAndTitles;
                }

                if(answer.Text != null)
                {
                    Debug.Assert(0 < answer.Text.Trim().Length);
                    break; // Found!
                }

                pageIdBlacklist.Add(answer.PageId);
            }
            if(answer?.Text == null)
            {
                // No answer found, yet.

                // We saved the page-ID-and-titles list from the result of the
                // first topic. Now, we are trying to find the first
                // (originally highest in the Wikipedia search results,
                // non-blacklisted page-ID-and-title entry.
                // If found, we will try to find an answer with that one.

                var fallbackPageIdAndTitle = firstTopicPageIdAndTitles!
                        .FirstOrDefault(
                            pageIdAndTitle =>
                                !pageIdBlacklist.Contains(
                                    pageIdAndTitle.Item1));

                if(fallbackPageIdAndTitle != default)
                {
                    Log.Line($"Trying to get answer via \"fallback\" topic \"{fallbackPageIdAndTitle.Item2}\"..");

                    ++attempts;
                    answer = await GetAnswer(
                        question,
                        begOfAnswer,
                        fallbackPageIdAndTitle.Item2,
                        begOfAnswerEmbVec,
                        p,
                        true, // <- Use first non-blacklisted article found.
                        pageIdBlacklist);
                    if (answer == null)
                    {
                        // TODO: Check, if this is always an error!
                        Log.Line("Error: Retrieved null from get-answer method (2)!");
                        return null;
                    }
                    Debug.Assert(!pageIdBlacklist.Contains(answer.PageId));
                }
            }
            if(answer?.Text == null)
            {
                return null;
            }

            stopwatch.Stop();
            return new SearchResult
            {
                Answer = answer.Text,
                Attempts = attempts, // A. (topics/titles) that were necessary.
                Seconds = stopwatch.Elapsed.TotalSeconds // How long it took.
            };
        }
    }
}
