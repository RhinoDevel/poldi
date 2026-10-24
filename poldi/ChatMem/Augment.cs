
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;

namespace Poldi.ChatMem
{
    /// <summary>
    /// Singleton to augment a user prompt via memory injection.
    /// </summary>
    internal static class Augment
    {
        private static readonly bool _writeToConsole =
            C.D<bool>("llm:writeToConsole");

        /// <summary>
        /// If the actual text given has less than this count of words in
        /// <see cref="GetMostSimilarStatementsFromText"/>, return no "similar"
        /// statements, which may causes skipping memory injection entirely.
        /// </summary>
        private static readonly int _memMinTextWordCount =
            C.D<int>("augment:memMinTextWordCount");

        /// <summary>
        /// In <see cref="GetAugmentedPrompt"/>, only the
        /// <see cref="_memMaxMostRecentChats"/> most recent chats will be
        /// taken into account, older chats will be ignored for memory
        /// retrieval.
        /// </summary>
        private static readonly int _memMaxMostRecentChats =
            C.D<int>("augment:memMaxMostRecentChats");

        /// <summary>
        /// Count of maximum statements the embedding cosine similarity and
        /// reranking scores comparing part returns.
        /// </summary>
        private static readonly int _memMaxStatements =
            C.D<int>("augment:memMaxStatements");

        /// <summary>
        /// Count of maximum statements the LLM-based relevance-gate returns, if
        /// the user prompt is used and the user asked a question.
        /// </summary>
        /// <remarks>
        /// Should be less than <see cref="_memMaxStatements"/>.
        /// </remarks>
        private static readonly int _memMaxStatementsQuestion =
            C.D<int>("augment:memMaxStatementsQuestion");

        /// <summary>
        /// Count of maximum statements the LLM-based relevance-gate returns, if
        /// no question is asked or the last assistant's response is used.
        /// </summary>
        /// <remarks>
        /// Should be less than <see cref="_memMaxStatements"/> and probably
        /// also less thant <see cref="_memMaxStatementsQuestion"/>.
        /// </remarks>
        private static readonly int _memMaxStatementsNoQuestion =
            C.D<int>("augment:memMaxStatementsNoQuestion");

        /// <summary>
        /// The maximum count of assistant responses ending with a question and
        /// following each other (so to say) allowed. Used to dynamically
        /// prevent the LLM to ask too many questions.
        /// </summary>
        private static readonly int _memMaxRespQuestionCount =
            C.D<int>("augment:memMaxRespQuestionCount");

        /// <summary>
        /// The maximum count of assistant responses NOT ending with a question
        /// and following each other (being "adjacent") allowed. Used to
        /// dynamically prevent the LLM to NOT asking any questions for a long
        /// time.
        /// </summary>
        private static readonly int _memMaxRespNoQuestionCount =
            C.D<int>("augment:memMaxRespNoQuestionCount");

        /// <summary>
        /// If this count of most recent, "adjacent" assistant responses had
        /// each at least <see cref="_memMaxWordCountTrigger"/> words, add
        /// dynamic rule trying to limit next LLM answer length.
        /// </summary>
        private static readonly int _memMaxWordCountRespCount =
            C.D<int>("augment:memMaxWordCountRespCount");

        private static readonly int _memMaxWordCountTrigger = // Words
            C.D<int>("augment:memMaxWordCountTrigger");

        private static readonly int _memMaxWordCountLimit = // Words.
            C.D<int>("augment:memMaxWordCountLimit");

        private static readonly int _memMinWordCountRespCount =
             C.D<int>("augment:memMinWordCountRespCount");

        private static readonly int _memMinWordCountTrigger = // Words
             C.D<int>("augment:memMinWordCountTrigger");

        private static readonly int _memMinWordCountLimit = // Words.
            C.D<int>("augment:memMinWordCountLimit");

        private static readonly string _gateSysPrompt =
            C.D<string>("augment:gateSysPrompt");
        private static readonly string _gatePrompt =
            C.D<string>("augment:gatePrompt");

