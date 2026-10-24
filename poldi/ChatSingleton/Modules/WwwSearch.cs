
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

// Marcel Timm, RhinoDevel, 2026oct02

using Poldi.Search;
using System;
using System.Diagnostics;

namespace Poldi.ChatSingleton.Modules
{
    internal class WwwSearch : Module
    {
        private enum State
        {
            Start,
            CheckInput,
            Search,
            Done
        }

        private const string _intention = "An internet search.";

        private static readonly string _askQuestionForWww =
            C.D<string>("lang:askQuestionForWww");
        private static readonly string _skippingWwwSearch =
            C.D<string>("lang:skippingWwwSearch");
        private static readonly string _answerNotFoundInWww =
            C.D<string>("lang:answerNotFoundInWww");

        private static readonly bool _writeToConsole =
            C.D<bool>("llm:writeToConsole");

        // *********************************************************************
        // *** Fix configuration settings:                                   ***
        // *********************************************************************

        private static readonly bool _useReranker =
            C.D<bool>("www:useReranker");
        private static readonly int _minChunkWordCount =
            C.D<int>("www:minChunkWordCount");
        private static readonly int _chunkCountToInject =
            C.D<int>("www:chunkCountToInject");

        // *********************************************************************

        private State _state = State.Start;

        // *********************************************************************
        // *** Callbacks to be used by WwwSearch:                           ***
        // *********************************************************************

        private static string Infer(string sysPrompt, string prompt)
        {
            return Mem.Create(
                sysPrompt,
                prompt,
                _writeToConsole,
                true)! // <- De-initialize memory slot, if used.
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
            var option = digProbsStats!.GetMaxIndex(1, options.Length);

            Log.Line($"Option nr.: {option} (confidence: {digProbsStats!.GetConfidence(1, options.Length)})");

            // Return index into options, not into (digit) probabilities:
            return option - 1;
        }

        // *********************************************************************

        private string OnStart(
            string input, out bool isDone, out bool reqImmedUserResp)
        {
            isDone = false;
            reqImmedUserResp = true;
            _state = State.CheckInput;
            return _askQuestionForWww;
        }

        /// <summary>
        /// Check, if the input is really a question for the internet. If not,
        /// the search is not going to happen.
        /// </summary>
        private string OnCheckInput(
            string input,
            out bool calledByMistake,
            out bool isDone,
            out bool reqImmedUserResp)
        {
            reqImmedUserResp = false;

            if (!SearchHelper.IsQuery(input))
            {
                isDone = true;
                _state = State.Done;
                calledByMistake = true;
                return _skippingWwwSearch;
            }
            isDone = false;
            _state = State.Search;
            calledByMistake = false;
            return string.Empty; // Empty => no user input before next step.
        }

        private string OnSearch(
            string input, out bool isDone, out bool reqImmedUserResp)
        {
            Search.SearchResult? searchResult;
            bool saveAndRestoreChatState;

            saveAndRestoreChatState =
                CppInterface.MtLlm.MtLlm.IsInit((int)SlotIndex.Chat)
                    && (Mem.IsUsingChatSlotPossible()
                            || Sel.IsUsingChatSlotPossible());

            // *****************************************************************
            // *** Save chat state once, if possible/necessary (to prevent   ***
            // *** Mem and/or Sel singletons doing an update and restore for ***
            // *** each call):                                               ***
            // *****************************************************************

            if (saveAndRestoreChatState)
            {
                CppInterface.MtLlm.MtLlm.SnapshotUpdate((int)SlotIndex.Chat);

                Debug.Assert(Mem.GetRestoreChatAfterSlotUse());
                Mem.SetRestoreChatAfterSlotUse(false);

                Debug.Assert(Sel.GetRestoreChatAfterSlotUse());
                Sel.SetRestoreChatAfterSlotUse(false);
            }

            searchResult = Poldi.WwwSearch.WwwSearch.Exec(
                new Poldi.WwwSearch.WwwSearchParams
                {
                    UseReranker = _useReranker,
                    EmbContextLength = Emb.GetMaxTokenCount()!.Value,
                    MinChunkWordCount = _minChunkWordCount,
                    ChunkCountToInject = _chunkCountToInject,
                    CreateEmbeddings = Emb.Create,
                    GetTokenCount = (string text, bool addSpecial)
                        => Mem.GetTokenCount(
                            text,
                            addSpecial,
                            true), // <- De-initialize, if mem. slot was used.
                    Infer = Infer,
                    GetSelectIndex = GetSelectIndex
                },
                input)
                    .GetAwaiter()
                    .GetResult();

            if (saveAndRestoreChatState)
            {
                CppInterface.MtLlm.MtLlm.SnapshotRestore((int)SlotIndex.Chat);

                Debug.Assert(!Mem.GetRestoreChatAfterSlotUse());
                Mem.SetRestoreChatAfterSlotUse(true);

                Debug.Assert(!Sel.GetRestoreChatAfterSlotUse());
                Sel.SetRestoreChatAfterSlotUse(true);
            }

            isDone = true;
            _state = State.Done;
            reqImmedUserResp = false;
            if(string.IsNullOrWhiteSpace(searchResult?.Answer))
            {
                return _answerNotFoundInWww;
            }
            return searchResult.Answer;
        }

        internal override string GetIntention() => _intention;
        internal override Module Create() => new WwwSearch();

        internal override string Exec(
            string input,
            out bool calledByMistake,
            out bool isDone,
            out bool reqImmedUserResp)
        {
            calledByMistake = false;

            do
            {
                var output = string.Empty;

                switch (_state)
                {
                    case State.Start:
                    {
                        output = OnStart(
                            input, out isDone, out reqImmedUserResp);
                        break;
                    }
                    case State.CheckInput:
                    {
                        output = OnCheckInput(
                            input,
                            out calledByMistake,
                            out isDone,
                            out reqImmedUserResp);
                        break;
                    }
                    case State.Search:
                    {
                        output = OnSearch(
                            input, out isDone, out reqImmedUserResp);
                        break;
                    }

                    case State.Done: // Should not be called, if in done state.
                    default:
                    {
                        Debug.Assert(false); // Must not get here.
                        throw new Exception("Unsupported state given!");
                    }
                }

                if (isDone)
                {
                    // The modules is done with its whole task.

                    Debug.Assert(0 < output.Trim().Length); // Resp. required.

                    Debug.Assert(_state == State.Done);
                    return output;
                }

                // The module is NOT done with the whole task.
                Debug.Assert(_state != State.Done);

                if (0 < output.Length)
                {
                    // The module needs new user input for its next step.

                    Debug.Assert( // The output must not be whitespace-only.
                        0 < output.Trim().Length);

                    return output;
                }

                // The module does not need new user input to proceed (indicated
                // by an empty output string).
            } while (true);
        }
    }
}
