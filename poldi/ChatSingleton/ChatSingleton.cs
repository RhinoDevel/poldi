
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

// RhinoDevel, Marcel Timm, 2025nov12

using Poldi.ChatMem;
using Poldi.CppInterface.MtLlm;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Poldi.ChatSingleton
{
    /// <summary>
    /// This is "the core" of Poldi. It is used to communicate with the LLM used
    /// for chat mode. It also handles the modules, writing the chat protocol to
    /// DB, import of messages in general and the "infinite chat" feature.
    /// </summary>
    internal static class ChatSingleton
    {
        private static readonly bool _memoryParagraph =
            C.D<bool>("augment:memoryParagraph");

        private static readonly ActiveModule _activeModule = new();

        private static readonly List<Message> _lastMsgs = new();
        
        /// <summary>
        /// If set to true (either by <see cref="Cancel"/> method
        /// called or by an exceptional error in the callback), the inference
        /// engine will be signalized to stop by the callback during its next
        /// call.
        /// </summary>
        private static volatile bool _cancelRequested = false;

        private static bool _isImportActive = false;

        private static ChatSingletonInternal? _internal = null;

        private static bool Callback(
            int tok, string piece, MtLlmTokType type, float[]? digProbs)
        {
            Debug.Assert(_internal != null);

            if (_internal.WriteToConsole)
            {
                Piece.ConsoleWrite(piece, type);
            }

            switch(type)
            {
                case MtLlmTokType.SYS_PROMPT: // Cannot get canceled.
                {
                    _internal.Last.System += piece;
                    break;
                }
                case MtLlmTokType.PROMPT: // Cannot get canceled.
                {
                    _internal.Last.Query += piece;
                    break;
                }
                case MtLlmTokType.SAMPLED_NON_EOG_NON_CONTROL:
                {
                    if(_isImportActive)
                    {
                        break;
                    }

                    _internal.Last.Response += piece;

                    if (_internal.OnSampledNonEogNonCtrl != null
                            && !_internal.OnSampledNonEogNonCtrl(
                                new CallbackParams
                                {
                                    Piece = piece,
                                    LastText = _internal.Last.Response,
                                    ForceOutput = false,
                                    ReqImmedUserResp = true
                                }))
                    {
                        Cancel();
                    }
                    break;
                }

                case MtLlmTokType.IRQ: // (falls through)
                case MtLlmTokType.SAMPLED_EOG: // (falls through)
                case MtLlmTokType.DELIM: // (falls through)
                case MtLlmTokType.SAMPLED_CONTROL_NON_EOG: // (falls through)
                case MtLlmTokType.MT_TOK_TYPE_THINK_BEGIN: // (falls through)
                case MtLlmTokType.MT_TOK_TYPE_THINK_TEXT: // (falls through)
                case MtLlmTokType.MT_TOK_TYPE_THINK_END:
                {
                    break;
                }

                default:
                {
                    throw new NotImplementedException(
                        "Unsupported token type given!");
                }
            }
            _internal.Last.Type = type;

            return _cancelRequested; // Stop inference, if cancel was requested.
        }

        private static void UpdateLastMsgsAndWriteToDb(
            string promptGiven,
            List<long> injectedStatementNrs,
            ChatSingletonLast last)
        {
            if (last.System.Length != 0) // E.g. empty, if follow-up.
            {
                _lastMsgs.Clear();

                _lastMsgs.Add(
                    new Message
                    {
                        MessageIndex = _lastMsgs.Count,
                        Type = MessageType.System,
                        Content = last.System,
                        Interrupted = false,
                        //InjectedStatementNrs // Always empty, here.
                    });
                // Immediately store in database and update object:
                _lastMsgs[_lastMsgs.Count - 1] =
                    ChatMemDb.LoadMsg(ChatMemDb.InsertMsg(_lastMsgs.Last()))!;
                Debug.Assert(0 < _lastMsgs.Last().Nr);
            }

            Debug.Assert(last.Query.Length != 0);
            _lastMsgs.Add(
                new Message
                {
                    MessageIndex = _lastMsgs.Count,
                    Type = MessageType.User,

                    // Technically not 100% correct to use given prompt, but we
                    // cannot use the last query, because it might hold the text
                    // added by the memory injection (which shall NOT be used to
                    // create new memories, later).
                    Content = promptGiven/*last.Query*/,

                    Interrupted = false,
                    InjectedStatementNrs = injectedStatementNrs
                });
            // Immediately store in database and update object:
            _lastMsgs[_lastMsgs.Count - 1] =
                ChatMemDb.LoadMsg(ChatMemDb.InsertMsg(_lastMsgs.Last()))!;
            Debug.Assert(0 < _lastMsgs.Last().Nr);

            Debug.Assert(last.Response.Length != 0);
            _lastMsgs.Add(
                new Message
                {
                    MessageIndex = _lastMsgs.Count,
                    Type = MessageType.Assistant,
                    Content = last.Response,
                    Interrupted = _cancelRequested,
                    //InjectedStatementNrs // Always empty, here.
                });
            // Immediately store in database and update object:
            _lastMsgs[_lastMsgs.Count - 1] =
                ChatMemDb.LoadMsg(ChatMemDb.InsertMsg(_lastMsgs.Last()))!;
            Debug.Assert(0 < _lastMsgs.Last().Nr);
        }

        private static bool ImportIntoContext(
            List<Tuple<DateTime, string, List<long>>> msgsToImport,
            bool useMemorySystem,
            int slotIndex,
            string? sysPrompt)
        {
            Debug.Assert(_internal != null);

            var curTokCnt = MtLlm.GetCurrentTokenCount(slotIndex);
            Debug.Assert(0 <= curTokCnt); // Must be initialized, here.
            bool isEmptyContext = curTokCnt < 1;
            Debug.Assert( // System prompt must be given on empty context, only.
                (isEmptyContext && !string.IsNullOrWhiteSpace(sysPrompt))
                    || (!isEmptyContext && sysPrompt == null));

            // We are working with a copy, to avoid modifying given list.
            List<Tuple<DateTime, string, List<long>>> msgsToImportToUse =
                [.. msgsToImport];
            List<string> txtToImport;
            bool retVal;

            if(isEmptyContext)
            {
                // Insert system prompt:
                msgsToImportToUse.Insert(
                    0,
                    new Tuple<DateTime, string, List<long>>(
                        // Use date/time of 1st msg. to import for system msg.
                        msgsToImportToUse.First().Item1,
                        sysPrompt!,
                        new List<long>()));
            }

            txtToImport = msgsToImportToUse.Select(m => m.Item2).ToList();

            if (useMemorySystem)
            {
                var firstLastMsgsIndex = _lastMsgs.Count;
                int i;

                // ******************************
                // *** Prepare last messages: ***
                // ******************************

                Debug.Assert(isEmptyContext == (firstLastMsgsIndex == 0));

                msgsToImportToUse.ForEach(
                    t =>
                    {
                        MessageType type;

                        if (_lastMsgs.Count == 0)
                        {
                            type = MessageType.System;
                        }
                        else
                        {
                            type = _lastMsgs.Count % 2 == 1
                                ? MessageType.User
                                : MessageType.Assistant;
                        }

                        _lastMsgs.Add(
                            new Message
                            {
                                Content = t.Item2,
                                MessageIndex = _lastMsgs.Count,
                                Type = type,
                                UtcCreatedAt = t.Item1,
                                InjectedStatementNrs = t.Item3
                            });
                    });

                // *************************************************************
                // *** Store last messages in database (and update objects): ***
                // *************************************************************

                _lastMsgs
                    .TakeLast(_lastMsgs.Count - firstLastMsgsIndex)
                    .ToList()
                    .ForEach(
                        m =>
                        {
                            m.Nr = ChatMemDb.InsertMsg(m, true);

                            Debug.Assert(0 < m.Nr);
                        });

                // *************************************************************
                // *** Augment to-be-imported texts:                         ***
                // *************************************************************

                i = 0;

                if (isEmptyContext)
                {
                    // 1st imported user message must be augmented with the
                    // summary of the last ("infinite") dialog:

                    ++i; // (message at index zero is the system prompt)

                    txtToImport[i] = Augment.GetAugmentedPrompt(
                        new AugmentParams
                        {
                            Prompt = txtToImport[i],
                            PromptDateTime =
                                msgsToImportToUse[i].Item1.ToLocalTime(),
                            LastMsgs = new List<Message>(), // Dummy
                            ConstraintsParagraph = false,
                            
                            // Always inject the memories on import.
                            MemoryParagraph = true,

                            MemoryParagraphFixedInjectStatementNrs =
                                msgsToImportToUse[i].Item3
                        },
                        out _);

                    i += 2;
                }

                // Augment all other user prompts.
                for (; i < txtToImport.Count; i += 2)
                {
                    txtToImport[i] = Augment.GetAugmentedPrompt(
                        new AugmentParams
                        {
                            Prompt = txtToImport[i],
                            PromptDateTime =
                                msgsToImportToUse[i].Item1.ToLocalTime(),
                            LastMsgs = // Stupid?
                                _lastMsgs.Take(firstLastMsgsIndex + i).ToList(),
                            ConstraintsParagraph = false,

                            // Always inject the memories on import.
                            MemoryParagraph = true,
                            
                            MemoryParagraphFixedInjectStatementNrs =
                                msgsToImportToUse[i].Item3
                        },
                        out _);
                }
            }

            if (_internal.OnSampledNonEogNonCtrl != null)
            {
                _internal.OnSampledNonEogNonCtrl(
                    new CallbackParams
                    {
                        Piece = C.D<string>("lang:waitForImportMsg"),
                        LastText = string.Empty,
                        ForceOutput = true,
                        ReqImmedUserResp = false
                    });
            }

            _isImportActive = true; // For the callback.
            retVal = MtLlm.Import(txtToImport, slotIndex, true);
            _isImportActive = false;
            return retVal;
        }

        /// <remarks>
        /// Must not be called, if memory system is disabled (because
        /// memorization will be used to create summary to-be-injected for
        /// "infinite" chat simulation).
        /// 
        /// Call this only, if adding to the context failed, because the context
        /// is full.
        /// 
        /// Assumes that a system prompt is used.
        /// </remarks>
        private static List<Tuple<DateTime, string, List<long>>>?
            PrepareForInfiniteChatAndGetToImport(ChatSingletonParams p)
        {
            Debug.Assert(p.UseMemorySystem);
            Debug.Assert(_internal != null);

            int maxTokCnt;
            int maxImpTokCnt;
            int tokCnt;
            int i;
            List<Message> impCandMsgs;
            List<Tuple<DateTime, string, List<long>>> impCandidates;
            List<Tuple<DateTime, string, List<long>>> toImport;
            ChatSingletonParams infP;

            if (_lastMsgs.Count < 3)
            {
                Debug.Assert(false); // Should not happen.
                Log.Line("Warning: \"Infinite chat\" not possible, because less than 3 former messages.");
                return null;
            }
            if (_lastMsgs[0].Type != MessageType.System)
            {
                Debug.Assert(false); // Should not happen.
                Log.Line("Warning: \"Infinite chat\" not possible, because 1st former msg. is no system prompt (not supported..).");
                return null;
            }
            Debug.Assert(_lastMsgs[1].Type == MessageType.User);
            Debug.Assert(_lastMsgs[2].Type == MessageType.Assistant);

            // At least a system prompt, a user prompt and 1 LLM response exist.
            // => This can be summarized.

            // We could theoretically (re-)use a maybe existing partial (LLM)
            // response later, but, for simplicity, we remove/ignore that
            // partial (LLM) response.
            // => Always using the current user prompt here, never the LLM
            //    response.

            if (!p.KeepLlmLoaded)
            {
                Debug.Assert(!MtLlm.IsInit(p.SlotIndex));
                MtLlm.ReInit(p.ParamsGiven, p.SlotIndex); // Return value ign.
            }

            maxTokCnt = MtLlm.GetMaxTokenCount(p.SlotIndex);

            // E.g.:
            //
            // Context length:                                          10000
            // Token count we want to have available after this (0.5x):  5000
            // Average increase factor of user prompt by injection:         2.0x
            //                                                               =>
            // Average increase factor of whole user/LLM msg. pairs
            // (under the assumption that the average length of user
            // an LLM msgs. is equal):                                      1.5x
            //                                                               =>
            // => 10000 / 2                                   = 5000
            //     5000 / 1.5                                 = 3333
            //     3333 - max-summary-word-count = 3333 - 200 = 3133
            //
            maxImpTokCnt = (int)((double)maxTokCnt / 2.0 / 1.5)
                - Extract.MaxWordsSummary;

            // Get all messages that might have to be imported back into context
            // after the to-be-created summary:
            impCandMsgs = _lastMsgs
                .Skip(3) // Skips system & 1st user & LLM msgs.
                .ToList();
            impCandidates = impCandMsgs
                .Select(
                    msg => new Tuple<DateTime, string, List<long>>(
                        msg.UtcCreatedAt,
                        msg.Content,
                        msg.InjectedStatementNrs))
                .ToList();

            // Add the user request that formerly couldn't be added to the
            // context or where the associated LLM response couldn't be added to
            // the context, because the context was full.
            impCandidates.Add(
                new(
                    DateTime.Now, // Does not really matter.
                    p.PromptGiven,
                    new()));

            // Iterate from the most recent user prompt, chronologically
            // backwards (to determine, how many of the last messages can be
            // imported and how many of the first messages must be summarized).
            tokCnt = 0;
            i = impCandidates.Count - 1;
            toImport = new List<Tuple<DateTime, string, List<long>>>();
            while (0 <= i)
            {
                string candStr;
                int candTokCnt;
                bool addPreceding = toImport.Count != 0;

                // Get the approximated token count for the current
                // request & response pair (or just for the current response, if
                // it is the last user prompt that has no response).
                candStr = impCandidates[i].Item2;
                if (addPreceding)
                {
                    // This is NOT the last candidate (which would be the last
                    // user prompt that has no LLM response, yet).
                    // <=>
                    // This is an LLM response.
                    // =>
                    // Add user request.

                    // Not 100% correct to just concatenate, but OK.
                    candStr = impCandidates[i - 1] + candStr;
                }
                // Not 100% correct, because it omits the "control" tokens.
                candTokCnt = MtLlm.GetTokenCount(candStr, false, p.SlotIndex);

                // Find out, if the current string can be imported.
                if (maxImpTokCnt < tokCnt + candTokCnt)
                {
                    break; // Cannot be imported, must be summarized.
                }
                // Current message or message pair can be imported.
                toImport.Insert(0, impCandidates[i]);
                if (addPreceding)
                {
                    // Also add the associated user prompt of the current pair.
                    toImport.Insert(0, impCandidates[i - 1]);
                    --i;
                }
                --i;
                tokCnt += candTokCnt;
            }
            if (toImport.Count == 0)
            {
                Debug.Assert(false); // Can theoretically happen.
                Log.Line("Warning: \"Infinite chat\" not possible, as last query alone is larger than max. token count to keep.");
                return null;
            }

            if (!p.KeepLlmLoaded)
            {
                Debug.Assert(MtLlm.IsInit(p.SlotIndex));
                MtLlm.DeInit(p.SlotIndex);
            }

            // =>
            // Messages in toImport variable:
            // - Do not include system prompt & 1st user & LLM msgs.
            // - Hold at least the last user query/prompt.
            // - All preceding included messages are pairs of user prompt & LLM
            //   response.
            // - Do not need that much more tokens than maximum import token
            //   count or even less.
            // - Do not include injected data, but to-be-injected data length
            //   was approximately taken into account, above.

            // ***********************************
            // *** It is "infinite chat" time! ***
            // ***********************************

            // 1) Remove last user prompt from lists and set prompt given:

            infP = new ChatSingletonParams(p);
            infP.PromptGiven = toImport.Last().Item2;
            Debug.Assert(impCandidates.Last().Item2 == infP.PromptGiven);
            impCandidates.RemoveAt(impCandidates.Count - 1);
            toImport.RemoveAt(toImport.Count - 1);

            // 2) Remove former messages NOT to be summarized from DB:

            impCandMsgs
                .Skip(impCandidates.Count - toImport.Count)
                .ToList()
                .ForEach(
                    msg =>
                    {
                        ChatMemDb.DeleteMsg(msg);
                        msg.Nr = 0;
                    });

            // 3) Create complete chat (with summary, etc.) from former messages
            //    TO BE summarized:

            if (_internal.OnSampledNonEogNonCtrl != null)
            {
                _internal.OnSampledNonEogNonCtrl(
                    new CallbackParams
                    {
                        Piece = C.D<string>("lang:waitForMemExtractMsg"),
                        LastText = string.Empty,
                        ForceOutput = true,
                        ReqImmedUserResp = false
                    });
            }
            {
                var internalBackup = _internal;

                _internal = null;

                // Will also take care of older messages w/o chat in DB.
                ChatMem.ChatMem.Update(false);

                _internal = internalBackup;
            }

            _lastMsgs.Clear();
            MtLlm.Reset(
                null, null, null, null, null, null, null, false, p.SlotIndex);
            return toImport;
        }

        /// <param name="msgsToImport">
        /// Messages to import (user request & assistant response pairs), must
        /// be an empty list, if the context is not empty.
        /// </param>
        /// <remarks>
        /// To be called from <see cref="QueryLlm"/>, only.
        /// </remarks>
        private static ChatSingletonResult QueryLlmInternal(
            ChatSingletonParams p,
            List<Tuple<DateTime, string, List<long>>> msgsToImport,
            out List<long> injectedStatementNrs)
        {
            // Not sure, if this is prevented by every caller..
            Debug.Assert(p.PromptGiven.Trim().Length != 0);

            Debug.Assert(_internal != null);

            MtLlmP llmP = new(p.ParamsGiven); // Deep-copies

            // .Trim() should not be necessary, here.
            var promptToUse = p.PromptGiven.Trim();
            bool isChat = p.SlotIndex == (int)SlotIndex.Chat;
            bool querySucceeded;

            // Although would not matter.
            Debug.Assert(p.ParamsGiven.callback == null);

            llmP.callback = Callback;

            injectedStatementNrs = new();

            Debug.Assert(
                !p.UseMemorySystem
                    || llmP.sys_prompt == ChatMem.ChatMem.GetSysPrompt());

            Debug.Assert( // Import expects a system prompt to exist.
                msgsToImport.Count == 0
                    || !string.IsNullOrWhiteSpace(llmP.sys_prompt));

            {
                bool doInit = true; // TRUE by default.

                do
                {
                    if (!MtLlm.IsInit(p.SlotIndex))
                    {
                        Log.Line($"Re-initializing, because slot {p.SlotIndex} is not initialized..");
                        break;
                    }

                    if(!p.KeepLlmLoaded)
                    {
                        Log.Line($"Re-initializing, because LLM in slot {p.SlotIndex} shall NOT be kept loaded..");
                        break;
                    }

                    // (in some cases, the configurations may not be equal, but
                    // mt_llm will be able to do a fast reset instead of a full
                    // reinitialization, anyway)
                    if(!llmP.IsEqual(MtLlm.GetParams(p.SlotIndex)!))
                    {
                        Log.Line($"Re-initializing, because parameters for slot {p.SlotIndex} are not equal..");
                        break;
                    }

                    doInit = false;
                } while (false);
                if(doInit)
                {
                    MtLlm.ReInit(llmP, p.SlotIndex); // Return value ignored.
                }
            }

            if(!p.KeepLlmLoaded)
            {
                // (Try to) restore last state from snapshot.
                MtLlm.SnapshotRestore(p.SlotIndex); // Return value ignored.
            }
            //
            // Otherwise: Last state is already preserved in context.

            // *****************************************************************
            // *** Import messages, if there are any to import:              ***
            // *****************************************************************

            if(msgsToImport.Count != 0)
            {
                if(!ImportIntoContext( // Does not work without a system prompt.
                        msgsToImport,
                        p.UseMemorySystem,
                        p.SlotIndex,
                        llmP.sys_prompt!))
                {
                    throw new Exception("Importing given messages into context failed!");
                }

                _internal.Last = new();
            }

            // *****************************************************************

            if (isChat // Probably not necessary.
                    && p.UseModules
                    && _activeModule.Msgs.Count != 0)
            {
                var curTokCnt = MtLlm.GetCurrentTokenCount(p.SlotIndex);
                List<Tuple<DateTime, string, List<long>>> modMsgsToImport;

                Debug.Assert(0 <= curTokCnt); // Must be initialized, here.

                // Add messages of user and module to the DB and to the context.

                // Must be pairs of user & module messages.
                Debug.Assert(_activeModule.Msgs.Count % 2 == 0);

                // Add module messages into the context:

                modMsgsToImport = _activeModule.Msgs
                    .Select(
                        m =>
                            new Tuple<DateTime, string, List<long>>(
                                // TODO: Store with _activeModule.Msgs and use here!
                                DateTime.UtcNow,
                                m,
                                new List<long>()))
                    .ToList();

                if (curTokCnt == 0)
                {
                    // We are assuming that an empty context is always large
                    // enough to hold all of a modules user interactions
                    // (which could be wrong..):
                    if(!ImportIntoContext(
                            modMsgsToImport,
                            p.UseMemorySystem,
                            p.SlotIndex,
                            llmP.sys_prompt!))
                    {
                        throw new Exception("Importing module messages into empty context failed!");
                    }
                }
                else
                {
                    if(!ImportIntoContext(
                            modMsgsToImport,
                            p.UseMemorySystem,
                            p.SlotIndex,
                            null))
                    {
                        if(!p.UseMemorySystem)
                        {
                            throw new Exception("Importing module messages into non-empty context failed (memory system is disabled)!");
                        }

                        // Assuming that the reason for failure is a full
                        // context.

                        var toImport = PrepareForInfiniteChatAndGetToImport(p);

                        if(toImport == null)
                        {
                            throw new Exception("Preparation for infinite chat after failed module message import also failed!");
                        }

                        if(!ImportIntoContext(
                                toImport,
                                p.UseMemorySystem,
                                p.SlotIndex,
                                llmP.sys_prompt))
                        {
                            throw new Exception("Import of infinite chat messages during module message import failed!");
                        }
                    }
                }

                _internal.Last = new();

                // Messages stored are no longer needed, must be removed.
                _activeModule.Msgs.Clear();
                _activeModule.MsgsFromBeforeCount = 0;
            }

            if(p.UseMemorySystem)
            {
                promptToUse = Augment.GetAugmentedPrompt(
                    new AugmentParams
                    {
                        Prompt = promptToUse,
                        PromptDateTime = DateTime.Now,
                        LastMsgs = _lastMsgs,
                        ConstraintsParagraph = true,
                        MemoryParagraph = _memoryParagraph,
                        MemoryParagraphFixedInjectStatementNrs = null
                    },
                    out injectedStatementNrs);
            }

            // *****************************************************************

            querySucceeded = MtLlm.Query(promptToUse, p.SlotIndex);

            //Log.Line($"Last response's word count: {Str.GetWordCount(_internal.Last.Response)}");

            if (!p.KeepLlmLoaded)
            {
                // Try to keep current state (before de-init., below).
                MtLlm.SnapshotUpdate(p.SlotIndex); // Return value ignored.

                // Immediately free memory allocated for LLM inference.
                MtLlm.DeInit(p.SlotIndex);
            }
            //
            // Otherwise: Current state (context) stays were it is.

            if (p.UseMemorySystem && querySucceeded)
            {
                UpdateLastMsgsAndWriteToDb(
                    p.PromptGiven, injectedStatementNrs, _internal.Last);
            }

            return new ChatSingletonResult
            {
                LastText = _internal.Last.Response,
                QuerySucceeded = querySucceeded
            };
        }

        /// <remarks>
        /// The chat-resume and "infinite"-chat (on context-full) features
        /// assume that there is always a system prompt (this is checked and
        /// handled, if not the case) and that the system prompt can be replaced
        /// with the "default" memory-related system prompt (not checked in any
        /// way). So the system prompt retrieved via
        /// <see cref="ChatMem.ChatMem.GetSysPrompt()"/> should never hold any
        /// dynamic/time-related data (e.g. no memories and no summary of the
        /// last chat).
        /// </remarks>
        private static ChatSingletonResult QueryLlm(ChatSingletonParams p)
        {
            Debug.Assert(_internal == null);

#if DEBUG
            ChatSingletonLast debugLast;
#endif //DEBUG
            List<long> injectedStatementNrs;
            ChatSingletonResult result;

            // Empty by default.
            List<Tuple<DateTime, string, List<long>>>? toImport = new();

            if (p.UseMemorySystem && _lastMsgs.Count == 0)
            {
                // Try to resume last chat from DB.

                List<Message> msgs;

                // Make sure that at most one session without a chat exists:
                ChatMem.ChatMem.Update(true); // <- Skips last session.

                msgs = ChatMemDb.LoadOrdMsgsWithoutChat();

                if(0 < msgs.Count)
                {
                    if (3 <= msgs.Count)
                    {
                        if (msgs[0].Type == MessageType.System)
                        {
                            Debug.Assert(msgs[1].Type == MessageType.User);
                            Debug.Assert(msgs[2].Type == MessageType.Assistant);

                            Debug.Assert(
                                msgs.Last().Type == MessageType.Assistant);

                            // Remove all messages of last session from DB:
                            msgs.ForEach(
                                msg =>
                                {
                                    ChatMemDb.DeleteMsg(msg);
                                    msg.Nr = 0;
                                });

                            // Remove the system prompt message.
                            msgs.RemoveAt(0);

                            // TODO: BUG: Assuming that context is large enough,
                            //            which is normally the case, but not,
                            //            if the context length was reduced
                            //            since last run (via config. file)!
                            toImport = msgs
                                .Select(
                                    msg =>
                                        new Tuple<DateTime, string, List<long>>(
                                            msg.UtcCreatedAt, 
                                            msg.Content,
                                            msg.InjectedStatementNrs))
                                .ToList();
                        }
                        else
                        {
                            Log.Line("Warning: Resuming session not possible, because 1st former msg. is no system prompt (not supported..).");
                        }
                    }
                    else
                    {
                        Log.Line("Warning: Resuming session not possible, because less than 3 former messages.");
                    }
                }
                //
                // Otherwise: All messages in DB belong to a chat (a "finished"
                //            session).
            }

            // Initialize static variable used in callback:
            _internal = new();
            _internal.OnSampledNonEogNonCtrl = p.OnSampledNonEogNonCtrl;
            _internal.WriteToConsole = p.WriteToConsole;

            result = QueryLlmInternal(
                p, toImport, out injectedStatementNrs);
#if DEBUG
            debugLast = _internal.Last;
#endif //DEBUG
            _internal.Last = new();
            toImport.Clear();

            if(!p.UseMemorySystem || result.QuerySucceeded)
            {
                // No "infinite" chat attempt, if memorization is deactivated
                // (because memorization will be used to create summary
                // to-be-injected for "infinite" chat simulation) or query
                // succeeded (we are assuming that the only cause for a failed
                // query is that the context is full).

                _internal = null; // De-inits. static var. used in callback.
                return result;
            }

            // Query did not succeed. Assuming that the cause is a full context.
#if DEBUG
            Debug.Assert(
                debugLast.Type == null // <- If full on first token.
                    || (debugLast.Type != null
                        && debugLast.Type.Value != MtLlmTokType.IRQ
                        && debugLast.Type.Value != MtLlmTokType.SAMPLED_EOG));
#endif //DEBUG

            Log.Line("Query failed (assuming cause is full context). Trying \"infinite chat\"..");

            toImport = PrepareForInfiniteChatAndGetToImport(p);
            if(toImport == null)
            {
                _internal = null; // De-inits. static var. used in callback.
                return result; // (called method logged)
            }
            Debug.Assert(0 < toImport.Count);

            // Import msgs. and run inference (a second time):
            result = QueryLlmInternal(p, toImport, out _);

            _internal = null; // De-initializes static var. used in callback.
            return result;
        }

        /// <remarks>
        /// Does nothing, if modules are disabled via configuration file.
        /// </remarks>
        private static void UpdateActiveModule(ChatSingletonParams p)
        {
            string question;
            List<Modules.Module> modulesToUse;
            string[] options;
            DigProbsStats digProbsStats;
            int maxIndex;

            if (!p.UseModules)
            {
                // Modules shall not be used. => This is an LLM request.
                Debug.Assert(
                    _activeModule.Current == null,
                    "Modules shall not be used, but some module is active! Deactivating..");
                _activeModule.Current = null;
                _activeModule.Msgs.Clear();
                _activeModule.MsgsFromBeforeCount = 0;
                return;
            }

            // Modules shall be used.

            if (p.OnSampledNonEogNonCtrl == null)
            {
                // Modules cannot be used. => Handle as an LLM request..
                Log.Line(
                    "Warning: Modules shall be used, but no callback given. Handling as LLM request..");
                Debug.Assert(
                    _activeModule.Current == null,
                    "Modules cannot be used, but some module is active!");
                _activeModule.Current = null;
                _activeModule.Msgs.Clear();
                _activeModule.MsgsFromBeforeCount = 0;
                return;
            }

            // Modules shall and can be used.

            if(_activeModule.Current != null)
            {
                return; // Keep active module active.
            }

            // => LLM mode is active.

            // Try to find out, which module to use and execute it:

            question = "What is requested by saying \"{0}\"?"; // Hard-coded

            modulesToUse = ActiveModule.Modules
                .Where(
                    m => !_activeModule.CalledByMistake.Exists(
                        cbm => m.IsSameType(cbm)))
                .ToList();

            if(modulesToUse.Count == 0)
            {
                _activeModule.Current = null; // Something else. => Use chat.
                return;
            }

            options = [
                .. modulesToUse.Select(m => m.GetIntention()),
                "Something else or this is no request at all." // Hard-coded
            ];

            // TODO: Implement dynamic support for more, later?
            Debug.Assert(options.Length <= SelQuery.MaxOptionsLen);

            // TODO: Implement a real check!
            Debug.Assert(options.Length == options.Distinct().Count());

            digProbsStats = Sel.Create(
                string.Format(question, p.PromptGiven),
                options,
                null,
                p.WriteToConsole,
                true)!; // <- De-initialize, if select slot was used.
            maxIndex = digProbsStats!.GetMaxIndex(1, options.Length);
            if (maxIndex == options.Length)
            {
                _activeModule.Current = null; // Something else. => Use chat.
                return; 
            }
            // A module was selected, activate it.
            _activeModule.Current =
                ActiveModule.Modules
                    .First(
                        // Kind of bad. Works, as long as the additional option
                        // is last in options string array.
                        m => m.IsSameType(modulesToUse[maxIndex - 1]))
                    .Create();
            
            _activeModule.MsgsFromBeforeCount = _activeModule.Msgs.Count;
            //
            // Don't do the following, here, in case of multiple module usages
            // in a row (so all their messages will be inserted into chat,
            // later):
            //_activeModule.Msgs.Clear();
            //_activeModule.MsgsFromBeforeCount = 0;
        }

        private static void ExecActiveModule(
            string request, Action<string, bool, bool> forwardResponse)
        {
            // Made sure of by caller.
            Debug.Assert(_activeModule.Current != null);

            Debug.Assert(request.Trim().Length != 0);

            bool isDone;
            bool calledByMistake;
            bool reqImmedUserResp;
            string response;

            response = _activeModule.Current.Exec(
                request, out calledByMistake, out isDone, out reqImmedUserResp);

            // TODO: Implement a real check (or send some default msg.?)!
            Debug.Assert(0 < response.Trim().Length);

            // Send output of module forward.
            forwardResponse(response, calledByMistake, reqImmedUserResp);
            
            if (isDone)
            {
                if (calledByMistake)
                {
                    // To be able to ignore this module as option when updating
                    // the module to use again.
                    Debug.Assert(
                        !_activeModule.CalledByMistake
                            .Any(m => m.IsSameType(_activeModule.Current)));
                    _activeModule.CalledByMistake.Add(_activeModule.Current);
                }
                else
                {
                    // No re-updating the active module to use necessary.
                    _activeModule.CalledByMistake.Clear();
                }

                // Deactivate module, it reached its done-state.
                _activeModule.Current = null; // => Back to LLM/chat mode.
            }
#if DEBUG
            else
            {
                // Module must be done, if called by mistake.
                Debug.Assert(!calledByMistake);
            }
#endif //DEBUG
        }

        /// <returns>
        /// true, if active module handled the query/request.
        /// false, if chat "mode" shall handle the query (called by mistake).
        /// </returns>
        /// <remarks>
        /// Assumes that <see cref="_activeModule.Current"/> is NOT null.
        /// </remarks>
        private static bool QueryActiveModule(ChatSingletonParams p)
        {
            // LLM/chat mode is not active, use active module.
            Debug.Assert(_activeModule.Current != null);

            bool moduleCalledByMistake = false;

            ExecActiveModule(
                p.PromptGiven,
                (response, calledByMistake, reqImmedUserResp) =>
                {
                    if (calledByMistake)
                    {
                        moduleCalledByMistake = true;
                    }
                    else
                    {
                        // To import into chat context, after module usage:
                        _activeModule.Msgs.Add(p.PromptGiven);
                        _activeModule.Msgs.Add(response);
                    }

                    p.OnSampledNonEogNonCtrl!(
                        new CallbackParams
                        {
                            Piece = response,
                            LastText = string.Empty,
                            ForceOutput = true,
                            ReqImmedUserResp = reqImmedUserResp
                        });
                });

            if (moduleCalledByMistake)
            {
                // Done by .ExecActiveModule() above:
                Debug.Assert(_activeModule.Current == null);

                p.PromptGiven =
                    _activeModule.Msgs[_activeModule.MsgsFromBeforeCount];
                _activeModule.Msgs.RemoveRange(
                    _activeModule.MsgsFromBeforeCount,
                    _activeModule.Msgs.Count
                        - _activeModule.MsgsFromBeforeCount);

                return false; // => Query LLM.
            }
            return true; // Active module handled the query/request.
        }

        // *********************************************************************
        // *** "Public" methods                                              ***
        // *********************************************************************

        internal static ChatSingletonResult Query(ChatSingletonParams p)
        {
            while (true)
            {
                UpdateActiveModule(p);

                if (_activeModule.Current == null)
                {
                    _activeModule.CalledByMistake.Clear();
                    return QueryLlm(p); // LLM/chat mode is active.
                }

                if (QueryActiveModule(p))
                {
                    return new ChatSingletonResult { QuerySucceeded = true };
                }

                // Module was called by mistake.
                Debug.Assert(0 < _activeModule.CalledByMistake.Count);

                // Queried module must always be done, here.
                Debug.Assert(_activeModule.Current == null);
            }
        }

        internal static void Cancel()
        {
            Log.Line("Setting cancel flag for LLM..");
            _cancelRequested = true; // Do this here, only!
        }

        internal static void ResetCancel()
        {
            Log.Line("Resetting cancel flag for LLM..");
            _cancelRequested = false; // Do this here, only!
        }
    }
}