        private static List<Statement> GetMostSimilarStatementsFromText(
            string text, List<Statement> inputStatements, int maxStatements)
        {
            var topIndices = Sim.GetOrdMostSimIndices(
                    text,
                    inputStatements
                        .Select(s => new SimDoc(s.Content, s.Embedding))
                        .ToList(),
                    maxStatements);

            if (topIndices == null)
            {
                Log.Line("Error: Failed to retrieve ordered most similar indices (returning empty statement list)!");
                return new List<Statement>();
            }

            Debug.Assert(topIndices.Count <= maxStatements);
            Debug.Assert(
                topIndices.TrueForAll(index => index <= inputStatements.Count));
            Debug.Assert(topIndices.Count == topIndices.Distinct().Count());

            return topIndices.Select(index => inputStatements[index]).ToList();

//#if DEBUG
//            {
//                const int topPercent = 10; // Hard-coded
//             
//                var ordStatementAndScores =
//                        statementAndScores
//                            .OrderBy(t => t.Item2)
//                            .ToList();
//                var softmax = Calc.GetSoftmax(
//                            ordStatementAndScores
//                                .Select(t => (double)t.Item2)
//                                .ToList());
//                var topPercentSum = Calc.GetTopPercentSum(softmax, topPercent);
//
//                var buf = string.Empty;
//
//                buf += "Text;Statement;Score;Softmax;Top percent;Top percent sum;";
//                for(int i = 0; i < ordStatementAndScores.Count; ++i)
//                {
//                    var t = ordStatementAndScores[i];
//
//                    buf += Environment.NewLine;
//                    buf += text + ";"; // Not escaped properly!
//                    buf += t.Item1.Content + ";"; // Not escaped properly!
//                    buf += (double)t.Item2 + ";";
//                    buf += softmax[i] + ";";
//                    buf += topPercent + ";";
//                    buf += topPercentSum + ";";
//                }
//                System.IO.File.WriteAllText("debug_rerank.csv", buf);
//            }
//#endif //DEBUG
        }

        private static List<Statement> GetMostSimilarStatements(
            string prompt,
            string? lastAssistantMsg,
            List<long> ignoreStatementNrs)
        {
            Debug.Assert(0 < prompt.Trim().Length);

            List<Statement> inputStatements;
            List<Statement> statements;
            List<Statement> outputStatements;
            int promptWordCount;
            int? lastAssistantMsgWordCount;
            bool canUsePrompt;
            bool canUseLastAssistantMsg;
            int outputMaxCount = 0;

            promptWordCount = Str.GetWordCount(prompt);
            canUsePrompt = _memMinTextWordCount <= promptWordCount;

            lastAssistantMsgWordCount = lastAssistantMsg == null
                ? null
                : Str.GetWordCount(lastAssistantMsg);
            canUseLastAssistantMsg =
                lastAssistantMsgWordCount != 0
                    && _memMinTextWordCount <= lastAssistantMsgWordCount;
            if (!canUsePrompt && !canUseLastAssistantMsg)
            {
                Log.Line($"Skipping statement(-s) injection, as user prompt has {promptWordCount} words, which is below set threshold of {_memMinTextWordCount} words and last assistant's message is {(lastAssistantMsgWordCount == null ? "not given" : $"also too short ({lastAssistantMsgWordCount} words)")}..");
                return new();
            }

            inputStatements = ChatMemDb.LoadAllStatements(
                _memMaxMostRecentChats);
            Log.Line($"Loaded {inputStatements.Count} statements from the most-recent (up to) {_memMaxMostRecentChats} chats.");

            // Ignore statements with nrs. given:
            inputStatements = inputStatements
                .Where(statement => !ignoreStatementNrs.Contains(statement.Nr))
                .ToList();

            if(inputStatements.Count == 0)
            {
                return new();
            }

            statements = new();

            if (canUsePrompt)
            {
                statements = GetMostSimilarStatementsFromText(
                    prompt, inputStatements, _memMaxStatements);
                if (0 < statements.Count)
                {
                    Log.Line($"Got {statements.Count} statements being most-similar to user prompt.");
                }

                outputMaxCount = prompt.Contains/*EndsWith*/('?')
                    ? _memMaxStatementsQuestion
                    : _memMaxStatementsNoQuestion;
            }
            if (canUseLastAssistantMsg && statements.Count == 0)
            {
                statements = GetMostSimilarStatementsFromText(
                    lastAssistantMsg!, inputStatements, _memMaxStatements);
                if (0 < statements.Count)
                {
                    Log.Line($"Got {statements.Count} statements being most-similar to last assistant msg.");
                }

                outputMaxCount = _memMaxStatementsNoQuestion;
            }

            // *****************************************************************
            // *** LLM-based relevance-gate and output statement limitation: ***
            // *****************************************************************

            outputStatements = new List<Statement>();

            {
                string response;
                string statementsStr;
                string lastAssistantMsgInfo;

                statementsStr = string.Empty;
                for(int i = 0; i < statements.Count; ++i)
                {
                    statementsStr += $"{i + 1}. {statements[i].Content}";
                    statementsStr += Environment.NewLine;
                }

                lastAssistantMsgInfo = string.Empty;
                if(lastAssistantMsg != null)
                {
                    lastAssistantMsgInfo =
                        $"{Environment.NewLine}Your last response: \"{lastAssistantMsg}\"{Environment.NewLine}";
                }

                // If not good enough, additionally use (llama.cpp) grammar.
                response = Mem.Create(
                    _gateSysPrompt,
                    string.Format(
                        _gatePrompt,
                        lastAssistantMsgInfo,
                        prompt,
                        statementsStr,
                        outputMaxCount),
                    _writeToConsole,
                    false)! // <- Do NOT de-initialize slot when done.
                        .Item1;
                Mem.DeInit(); // In case the memory slot was used.

                try
                {
                    int beg = response.IndexOf('[');
                    int end = response.IndexOf(
                                ']', beg + 1, response.Length - beg - 1);
                    var jsonStr = response.Substring(beg, end - beg + 1);
                    List<int> nrs;

                    // E.g.: ["2"] => [2]
                    jsonStr = jsonStr.Replace("\"", string.Empty);

                    using var doc = JsonDocument.Parse(jsonStr);

                    nrs = new();
                    foreach (var ele in doc.RootElement.EnumerateArray())
                    {
                        nrs.Add(ele.GetInt32());
                    }

                    outputStatements = nrs
                        .Distinct() // Avoids duplicates.
                        .Take(outputMaxCount) // Enforces (see "gatePrompt").
                        .Select(nr => statements[nr - 1])
                        .ToList();
                }
                catch (Exception)
                {
                    Log.Line("Error: Failed to get JSON result from memory LLM, injecting NO statements!");
                }
            }
            //
            //// This is too slow:
            //foreach(var statement in statements)
            //{
            //    var digProbsStats = Sel.Create(
            //            $"You are in a conversation with the user. You have the memory \"{statement.Content}\". The user's request is \"{prompt}\". Does the user's request require this memory to produce a factually correct response?", // Hard-coded
            //            ["Yes", "No"],
            //            null,
            //            _writeToConsole,
            //            false); // <- Do NOT de-initialize slot when done.
            //
            //    var maxIndex = digProbsStats!.GetMaxIndex(1, 2);
            //      
            //    if(maxIndex == 1)
            //    {
            //        outputStatements.Add(statement);
            //    }
            //    if(outputStatements.Count == outputMaxCount)
            //    {
            //        break;
            //    }
            //}
            //Sel.DeInit(); // In case the select slot was used.

            // *****************************************************************

            return outputStatements;
        }

