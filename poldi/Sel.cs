
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

// Marcel Timm, RhinoDevel, 2026jun27

using Poldi.CppInterface.MtLlm;
using System;
using System.Diagnostics;
using System.Linq;

namespace Poldi
{
    /// <summary>
    /// Singleton to be used as classifier via the "select" LLM configuration.
    /// </summary>
    /// <remarks>
    /// Assumes that the slot with index <see cref="_slotIndex"/> is only
    /// used by this singleton (no check, if parameters are equal).
    /// </remarks>
    internal static class Sel
    {
        /// <summary>
        /// If enabled, this singleton will use the chat LLM for select mode, if
        /// it is loaded and both configurations use the same model (file path).
        /// All other settings will not be compared / will be ignored(!).
        /// Before the select run, the chat LLM's context will be saved (in RAM)
        /// and after the run, it will be restored.
        /// </summary>
        private static readonly bool _useChatSlotIfPossible =
            C.D<bool>("llm:sel:useChatSlotIfPossible");

        private const int _slotIndex = (int)SlotIndex.Sel;

        // *********************************************************************

        private static readonly MtLlmP _p;

        private static readonly bool _useChatSlotIsPossible;
        private static readonly string? _chatSysPrompt;

        // *********************************************************************

        private static bool _restoreChatAfterSlotUse = true;

        private static bool _writeToConsole = false;
        private static float[]? _lastDigProbs = null;

        /// <remarks>
        /// Currently used for debugging, only.
        /// </remarks>
        private static string _lastResponse = string.Empty;

        // *********************************************************************

        static Sel()
        {
            _p = LlmPCreate.FromConfigSel(Callback);

            _useChatSlotIsPossible = false;
            _chatSysPrompt = null;
            if (_useChatSlotIfPossible)
            {
                var c = LlmPCreate.FromConfigChat(null);

                Debug.Assert(!string.IsNullOrWhiteSpace(_p.model_file_path));
                _useChatSlotIsPossible = 
                    c.model_file_path == _p.model_file_path;

                _chatSysPrompt = c.sys_prompt;
                Debug.Assert(!string.IsNullOrWhiteSpace(_chatSysPrompt));
            }
        }

        private static bool Callback(
            int tok, string piece, MtLlmTokType type, float[]? digProbs)
        {
            if (digProbs != null)
            {
                // Always expecting the digit probabilities of the first
                // inferred non-whitespace LLM response/answer token:
                Debug.Assert(
                    (_lastDigProbs == null
                        && _lastResponse.Trim().Length == 0
                        && type == MtLlmTokType.SAMPLED_NON_EOG_NON_CONTROL)
                    || (_lastDigProbs != null
                            && _lastDigProbs.SequenceEqual(digProbs)));

                if (_lastDigProbs == null) // This if-clause is not necessary.
                {
                    _lastDigProbs = digProbs;
                }
            }

            if (_writeToConsole)
            {
                Piece.ConsoleWrite(piece, type);
            }

            if(type == MtLlmTokType.SAMPLED_NON_EOG_NON_CONTROL)
            {
                _lastResponse += piece;
            }

            // Stop inference, if digit probabilites were already retrieved.
            return _lastDigProbs != null;
        }

