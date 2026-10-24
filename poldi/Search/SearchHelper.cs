
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Poldi.Search
{
    internal static class SearchHelper
    {
        // *********************************************************************
        // *** Initialized via static constructor:                           ***
        // *********************************************************************
        //
        internal static readonly string SysPrompt;
        internal static readonly string
            PromptTemplate_getSingleTopicFromSentence;
        internal static readonly string PromptTemplate_getTopicsFromSentence;
        internal static readonly string PromptTemplate_getAnswerPostfix;
        internal static readonly string
            PromptTemplate_getBeginningOfAnswerFromQuestion;

        // *********************************************************************
        // *** Constants:                                                    ***
        // *********************************************************************

        internal const string NotFoundResponse = "<NOT FOUND>";

        private static readonly bool _writeToConsole =
            C.D<bool>("llm:writeToConsole");

        static SearchHelper()
        {
            string relPath = AppContext.BaseDirectory
                + "Search" // <- Hard-coded!
                + Path.DirectorySeparatorChar.ToString();

            SysPrompt = File.ReadAllText(
                relPath + "systemPromptTemplate.txt");
            PromptTemplate_getSingleTopicFromSentence = File.ReadAllText(
                relPath + "promptTemplate_getSingleTopicFromSentence.txt");
            PromptTemplate_getTopicsFromSentence = File.ReadAllText(
                relPath + "promptTemplate_getTopicsFromSentence.txt");
            PromptTemplate_getAnswerPostfix = File.ReadAllText(
                relPath + "promptTemplate_getAnswerPostfix.txt");
            PromptTemplate_getBeginningOfAnswerFromQuestion = File.ReadAllText(
                relPath
                    + "promptTemplate_getBeginningOfAnswerFromQuestion.txt");
        }

        /// <summary>
        /// Retrieve multiple topics from a single query.
        /// </summary>
        private static List<string>? GetTopics(
            string sentence, int maxTopics, Func<string, string, string?> infer)
        {
            string? topics;
            string prompt;
            List<string> topicsList;

            if (maxTopics <= 0)
            {
                Debug.Assert(false);
                return null;
            }

            prompt = string.Format(
                PromptTemplate_getTopicsFromSentence, maxTopics, sentence);

            topics = infer(SysPrompt, prompt);
            if (string.IsNullOrWhiteSpace(topics))
            {
                return null;
            }

            topics = topics.Trim();

            topicsList = topics
                .Split("\n")
                .ToList()
                .Select(topic => topic.Trim())
                .ToList();
            //Log.Line($"Topics: \"{string.Join("\", \"", topicsArr)}\"");

            return topicsList;
        }

        /// <summary>
        /// Retrieve a single (main) topic.
        /// </summary>
        internal static string? GetSingleTopic(
            string sentence, Func<string, string, string?> infer)
        {
            string? topic;
            string prompt;

            prompt = string.Format(
                PromptTemplate_getSingleTopicFromSentence, sentence);

            topic = infer(SysPrompt, prompt);
            if (string.IsNullOrWhiteSpace(topic))
            {
                return null;
            }

            topic = topic.Trim();

            //Log.Line($"Single topic: \"{topic}\"");

            return topic;
        }

        /// <remarks>
        /// Given <paramref name="paragraphs"/> must use '\n' for line breaks,
        /// only and must not start or end with any whitespace characters.
        /// </remarks>
        internal static List<string> GetChunks(
            List<string> paragraphs,
            string articleTitle,
            int minChunkWordCount,
            int embCtxLen,
            string[] skipStartsWith,
            Func<string, bool, int> getTokenCount)
        {
            var retVal = new List<string>();

            foreach (var paragraph in paragraphs)
            {
                string[] lines;
                bool allLinesShort;

                // Skip paragraphs where we know that they don't hold useful
                // text data, because of their heading.
                if (skipStartsWith.Any(paragraph.ToLower().StartsWith))
                {
                    continue; // Skip
                }

                // This is not 100% correct, if we are using the LLM (and not
                // the embedding model) to calculate the tokens.
                if (getTokenCount(paragraph, false) <= embCtxLen)
                {
                    retVal.Add(paragraph); // Whole paragraph fits.
                    continue;
                }

                // Split by UNIX newlines.

                lines = paragraph.Split('\n');
                allLinesShort = true;
                foreach (var line in lines)
                {
                    // This is not 100% correct, if we are using the LLM (and
                    // not the embedding model) to calculate the tokens.
                    if (embCtxLen < getTokenCount(line, false))
                    {
                        allLinesShort = false;
                        break;
                    }
                }
                if (allLinesShort)
                {
                    retVal.AddRange(lines); // All lines fit in one chunk each.
                    continue;
                }

                // Split long lines by ". ".

                foreach (var line in lines)
                {
                    // TODO: Could use cached result from above (at least
                    //       for some)..
                    //
                    // This is not 100% correct, if we are using the LLM (and
                    // not the embedding model) to calculate the tokens.
                    if (getTokenCount(line, false) <= embCtxLen)
                    {
                        retVal.Add(line); // Current line fits into one chunk.
                    }
                    else
                    {
                        string[] sentences = line.Split(
                            [". "], StringSplitOptions.None);

                        foreach (var sentence in sentences)
                        {
                            if (!string.IsNullOrWhiteSpace(sentence))
                            {
                                var trimmedSentence = sentence.Trim();

                                // TODO: This must NOT necessarily be true!
                                Debug.Assert(
                                    getTokenCount(trimmedSentence, false)
                                        <= embCtxLen);

                                retVal.Add(trimmedSentence);
                            }
                        }
                    }
                }
            }

            // Ignore too short chunks.
            retVal = retVal
                .Where(chunk => minChunkWordCount <= Str.GetWordCount(chunk))
                .ToList();

            // Ignore chunks that seem to hold some kind of "heading".
            retVal = retVal
                .Where(chunk => !chunk.Trim().EndsWith(":"))
                .ToList();

            // Ignore adjacent chunks that seem to represent a list of
            // article (title) related names (originally implemented for
            // Wikipedia search).
            retVal = retVal
                .Where(
                    chunk =>
                    {
                        const int lineCount = 2; // MAGIC number of lines..
                        const int charCount = 12; // MAGIC number of chars..

                        var lowerTopic = articleTitle.ToLower();
                        var lines = chunk.Split('\n');
                        string firstTrimmedLowerLineCharCount;
                        bool notAllStartWithTopic;
                        bool notAllBeginWithSame;

                        if (lines.Length < lineCount)
                        {
                            return true;
                        }
                        notAllStartWithTopic = false;
                        notAllBeginWithSame = false;

                        // For the compiler:
                        firstTrimmedLowerLineCharCount = string.Empty;

                        for (int i = 0; i < lines.Count(); ++i)
                        {
                            string trimmedLowerLine;

                            // Shortcut:
                            if (notAllBeginWithSame && notAllStartWithTopic)
                            {
                                return true;
                            }

                            trimmedLowerLine = lines[i].Trim().ToLower();

                            if (!notAllStartWithTopic // (otherw. alr. found)
                                    && !trimmedLowerLine.StartsWith(lowerTopic))
                            {
                                notAllStartWithTopic = true; // Found one.
                            }

                            if (i == 0)
                            {
                                if (trimmedLowerLine.Length < charCount)
                                {
                                    // Too short, skip this test.
                                    notAllBeginWithSame = true;
                                    continue;
                                }

                                firstTrimmedLowerLineCharCount =
                                    trimmedLowerLine.Substring(0, charCount);
                                continue;
                            }
                            if (!notAllBeginWithSame // (otherwise already found)
                                    && !trimmedLowerLine.StartsWith(
                                            firstTrimmedLowerLineCharCount))
                            {
                                notAllBeginWithSame = true; // Found one.
                            }
                        }
                        return notAllBeginWithSame && notAllStartWithTopic;
                    })
                .ToList();

            return retVal;
        }

        internal static bool IsRealAnswer(string response)
        {
            // These should have been checked before.
            Debug.Assert(0 < response.Trim().Length);
            Debug.Assert(response.ToLower() != NotFoundResponse.ToLower());

            string[] options = [
                    "Yes, the response holds a real answer",
                    "No, the response just states that the answer was not found."
                ];
            ChatSingleton.DigProbsStats? digProbsStats;

            digProbsStats = Sel.Create(
                $"Does the response \"{response}\" hold a real answer or does it just state that the answer was not found?",
                options,
                "An LLM was queried for an answer. You shall decide, if its response provides a real answer or not.",
                _writeToConsole,
                true);

            return digProbsStats!.GetMaxIndex(1, options.Length) == 1; // Yes
        }

        internal static bool IsQuery(string userPrompt)
        {
            // This should have been checked before.
            Debug.Assert(0 < userPrompt.Trim().Length);

            string[] options = [
                    "Yes, the user prompt holds a query (e.g. a question to answer).",
                    "No, the user prompt does not hold a query or question."
                ];
            ChatSingleton.DigProbsStats? digProbsStats;

            digProbsStats = Sel.Create(
                $"Does the user prompt \"{userPrompt}\" hold a query (e.g. a question)?",
                options,
                "A user prompt was given to an LLM. You shall decide, if it represents an actual query (e.g. a question) or not.",
                _writeToConsole,
                true);

            return digProbsStats!.GetMaxIndex(1, options.Length) == 1; // Yes
        }
    }
}