        private static string GetParagraphMemoriesOrEmpty(
            AugmentParams p, out List<long> injectedStatementNrs)
        {
            Debug.Assert(0 < p.Prompt.Trim().Length);
            Debug.Assert(
                p.LastMsgs.Count == 0
                    || p.LastMsgs.Last().Type == MessageType.Assistant);

            string buf;
            List<Statement> statements;
            List<Tuple<Statement, Chat>> l;
            Chat? lastChat;

            lastChat = null;
            if (p.LastMsgs.Count == 0)
            {
                // For later injection of most-recent chat's summary.
                lastChat = ChatMemDb.LoadLastChat();
                Debug.Assert(
                    lastChat == null || 0 < lastChat.Summary.Trim().Length);
            }
            
            if(p.MemoryParagraphFixedInjectStatementNrs != null)
            {
                statements = new();
                foreach(
                    var statementNr in p.MemoryParagraphFixedInjectStatementNrs)
                {
                    Debug.Assert(0 < statementNr);
                    
                    var statement = ChatMemDb.LoadStatement(statementNr);

                    if(statement == null)
                    {
                        Debug.Assert(false); // Should not get here.
                        Log.Line($"Error: Failed to load fixed injected statement nr. {statementNr} from DB! Proceeding anyway (no problem)..");
                        continue;
                    }
                    statements.Add(statement);
                }
            }
            else
            {
                // Get nrs. of statements to ignore:

                var alreadyInjectedStatementNrs = new List<long>();
                
                foreach (var msg in p.LastMsgs)
                {
                    alreadyInjectedStatementNrs.AddRange(msg.InjectedStatementNrs);
                }
                
                alreadyInjectedStatementNrs = alreadyInjectedStatementNrs
                    .Distinct()
                    .ToList();

                // Get the statements:

                statements = GetMostSimilarStatements(
                    p.Prompt,
                    p.LastMsgs.Count != 0 ? p.LastMsgs.Last().Content : null,
                    alreadyInjectedStatementNrs);
            }
            if (statements.Count == 0 && lastChat == null)
            {
                injectedStatementNrs = new();
                return string.Empty;
            }

            buf = string.Empty;

            buf += "[MEMORIES]";

            if (lastChat != null)
            {
                var lastChatLocalDateTime = lastChat.UtcChatAt.ToLocalTime();

                buf += Environment.NewLine;
                buf += Environment.NewLine;
                buf += $"From {Temporal.GetDateHourMin(lastChatLocalDateTime)}";
                buf += $": Last conversation's summary:";

                buf += Environment.NewLine;
                buf += Environment.NewLine;
                buf += lastChat.Summary;

                if (statements.Count != 0)
                {
                    buf += Environment.NewLine;
                    buf += Environment.NewLine;
                    buf += "Other memories:";
                    buf += Environment.NewLine;
                }
            }

            l = statements
                    .Select(
                        statement => new Tuple<Statement, Chat>(
                            statement,
                            ChatMemDb.LoadChat( // Overdone, but OK.
                                statement.ChatNr)!))
                    // Most-recent info FIRST (ordering by score may be better):
                    .OrderByDescending(t => t.Item2.UtcChatAt)
                    .ThenBy(t => t.Item2.Nr)
                    .ThenBy(t => t.Item1.StatementIndex)
                    .ToList();

            for (int i = 0; i < l.Count; ++i)
            {
                string memory;
                Statement statement;
                Chat chat;
                DateTime chatAtLocal;

                statement = l[i].Item1;
                chat = l[i].Item2;
                chatAtLocal = chat.UtcChatAt.ToLocalTime();
                memory = string.Empty;
                memory += "- ";
                memory += $"Memory from {Temporal.GetDate(chatAtLocal)}";//$"From {Temporal.GetDate(chatAtLocal)} ({Temporal.GetDaysDesc(chatAtLocal, curDateTime)})";
                memory += $": \"{statement.Content}\"";

                // Cosine similarities currently not available to log, here.
                //Debug.Assert(_memMinCosSim <= cosSim);
                //Log.Line($"{cosSim.ToString("+0.00;-0.00", System.Globalization.CultureInfo.InvariantCulture)}: \"{memory}\"");

                buf += Environment.NewLine
                        + memory;
            }
            injectedStatementNrs = l
                .Select(t => t.Item1.Nr)
                .OrderBy(nr => nr)
                .ToList();
            return buf;
        }