        private static ChatSingleton.DigProbsStats? QueryLlmInternal(
            string prompt, bool writeToConsole, bool deInitOnDone)
        {
            Debug.Assert(prompt.Trim().Length != 0);

            // Although wouldn't matter (see below):
            Debug.Assert(_lastDigProbs == null);
            Debug.Assert(_lastResponse.Length == 0);
            Debug.Assert(_writeToConsole == false);

            ChatSingleton.DigProbsStats? retVal;

            int slotIndex = _slotIndex;
            Func<int, string, MtLlmTokType, float[]?, bool>? chatCallback =
                null;

            if (_useChatSlotIsPossible && MtLlm.IsInit((int)SlotIndex.Chat))
            {
                Log.Line("Using chat LLM for selection, too..");
                slotIndex = (int)SlotIndex.Chat;
                Log.Line("Making sure that select system prompt will be used..");
                // (also we always need to start fresh in select mode)
                chatCallback = MtLlm.GetParams(slotIndex)!.callback;
                if (_restoreChatAfterSlotUse)
                {
                    MtLlm.SnapshotUpdate(slotIndex);
                }
                MtLlm.Reset(
                    _p.sys_prompt,
                    null,
                    null,
                    null,
                    null,
                    null,
                    _p.callback,
                    true, // <- Set callback to null, if _p.callback is null.
                    slotIndex);
            }
            else
            {
                // The select slot shall be used.

                MtLlm.ReInit( // mt_llm will reset, only, if possible, anyway.
                    _p, slotIndex); // The system prompt is always the same.
            }

            // *****************************************************************
            // *** Initialize static variables used in callback:             ***
            // *****************************************************************

            _lastDigProbs = null;
            _lastResponse = string.Empty;
            _writeToConsole = writeToConsole;

            // *****************************************************************

            retVal = null;
            if(MtLlm.Query(prompt, slotIndex))
            {
                retVal = new ChatSingleton.DigProbsStats(_lastDigProbs!);
            }

            // *****************************************************************
            // *** De-initialize static variables used in callback:          ***
            // *****************************************************************

            _writeToConsole = false;
            _lastResponse = string.Empty;
            _lastDigProbs = null;

            // *****************************************************************

            if (slotIndex == (int)SlotIndex.Chat)
            {
                // The chat LLM slot was used.

                MtLlm.Reset(
                    _chatSysPrompt,
                    null,
                    null,
                    null,
                    null,
                    null,
                    chatCallback,
                    true, // <- Set callback to null, if chatCallback is null.
                    slotIndex);
                if (_restoreChatAfterSlotUse)
                {
                    MtLlm.SnapshotRestore(slotIndex);
                }
            }
            else
            {
                // The (default) select slot was used.

                if(deInitOnDone)
                {
                    // Immediately free memory allocated for LLM inference.
                    MtLlm.DeInit(slotIndex);
                }
            }

            // *****************************************************************

            return retVal;
        }

        // *********************************************************************
        // *** "Public" methods                                              ***
        // *********************************************************************

        /// <remarks>
        /// Useful, if keep-loaded is wanted/configured, but the memory needs to
        /// get freed for something else.
        /// 
        /// Just does nothing, if not initialized.
        /// </remarks>
        internal static void DeInit()
        {
            MtLlm.DeInit(_slotIndex);
        }

        internal static string GetModelFilePath() => _p.model_file_path!;

        internal static bool GetRestoreChatAfterSlotUse()
        {
            return _restoreChatAfterSlotUse;
        }
        internal static void SetRestoreChatAfterSlotUse(bool val)
        {
            _restoreChatAfterSlotUse = val;
        }
        internal static bool IsUsingChatSlotPossible()
        {
            return _useChatSlotIsPossible;
        }

        /// <summary>
        /// Configure the select-LLM stuff in the config. file and just use this
        /// method and don't care about anything else.
        /// </summary>
        /// <param name="deInitOnDone">
        /// If set to true and select slot was used (not the chat slot),
        /// <see cref="MtLlm.DeInit"/> will be called after the query got
        /// processed. If set to false, the caller has to take care of
        /// de-initalizing the <see cref="SlotIndex.Sel"/> slot later.
        /// </param>
        internal static ChatSingleton.DigProbsStats? Create(
            string prompt, bool writeToConsole, bool deInitOnDone)
        {
            return QueryLlmInternal(prompt, writeToConsole, deInitOnDone);
        }

        /// <summary>
        /// Configure the select-LLM stuff in the config. file and just use this
        /// method and don't care about anything else.
        /// </summary>
        /// <param name="deInitOnDone">
        /// If set to true and select slot was used (not the chat slot),
        /// <see cref="MtLlm.DeInit"/> will be called after the query got
        /// processed. If set to false, the caller has to take care of
        /// de-initalizing the <see cref="SlotIndex.Sel"/> slot later.
        /// </param>
        internal static ChatSingleton.DigProbsStats? Create(
            string question,
            string[] options,
            string? explanation,
            bool writeToConsole,
            bool deInitOnDone)
        {
            return Create(
                SelQuery.GetSelPrompt(question, options, explanation),
                writeToConsole,
                deInitOnDone);
        }
    }
}
