
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

// Marcel Timm, RhinoDevel, 2026jul08

using Poldi.CppInterface.MtLlm;
using System;
using System.Diagnostics;

namespace Poldi
{
    /// <summary>
    /// Singleton to be used to query the "memory" LLM configured in different
    /// ways to extract memory statements from former chats, etc.
    /// </summary>
    /// <remarks>
    /// Assumes that the slot with index <see cref="_slotIndex"/> is only
    /// used by this singleton (no check, if parameters are equal).
    /// 
    /// Will de-initialize <see cref="Emb"/>, <see cref="Rer"/> and
    /// <see cref="Sel"/> slots to save memory (e.g. VRAM) before execution.
    /// </remarks>
    internal static class Mem
    {
        /// <summary>
        /// If enabled, this singleton will use the chat LLM for memory mode, if
        /// it is loaded and both configurations use the same model (file path).
        /// All other settings will not be compared / will be ignored(!).
        /// Before the memory mode run, the chat LLM's context will be saved (in
        /// RAM) and after the run, it will be restored.
        /// </summary>
        private static readonly bool _useChatSlotIfPossible =
            C.D<bool>("llm:mem:useChatSlotIfPossible");

        private const int _slotIndex = (int)SlotIndex.Mem;

        // *********************************************************************

        private static readonly MtLlmP _p;

        private static readonly bool _useChatSlotIsPossible;
        private static readonly string? _chatSysPrompt;
        private static int? _chatTopK;
        private static float? _chatTopP;
        private static float? _chatMinP;
        private static float? _chatTemp;

        // *********************************************************************

        private static bool _restoreChatAfterSlotUse = true;
        private static bool _writeToConsole = false;
        private static string _lastResponse = string.Empty;

        // *********************************************************************

        static Mem()
        {
            _p = LlmPCreate.FromConfigMem(Callback);

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
                _chatTopK = c.top_k;
                _chatTopP = c.top_p;
                _chatMinP = c.min_p;
                _chatTemp = c.temp;
            }
        }

        private static bool Callback(
            int tok, string piece, MtLlmTokType type, float[]? digProbs)
        {
            if (_writeToConsole)
            {
                Piece.ConsoleWrite(piece, type);
            }

            if(type == MtLlmTokType.SAMPLED_NON_EOG_NON_CONTROL)
            {
                _lastResponse += piece;
            }

            return false; // Never stop inference, here.
        }

        private static void LlmInit(
            string sysPrompt,
            out int slotIndex,
            out Func<int, string, MtLlmTokType, float[]?, bool>? chatCallback)
        {
            MtLlmP pWithSysPrompt;

            if (_useChatSlotIsPossible && MtLlm.IsInit((int)SlotIndex.Chat))
            {
                //Log.Line("Using chat LLM for memory mode, too..");
                slotIndex = (int)SlotIndex.Chat;
                //Log.Line("Making sure that memory mode sampling parameters will be used (e.g. ignoring context size)..");
                // (also we always need to start fresh in memory mode)
                chatCallback = MtLlm.GetParams(slotIndex)!.callback;
                if (_restoreChatAfterSlotUse)
                {
                    MtLlm.SnapshotUpdate(slotIndex);
                }
                MtLlm.Reset(
                    sysPrompt,
                    _p.top_k,
                    _p.top_p,
                    _p.min_p,
                    _p.temp,
                    null, // Grammar is not used in memory & chat modes.
                    _p.callback,
                    true, // <- Set callback to null, if _p.callback is null.
                    slotIndex);
                return;
            }

            // The memory mode slot shall be used.

            slotIndex = _slotIndex;
            chatCallback = null;

            pWithSysPrompt = new MtLlmP(_p);
            pWithSysPrompt.sys_prompt = sysPrompt;

            // Free some memory:
            Sel.DeInit();
            Rer.DeInit();
            Emb.DeInit();

            // mt_llm will reset, only, if possible, anyway.
            MtLlm.ReInit(pWithSysPrompt, slotIndex);
        }
        private static void LlmDeInit(
            int slotIndex,
            bool deInitOnDone,
            Func<int, string, MtLlmTokType, float[]?, bool>? chatCallback)
        {
            if (slotIndex == (int)SlotIndex.Chat)
            {
                // The chat LLM slot was used.

                MtLlm.Reset(
                    _chatSysPrompt,
                    _chatTopK,
                    _chatTopP,
                    _chatMinP,
                    _chatTemp,
                    null, // Grammar is not used in chat & memory modes.
                    chatCallback,
                    true, // Set callback to null, if chatCallback is null.
                    slotIndex);
                if (_restoreChatAfterSlotUse)
                {
                    MtLlm.SnapshotRestore(slotIndex);
                }
                return;
            }

            // The (default) memory mode slot was used.

            if (deInitOnDone)
            {
                // Immediately free memory allocated for LLM inference.
                MtLlm.DeInit(slotIndex);
            }
        }