        private static string GetParagraphMetaData(DateTime curDateTime)
        {
            var retVal = string.Empty;

            retVal += "[METADATA]";

            retVal += Environment.NewLine;
            retVal += $"- Current weekday, date, time: {Temporal.GetWeekdayDateHourMin(curDateTime)}";

            return retVal;
        }

        private static string GetParagraphConstraints(List<Message> lastMsgs)
        {
            var retVal = string.Empty;

            // *****************************************************************
            // *** Static constraints:                                       ***
            // *****************************************************************

            //retVal += Environment.NewLine;
            //retVal += "- Never treat past events from memories as current event, unless the user confirms it.";
            //
            //retVal += Environment.NewLine;
            //retVal += "- Keep dates of memories in mind, some infos may no longer be valid.";
            //
            //retVal += Environment.NewLine;
            //retVal += "- Reference a memory only, if it makes sense in the context of the current chat.";
            ////retVal += "- Before answering: Decide if any memory is relevant. If none are relevant, ignore all memories.";

            // *****************************************************************
            // *** Dynamic constraints:                                      ***
            // *****************************************************************

            {
                int lastQuestions = 0;

                for (int i = lastMsgs.Count - 1; 0 <= i; --i)
                {
                    var msg = lastMsgs[i];

                    if (msg.Type != MessageType.Assistant)
                    {
                        continue;
                    }

                    if (!msg.Content.Contains/*EndsWith*/('?'))
                    {
                        lastQuestions = 0;
                        break;
                    }

                    ++lastQuestions;
                    if (lastQuestions == _memMaxRespQuestionCount)
                    {
                        break;
                    }
                }
                if (lastQuestions == _memMaxRespQuestionCount)
                {
                    retVal += Environment.NewLine;
                    retVal += "- Don't ask the user a question in your response.";
                }
            }

            {
                int lastWithoutQuestions = 0;

                for (int i = lastMsgs.Count - 1; 0 <= i; --i)
                {
                    var msg = lastMsgs[i];

                    if (msg.Type != MessageType.Assistant)
                    {
                        continue;
                    }

                    if (msg.Content.Contains/*EndsWith*/('?'))
                    {
                        lastWithoutQuestions = 0;
                        break;
                    }

                    ++lastWithoutQuestions;
                    if (lastWithoutQuestions == _memMaxRespNoQuestionCount)
                    {
                        break;
                    }
                }
                if (lastWithoutQuestions == _memMaxRespNoQuestionCount)
                {
                    retVal += Environment.NewLine;
                    retVal += "- Ask the user a question in your response.";
                }
            }

            // TODO: For performance, store word counts per message in DB?

            {
                int lastWithTooManyWords = 0;

                for (int i = lastMsgs.Count - 1; 0 <= i; --i)
                {
                    var msg = lastMsgs[i];

                    if (msg.Type != MessageType.Assistant)
                    {
                        continue;
                    }

                    if (Str.GetWordCount(msg.Content)
                            < _memMaxWordCountTrigger)
                    {
                        lastWithTooManyWords = 0;
                        break;
                    }

                    ++lastWithTooManyWords;
                    if (lastWithTooManyWords == _memMaxWordCountRespCount)
                    {
                        break;
                    }
                }
                if (lastWithTooManyWords == _memMaxWordCountRespCount)
                {
                    retVal += Environment.NewLine;
                    retVal += $"- Use at most {_memMaxWordCountLimit} words in your response.";
                }
            }

            {
                int lastWithTooFewWords = 0;

                for (int i = lastMsgs.Count - 1; 0 <= i; --i)
                {
                    var msg = lastMsgs[i];

                    if (msg.Type != MessageType.Assistant)
                    {
                        continue;
                    }

                    if (_memMinWordCountTrigger
                            <= Str.GetWordCount(msg.Content))
                    {
                        lastWithTooFewWords = 0;
                        break;
                    }

                    ++lastWithTooFewWords;
                    if (lastWithTooFewWords == _memMinWordCountRespCount)
                    {
                        break;
                    }
                }
                if (lastWithTooFewWords == _memMinWordCountRespCount)
                {
                    retVal += Environment.NewLine;
                    retVal += $"- Use at least {_memMinWordCountLimit} words in your response.";
                }
            }

            if (retVal.Length != 0)
            {
                retVal = "[CONSTRAINTS]" + retVal;
            }
            return retVal;
        }

