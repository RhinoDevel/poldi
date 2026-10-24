
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

// RhinoDevel, Marcel Timm, 2025mar16

using Poldi.CppInterface.MtLlm;
using Poldi.WikiSearch;
using System;

namespace Poldi
{
    /// <summary>
    /// Wikipedia search via console/terminal.
    /// </summary>
    internal static class ConsoleWiki
    {
        private static readonly bool _writeToConsole =
            C.D<bool>("llm:writeToConsole");

        // *********************************************************************
        // *** Fix configuration settings:                                   ***
        // *********************************************************************

        private static readonly bool _useReranker =
            C.D<bool>("wikipedia:useReranker");
        private static readonly int _maxTopics =
            C.D<int>("wikipedia:maxTopics");
        private static readonly int _minChunkWordCount =
            C.D<int>("wikipedia:minChunkWordCount");
        private static readonly int _chunkCountToInject =
            C.D<int>("wikipedia:chunkCountToInject");

        private const char _cmdPrefix = '@';

        // *********************************************************************
        // *** Callbacks to be used by WikiSearch:                           ***
        // *********************************************************************

        private static float[]? CreateEmbeddings(string text)
            => Emb.Create(text);

        private static int GetTokenCount(string text, bool addSpecial)
            => Mem.GetTokenCount(
                text,
                addSpecial,
                true); // <- De-initialize, if memory slot was used.

        private static string? Infer(string sysPrompt, string prompt)
        {
            return Mem.Create(
                sysPrompt,
                prompt,
                _writeToConsole,
                true)! // <- De-initialize, if select slot was used.
                    .Item1;
        }

        private static int GetSelectIndex(string question, string[] options)
        {
            var digProbsStats = Sel.Create(
                    question,
                    options,
                    null,
                    _writeToConsole,
                    true); // <- De-initialize, if select slot was used.

            Log.Line($"Confidence: {digProbsStats!.GetConfidence(1, options.Length)}");

            // Return index into options, not into (digit) probabilities:
            return digProbsStats.GetMaxIndex(1, options.Length) - 1;
        }

        // *********************************************************************

        private static void PrintFixConfigSettings()
        {
            Console.WriteLine($"[LLMs write to console: {_writeToConsole}]");
            Console.WriteLine($"[Use reranker: {_useReranker}]");
            Console.WriteLine($"[Command prefix: \"{_cmdPrefix}\"]");
            Console.WriteLine($"[Maximum topic count: {_maxTopics}]");
            Console.WriteLine($"[Minimum chunk word count: {_minChunkWordCount}]");
            Console.WriteLine($"[Chunk count to inject: {_chunkCountToInject}]");
        }

        private static void ExecCmd(string cmd, out bool exit)
        {
            exit = false;
            switch (cmd)
            {
                case "exit":
                {
                    Console.WriteLine("[Triggering exit..]");
                    exit = true;
                    return;
                }

                default:
                {
                    Console.WriteLine($"[Unsupported command \"{cmd}\" given. Doing nothing..]");
                    return;
                }
            }
        }

        private static string ReadUserInput()
        {
            string? buf;

            while (true)
            {
                Console.Write("> ");
                buf = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(buf))
                {
                    return buf.Trim();
                }
                Console.WriteLine("[Please enter a question or a command.]");
            }
        }

        internal static bool Exec()
        {
            PrintFixConfigSettings();
            Console.WriteLine($"[LLM file: {Mem.GetModelFilePath()}]");
            Console.WriteLine($"[Emb. model file: {Emb.GetModelFilePath()}]");

            while (true)
            {
                string userInput;
                Search.SearchResult? searchResult;

                userInput = ReadUserInput();
                if(userInput.StartsWith(_cmdPrefix))
                {
                    // The user input seems to be a command.

                    bool exit;

                    ExecCmd(userInput[1..], out exit);
                    if (exit)
                    {
                        break; // => Exit application.
                    }
                    continue;
                }

                // The user input is interpreted as question.

                // Trick to make Mem and Sel re-use the chat slot.
                MtLlm.ReInit(
                    LlmPCreate.FromConfigMem(null), (int)SlotIndex.Chat);

                searchResult = WikiSearch.WikiSearch.Exec(
                    new WikiSearchParams
                    {
                        UseReranker = _useReranker,
                        EmbContextLength = Emb.GetMaxTokenCount()!.Value,
                        MaxTopics = _maxTopics,
                        MinChunkWordCount = _minChunkWordCount,
                        ChunkCountToInject = _chunkCountToInject,
                        CreateEmbeddings = CreateEmbeddings,
                        GetTokenCount = GetTokenCount,
                        Infer = Infer,
                        GetSelectIndex = GetSelectIndex
                    },
                    userInput)
                        .GetAwaiter()
                        .GetResult();

                // Belongs to the trick, above:
                MtLlm.DeInit((int)SlotIndex.Chat);

                Console.WriteLine(
                    $"The answer to the question \"{userInput}\" "
                        + (string.IsNullOrWhiteSpace(searchResult?.Answer)
                            ? "could not be found."
                            : $"is: \"{searchResult.Answer}\" [took {searchResult.Attempts} attempt(-s) and {searchResult.Seconds} seconds]"));
            }
            return true;
        }
    }
}