        private static Tuple<string, int>? QueryLlmInternal(
            string sysPrompt,
            string prompt,
            bool writeToConsole,
            bool deInitOnDone)
        {
            Debug.Assert(sysPrompt.Trim().Length != 0);
            Debug.Assert(prompt.Trim().Length != 0);

            // Although wouldn't matter (see below):
            Debug.Assert(_lastResponse.Length == 0);
            Debug.Assert(_writeToConsole == false);

            Tuple<string, int>? retVal;
            int slotIndex;
            Func<int, string, MtLlmTokType, float[]?, bool>? chatCallback;

            // *****************************************************************

            LlmInit(sysPrompt, out slotIndex, out chatCallback);

            // *****************************************************************
            // *** Initialize static variables used in callback:             ***
            // *****************************************************************

            _lastResponse = string.Empty;
            _writeToConsole = writeToConsole;

            // *****************************************************************

            retVal = null;
            if(MtLlm.Query(prompt, slotIndex))
            {
                retVal = new Tuple<string, int>(
                    _lastResponse,
                    MtLlm.GetTokenCount(_lastResponse, true, slotIndex));
            }

            // *****************************************************************
            // *** De-initialize static variables used in callback:          ***
            // *****************************************************************

            _writeToConsole = false;
            _lastResponse = string.Empty;

            // *****************************************************************

            LlmDeInit(slotIndex, deInitOnDone, chatCallback);

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

        /// <param name="deInitOnDone">
        /// If set to true and memory slot was used (not the chat slot),
        /// <see cref="MtLlm.DeInit"/> will be called after the query got
        /// processed. If set to false, the caller has to take care of
        /// de-initalizing the <see cref="SlotIndex.Mem"/> slot later.
        /// </param>
        internal static int GetTokenCount(
            string text, bool addSpecial, bool deInitOnDone)
        {
            int retVal;
            int slotIndex;
            Func<int, string, MtLlmTokType, float[]?, bool>? chatCallback;

            LlmInit(
                "Dummy system prompt.", // Might be OK, if empty.
                out slotIndex,
                out chatCallback);

            retVal = MtLlm.GetTokenCount(text, addSpecial, slotIndex);

            LlmDeInit(slotIndex, deInitOnDone, chatCallback);

            return retVal;
        }

        /// <summary>
        /// Configure the memory mode - LLM stuff in the config. file and just
        /// use this method and don't care about anything else.
        /// </summary>
        /// <param name="deInitOnDone">
        /// If set to true and memory slot was used (not the chat slot),
        /// <see cref="MtLlm.DeInit"/> will be called after the query got
        /// processed. If set to false, the caller has to take care of
        /// de-initalizing the <see cref="SlotIndex.Mem"/> slot later.
        /// </param>
        internal static Tuple<string, int>? Create(
            string sysPrompt,
            string prompt,
            bool writeToConsole,
            bool deInitOnDone)
        {
            return QueryLlmInternal(
                sysPrompt, prompt, writeToConsole, deInitOnDone);
        }
    }
}