        private static string GetParagraphUserInput(string prompt)
        {
            Debug.Assert(0 < prompt.Trim().Length);

            var retVal = string.Empty;

            retVal += "[USER INPUT]";
            retVal += Environment.NewLine;
            retVal += prompt;

            return retVal;
        }

        internal static string GetAugmentedPrompt(
            AugmentParams p, out List<long> injectedStatementNrs)
        {
            var buf = string.Empty;

            // Order of paragraphs: Most to less important.

            // *****************************************************************
            // *** Constraints paragraph:                                    ***
            // *****************************************************************
            //
            if(p.ConstraintsParagraph)
            {
                var paragraph = GetParagraphConstraints(p.LastMsgs);

                if (paragraph.Length != 0)
                {
                    if (buf.Length != 0)
                    {
                        buf += Environment.NewLine;
                        buf += Environment.NewLine;
                    }
                    buf += paragraph;
                }
            }

            // *****************************************************************
            // *** Actual user input paragraph:                              ***
            // *****************************************************************
            //
            {
                var paragraph = GetParagraphUserInput(p.Prompt);

                if (paragraph.Length != 0)
                {
                    if (buf.Length != 0)
                    {
                        buf += Environment.NewLine;
                        buf += Environment.NewLine;
                    }
                    buf += paragraph;
                }
            }

            // *****************************************************************
            // *** Memory paragraph (may be an empty string):                ***
            // *****************************************************************
            //
            if(p.MemoryParagraph)
            {
                var paragraph = GetParagraphMemoriesOrEmpty(
                        p, out injectedStatementNrs);

                if (paragraph.Length != 0)
                {
                    if (buf.Length != 0)
                    {
                        buf += Environment.NewLine;
                        buf += Environment.NewLine;
                    }
                    buf += paragraph;
                }
            }
            else
            {
                injectedStatementNrs = new();
            }

            // *****************************************************************
            // *** Meta data paragraph:                                      ***
            // *****************************************************************
            //
            {
                var paragraph = GetParagraphMetaData(p.PromptDateTime);

                if (paragraph.Length != 0)
                {
                    if (buf.Length != 0)
                    {
                        buf += Environment.NewLine;
                        buf += Environment.NewLine;
                    }
                    buf += paragraph;
                }
            }

            return buf;
        }
    }
}