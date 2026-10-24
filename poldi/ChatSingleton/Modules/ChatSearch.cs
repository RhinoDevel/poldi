
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

// Marcel Timm, RhinoDevel, 2026aug08

using Poldi.Search;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Poldi.ChatSingleton.Modules
{
    internal class ChatSearch : Module
    {
        private enum State
        {
            Start,
            Confirm,
            Search,
            Done
        }

        private static readonly string _notFoundUserResponse =
            C.D<string>("lang:topicNotFoundInChatHistory");
        private static readonly string _doYouWantMeToSearchChatHistory =
            C.D<string>("lang:doYouWantMeToSearchChatHistory");
        private static readonly string _skippingChatHistorySearch =
            C.D<string>("lang:skippingChatHistorySearch");

        private static readonly bool _writeToConsole =
            C.D<bool>("llm:writeToConsole");

        private const string _intention =
            "To search memory for a specific topic.";

        // *********************************************************************
        // *** Fix configuration settings:                                   ***
        // *********************************************************************

        private static readonly int _llmMaxCandidateCount =
            C.D<int>("history:llmMaxCandidateCount");

        private static readonly int _maxResponseWordCount =
            C.D<int>("history:maxResponseWordCount");

        // *********************************************************************

        private State _state = State.Start;
        private string _confirmedUserInput = string.Empty;

        private static string? GetTopic(string input)
        {
            var sysPrompt =
                    """
                    You will be given a task.
                    Follow the instructions completely.
                    """;
            var prompt =
                    $"""
                    Extract the topic from the following user request.

                    User request: "{input}"

                    Answer with that extracted topic, only!
                    If no topic can be extracted, answer with only "{SearchHelper.NotFoundResponse}" instead.
                    """;
            var tuple = Mem.Create(
                    sysPrompt,
                    prompt,
                    _writeToConsole,
                    true); // <- De-initialize, if memory slot was used.
            string topic;

            if(tuple == null)
            {
                Debug.Assert(false); // Should not get here.
                return null;
            }

            topic = tuple.Item1;

            if(topic.Trim().Length == 0)
            {
                Debug.Assert(false); // Should not get here.
                return null;
            }

            if (topic.ToLower().Contains(SearchHelper.NotFoundResponse.ToLower()))
            {
                return null; // It was not possible to extract the topic.
            }

            Log.Line(
                $"Extracted topic \"{topic}\" from user input \"{input}\".");
            return topic;
        }

        private static string? GetResponse(string topic, ChatMem.Chat chat)
        {
            var sysPrompt =
                    """
                    You will be given a task.
                    Follow the instructions completely.
                    """;
            var messages = ChatMem.ChatMemDb.LoadOrdMsgs((int)chat.Nr);
            var prompt =
                    $"""
                    A topic and a dialog protocol follow.
                    Summarize everything that was spoken about the topic in not more than {_maxResponseWordCount} words.
                    Write from the perspective of the assistant that recalls talking about the topic in a follow-up dialog with the user,
                    like "On {Temporal.GetDate(chat.UtcChatAt)}, we talked about the topic. You told me ...".

                    Topic: "{topic}"

                    Dialog protocol:

                    {ChatMem.Extract.GetDialog(messages.GetRange(1, messages.Count - 1))}

                    ---

                    Answer with the summary, only!
                    Keep the focus on the given topic, the other parts of the conversation are of no interest.
                    Use full sentences. Do not use formatting symbols or structural markers.
                    If nothing was spoken about the topic at all, answer with only "{SearchHelper.NotFoundResponse}" instead.
                    """;
            var tuple = Mem.Create(
                    sysPrompt,
                    prompt,
                    _writeToConsole,
                    true); // <- De-initialize, if memory slot was used.
            string response;

            if (tuple == null)
            {
                Debug.Assert(false); // Should not get here.
                return null;
            }

            response = tuple.Item1;

            if (response.Trim().Length == 0)
            {
                Debug.Assert(false); // Should not get here.
                return null;
            }

            if (response.ToLower().Contains(SearchHelper.NotFoundResponse.ToLower()))
            {
                return null; // It was not possible to extract the topic.
            }

            Log.Line($"Extracted response \"{response}\" from given chat.");
            return response;
        }

        private static string GetSearchResult(string input)
        {
            string? topic;
            List<ChatMem.Chat> allChats;
            List<int> rerankIndices;
            List<ChatMem.Chat> llmInputChats;
            ChatMem.Chat? chatToUse;
            string? response;

            topic = GetTopic(input);
            if(topic == null)
            {
                return _notFoundUserResponse;
            }
            Debug.Assert(0 < topic.Trim().Length);

            // The current implementation needs to load all chats and keep them
            // in memory at once, anyway, to be able to compare the cosine
            // similarity scores..
            allChats = ChatMem.ChatMemDb.LoadAllChatNrs()
                .Select(chatNr => ChatMem.ChatMemDb.LoadChat(chatNr)!)
                .ToList();

            if(allChats.Count == 0)
            {
                return _notFoundUserResponse;
            }

            // Assuming that the topic token count is never too high for the
            // model.
            rerankIndices = Sim.GetOrdMostSimIndices(
                topic,
                allChats
                    .Select(c => new SimDoc(c.Summary, c.Embedding))
                    .ToList(),
                _llmMaxCandidateCount)!;

            Debug.Assert(
                rerankIndices.TrueForAll(index => index < allChats.Count));
            Debug.Assert(rerankIndices.Count <= _llmMaxCandidateCount);
            Debug.Assert(
                rerankIndices.Count == rerankIndices.Distinct().Count());

            llmInputChats = rerankIndices
                .Select(index => allChats[index])
                .ToList();

            // Find most-recent top-scoring chat that also has the topic
            // included based on LLM check:

            chatToUse = null;
            foreach(var llmInputChat in llmInputChats)
            {
                string[] options = ["Yes", "No"];
                DigProbsStats digProbsStats = Sel.Create(
                    $"""
                    Does the following summary of a dialog indicate that at least one topic of that dialog was "{topic}"?

                    The dialog's summary:

                    {llmInputChat.Summary}
                    """,
                    options,
                    null,
                    _writeToConsole,
                    true)!; // <- De-initialize, if select slot was used.
                int option = digProbsStats!.GetMaxIndex(1, options.Length);

                Log.Line($"Option nr.: {option} (confidence: {digProbsStats!.GetConfidence(1, options.Length)})");

                if (option == 1) // 1 <=> "Yes".
                {
                    chatToUse = llmInputChat;
                    break;   
                }
            }
            if(chatToUse == null)
            {
                return _notFoundUserResponse;
            }

            response = GetResponse(topic, chatToUse);
            if (response == null)
            {
                return _notFoundUserResponse;
            }
            Debug.Assert(0 < response.Trim().Length);
            return response;
        }

        private string OnStart(
            string input, out bool isDone, out bool reqImmedUserResp)
        {
            isDone = false;
            reqImmedUserResp = true;
            _state = State.Confirm;
            _confirmedUserInput = input;
            return _doYouWantMeToSearchChatHistory;
        }

        /// <summary>
        /// Check, if the user agreed to the chat history search. If not, the
        /// search is not going to happen.
        /// </summary>
        private string OnConfirm(
            string input,
            out bool calledByMistake,
            out bool isDone,
            out bool reqImmedUserResp)
        {
            Debug.Assert(0 < _confirmedUserInput.Trim().Length);

            DigProbsStats? digProbsStats;
            string[] options;
            int option;

            reqImmedUserResp = false;

            options = ["Yes", "No"];
            digProbsStats = Sel.Create(
                $"""
                Does the following user response indicate agreement or confirmation?
                
                User response: "{input}"
                """,
                options,
                null,
                _writeToConsole,
                true); // <- De-initialize, if select slot was used.
            option = digProbsStats!.GetMaxIndex(1, options.Length);
            Log.Line($"Option nr.: {option} (confidence: {digProbsStats!.GetConfidence(1, options.Length)})");

            if (option != 1) // 1 <=> "Yes".
            {
                isDone = true;
                _state = State.Done;
                calledByMistake = true;
                return _skippingChatHistorySearch;
            }
            isDone = false;
            _state = State.Search;
            calledByMistake = false;
            return string.Empty; // Empty => no user input before next step.
        }

        private string OnSearch(
            string input, out bool isDone, out bool reqImmedUserResp)
        {
            Debug.Assert(0 < _confirmedUserInput.Trim().Length);

            string retVal;
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

            retVal = GetSearchResult(_confirmedUserInput);

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
            return retVal;
        }

        internal override Module Create() => new ChatSearch();

        internal override string GetIntention() => _intention;

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
                    case State.Confirm:
                    {
                        output = OnConfirm(
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
