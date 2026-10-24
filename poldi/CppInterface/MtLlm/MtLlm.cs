
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

// Marcel Timm, RhinoDevel, 2024oct05

using System.Runtime.InteropServices;
using System;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;

namespace Poldi.CppInterface.MtLlm
{
    internal static class MtLlm
    {
#if LINUX
        private const string _dllPath =
           "../../../mt_llm/mt_llm/libmtllm.so";
#else //LINUX
    #if DEBUG
        private const string _dllPath =
            "..\\..\\..\\mt_llm\\x64\\Debug\\mt_llm.dll";
    #else //DEBUG
        private const string _dllPath =
            "..\\..\\..\\mt_llm\\x64\\Release\\mt_llm.dll";
    #endif //DEBUG
#endif //LINUX

        private class Slot
        {
            internal MtLlmP? POuter { get; set; } = null;
        }
        private static Dictionary<int, Slot> _indexToSlots = new();

        /// <remarks>
        /// Set/unset by <see cref="Query"/> and <see cref="DecodeResponse"/>.
        /// </remarks>
        private static int _activeQuerySlotIndex = -1;

        private static Slot GetSlot(int slotIndex)
        {
            if (!_indexToSlots.ContainsKey(slotIndex))
            {
                Debug.Assert(false); // Must not get here.
                throw new NotSupportedException("Invalid slot index given!");
            }
            return _indexToSlots[slotIndex];
        }

        internal static bool IsInit(int slotIndex)
        {
            return _indexToSlots.ContainsKey(slotIndex);
        }

        private static readonly CallbackDelegate _callbackDelegate =
            new CallbackDelegate(Callback);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool CallbackDelegate(
            int tok, IntPtr piece, int type, IntPtr dig_probs);

        [DllImport(
            _dllPath,
            EntryPoint = "mt_llm_snapshot_clear",
            CallingConvention = CallingConvention.StdCall)]
        private static extern void MtLlmSnapshotClear(int slot_index);

        [DllImport(
            _dllPath,
            EntryPoint = "mt_llm_snapshot_update",
            CallingConvention = CallingConvention.StdCall)]
        private static extern bool MtLlmSnapshotUpdate(int slot_index);

        [DllImport(
            _dllPath,
            EntryPoint = "mt_llm_snapshot_restore",
            CallingConvention = CallingConvention.StdCall)]
        private static extern bool MtLlmSnapshotRestore(int slot_index);

        [DllImport(
            _dllPath,
            EntryPoint = "mt_llm_snapshot_to_file",
            CallingConvention = CallingConvention.StdCall)]
        private static extern bool MtLlmSnapshotToFile(
            int slot_index, IntPtr abs_file_path);

        [DllImport(
            _dllPath,
            EntryPoint = "mt_llm_snapshot_from_file",
            CallingConvention = CallingConvention.StdCall)]
        private static extern bool MtLlmSnapshotFromFile(
            IntPtr abs_file_path, int slot_index);

        [DllImport(
            _dllPath,
            EntryPoint = "mt_llm_free",
            CallingConvention = CallingConvention.StdCall)]
        private static extern void MtLlmFree(IntPtr ptr);

        [DllImport(
            _dllPath,
            EntryPoint = "mt_llm_get_max_token_count",
            CallingConvention = CallingConvention.StdCall)]
        private static extern int MtLlmGetMaxTokenCount(int slot_index);

        [DllImport(
            _dllPath,
            EntryPoint = "mt_llm_get_current_token_count",
            CallingConvention = CallingConvention.StdCall)]
        private static extern int MtLlmGetCurrentTokenCount(int slot_index);

        [DllImport(
            _dllPath,
            EntryPoint = "mt_llm_get_token_count",
            CallingConvention = CallingConvention.StdCall)]
        private static extern int MtLlmGetTokenCount(
            IntPtr prompt, bool add_special, int slot_index);

        [DllImport(
            _dllPath,
            EntryPoint = "mt_llm_import",
            CallingConvention = CallingConvention.StdCall)]
        private static extern bool MtLlmImport(
            IntPtr messages,
            int msg_cnt,
            int slot_index,
            bool no_last_prompt_beg_delim);

        [DllImport(
            _dllPath,
            EntryPoint = "mt_llm_query",
            CallingConvention = CallingConvention.StdCall)]
        private static extern bool MtLlmQuery(
            IntPtr prompt,
            int slot_index,
            bool skip_sys_prompt_end_delim_and_inference,
            bool follow_up_decode_prompt_and_sys_prompt_end_delim);

        [DllImport(
            _dllPath,
            EntryPoint = "mt_llm_create_embeddings",
            CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr MtLlmCreateEmbeddings(
            IntPtr prompt, int slot_index, ref int out_count);

        [DllImport(
            _dllPath,
            EntryPoint = "mt_llm_rerank",
            CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr MtLlmRerank(
            IntPtr query, IntPtr documents, int doc_count, int slot_index);

        [DllImport(
            _dllPath,
            EntryPoint = "mt_llm_reset",
            CallingConvention = CallingConvention.StdCall)]
        private static extern void MtLlmReset(
            IntPtr sys_prompt,
            IntPtr top_k,
            IntPtr top_p,
            IntPtr min_p,
            IntPtr temp,
            IntPtr grammar,
            int slot_index);
        
        [DllImport(
            _dllPath,
            EntryPoint = "mt_llm_deinit",
            CallingConvention = CallingConvention.StdCall)]
        private static extern void MtLlmDeInit(int slot_index);

        [DllImport(
            _dllPath,
            EntryPoint = "mt_llm_reinit",
            CallingConvention = CallingConvention.StdCall)]
        private static extern bool MtLlmReInit(IntPtr mt_p, int slot_index);

        /// <summary>
        /// Create a null-terminated, UTF-8 encoded "C" string version of the
        /// given string <paramref name="str"/> in unmanaged memory.
        /// </summary>
        /// <remarks>
        /// The return value must be freed via
        /// <see cref="Marshal.FreeHGlobal"/>.
        /// </remarks>
        private static IntPtr CreateUtf8Pointer(string str)
        {
            var utf8Bytes = Encoding.UTF8.GetBytes(str);
            var retVal = Marshal.AllocHGlobal(
                    utf8Bytes.Length + 1); // +1 = '\0'.

            // Copy byte array to unmanaged memory:
            //
            Marshal.Copy(utf8Bytes, 0, retVal, utf8Bytes.Length);

            // Null-terminate the string:
            //
            Marshal.WriteByte(retVal + utf8Bytes.Length, (byte)'\0');

            return retVal;
        }

        /// <remarks>
        /// Also works with empty string. Returns null, if null given.
        /// </remarks>
        private static byte[]? CreateAsUtf8(string? str, int maxLenWithZero)
        {
            byte[] arr, buf;

            if (str == null)
            {
                return null;
            }

            buf = Encoding.UTF8.GetBytes(str);
            if (buf.Length >= maxLenWithZero) // (zero terminator to be added)
            {
                return null;
            }

            arr = new byte[maxLenWithZero];
            for (int i = 0; i < buf.Length; ++i)
            {
                arr[i] = buf[i];
            }
            arr[buf.Length] = (byte)'\0';
            return arr;
        }

        private static bool Callback(
            int tok, IntPtr piece, int type, IntPtr dig_probs)
        {
            Debug.Assert(Enum.IsDefined(typeof(MtLlmTokType), type));

            Debug.Assert(_indexToSlots.ContainsKey(_activeQuerySlotIndex));

            var slot = GetSlot(_activeQuerySlotIndex);

            Debug.Assert(slot.POuter != null);
            Debug.Assert(slot.POuter.callback != null);

            var str = Marshal.PtrToStringUTF8(piece);

            // Hard-coded length (although there are always 10 digits..).
            float[]? digProbs = null;

            Debug.Assert(str != null);

            if(dig_probs != IntPtr.Zero)
            {
                digProbs = new float[10];

                Marshal.Copy(dig_probs, digProbs, 0, digProbs.Length);
            }
            //
            // Otherwise: No digit probabilities given.

            return slot.POuter.callback(tok, str, (MtLlmTokType)type, digProbs);
        }

        private static mt_llm_p? CreateParams(MtLlmP p)
        {
            mt_llm_p ret_val;
            byte[]? buf;

            ret_val.n_gpu_layers = p.n_gpu_layers;

            ret_val.n_ctx = p.n_ctx;
            ret_val.n_batch = p.n_batch;
            ret_val.n_ubatch = p.n_ubatch;
            ret_val.threads = p.threads;
            ret_val.type_kv_q8_0 = (byte)(p.type_kv_q8_0 ? 1 : 0); ;
            ret_val.emb_or_rerank = p.emb_or_rerank;

            ret_val.seed = p.seed;
            ret_val.top_k = p.top_k;
            ret_val.top_p = p.top_p;
            ret_val.min_p = p.min_p;
            ret_val.temp = p.temp;

            buf = CreateAsUtf8(p.grammar, MtLlmPLen.MT_LLM_P_LEN_GRAMMAR);
            if (buf == null)
            {
                return null;
            }
            ret_val.grammar = buf;

            ret_val.cpu_moe = (byte)(p.cpu_moe ? 1 : 0);
            ret_val.n_cpu_moe = p.n_cpu_moe;

            buf = CreateAsUtf8(
                    p.model_file_path,
                    MtLlmPLen.MT_LLM_P_LEN_MODEL_FILE_PATH);
            if (buf == null)
            {
                return null;
            }
            ret_val.model_file_path = buf;

            buf = CreateAsUtf8(
                    p.sys_prompt, MtLlmPLen.MT_LLM_P_LEN_SYS_PROMPT);
            if (buf == null)
            {
                return null;
            }
            ret_val.sys_prompt = buf;

            buf = CreateAsUtf8(
                    p.prompt_beg_delim,
                    MtLlmPLen.MT_LLM_P_LEN_PROMPT_BEG_DELIM);
            if (buf == null)
            {
                return null;
            }
            ret_val.prompt_beg_delim = buf;

            buf = CreateAsUtf8(
                    p.prompt_end_delim,
                    MtLlmPLen.MT_LLM_P_LEN_PROMPT_END_DELIM);
            if (buf == null)
            {
                return null;
            }
            ret_val.prompt_end_delim = buf;

            buf = CreateAsUtf8(
                    p.sys_prompt_beg_delim,
                    MtLlmPLen.MT_LLM_P_LEN_SYS_PROMPT_BEG_DELIM);
            if (buf == null)
            {
                return null;
            }
            ret_val.sys_prompt_beg_delim = buf;

            buf = CreateAsUtf8(
                    p.sys_prompt_mid_delim,
                    MtLlmPLen.MT_LLM_P_LEN_SYS_PROMPT_MID_DELIM);
            if (buf == null)
            {
                return null;
            }
            ret_val.sys_prompt_mid_delim = buf;

            buf = CreateAsUtf8(
                    p.sys_prompt_end_delim,
                    MtLlmPLen.MT_LLM_P_LEN_SYS_PROMPT_END_DELIM);
            if (buf == null)
            {
                return null;
            }
            ret_val.sys_prompt_end_delim = buf;

            buf = CreateAsUtf8(
                    p.think_beg_delim, MtLlmPLen.MT_LLM_P_LEN_THINK_BEG_DELIM);
            if (buf == null)
            {
                return null;
            }
            ret_val.think_beg_delim = buf;

            buf = CreateAsUtf8(
                    p.think_end_delim, MtLlmPLen.MT_LLM_P_LEN_THINK_END_DELIM);
            if (buf == null)
            {
                return null;
            }
            ret_val.think_end_delim = buf;

            ret_val.try_prompts_by_model =
                (byte)(p.try_prompts_by_model ? 1 : 0);
            ret_val.enable_thinking = (byte)(p.enable_thinking ? 1 : 0);
            ret_val.enable_log = (byte)(p.enable_log ? 1 : 0);
            ret_val.enable_llama_cpp_log =
                (byte)(p.enable_llama_cpp_log ? 1 : 0);

            ret_val.callback = Marshal.GetFunctionPointerForDelegate(
                _callbackDelegate);

            return ret_val;
        }

        internal static void SnapshotClear(int slotIndex)
        {
            MtLlmSnapshotClear(slotIndex);
        }

        internal static bool SnapshotUpdate(int slotIndex)
        {
            return MtLlmSnapshotUpdate(slotIndex);
        }

        internal static bool SnapshotRestore(int slotIndex)
        {
            return MtLlmSnapshotRestore(slotIndex);
        }

        internal static bool SnapshotToFile(int slotIndex, string absFilePath)
        {
            bool buf;
            byte[] utf8Bytes;
            IntPtr ptr;

            if (!_indexToSlots.ContainsKey(slotIndex))
            {
                Debug.Assert(GetSlot(slotIndex).POuter == null);
                return false; // Does not seem to be initialized.
            }
            Debug.Assert(GetSlot(slotIndex).POuter != null);

            utf8Bytes = Encoding.UTF8.GetBytes(absFilePath);
            ptr = Marshal.AllocHGlobal(utf8Bytes.Length + 1); // Zero term.!

            // Copy byte array to unmanaged RAM:
            Marshal.Copy(utf8Bytes, 0, ptr, utf8Bytes.Length);

            // Add zero terminator string:
            Marshal.WriteByte(ptr + utf8Bytes.Length, (byte)'\0');

            buf = MtLlmSnapshotToFile(slotIndex, ptr);

            Marshal.FreeHGlobal(ptr);

            return buf;
        }

        internal static bool SnapshotFromFile(string absFilePath, int slotIndex)
        {
            bool buf;
            byte[] utf8Bytes;
            IntPtr ptr;

            if (!_indexToSlots.ContainsKey(slotIndex))
            {
                return false; // Does not seem to be initialized.
            }
            Debug.Assert(GetSlot(slotIndex).POuter != null);

            utf8Bytes = Encoding.UTF8.GetBytes(absFilePath);
            ptr = Marshal.AllocHGlobal(utf8Bytes.Length + 1); // Zero term.!

            // Copy byte array to unmanaged RAM:
            Marshal.Copy(utf8Bytes, 0, ptr, utf8Bytes.Length);

            // Add zero terminator string:
            Marshal.WriteByte(ptr + utf8Bytes.Length, (byte)'\0');

            buf = MtLlmSnapshotFromFile(ptr, slotIndex);

            Marshal.FreeHGlobal(ptr);

            return buf;
        }

        /// <param name="setCallbackToNullIfNullGiven">
        /// If true and <paramref name="callback"/> is null, set the callback to
        /// null instead of doing nothing.
        /// </param>
        /// <remarks>
        /// Just does nothing, if the slot is not initialized, yet.
        /// </remarks>
        internal static void Reset(
            string? sysPrompt,
            int? topK,
            float? topP,
            float? minP,
            float? temp,
            string? grammar,
            Func<int, string, MtLlmTokType, float[]?, bool>? callback,
            bool setCallbackToNullIfNullGiven,
            int slotIndex)
        {
            var sysPromptStr = IntPtr.Zero;
            var topKPtr = IntPtr.Zero;
            var topPPtr = IntPtr.Zero;
            var minPPtr = IntPtr.Zero;
            var tempPtr = IntPtr.Zero;
            var grammarStr = IntPtr.Zero;
            
            if(!IsInit(slotIndex))
            {
                Log.Line($"Warning: Slot with index {slotIndex} is not initialized. Doing nothing..");
                return;
            }

            if(sysPrompt != null
                && sysPrompt.Trim().Length != 0) // <- TODO: Necessary/wanted?
            {
                sysPromptStr = CreateUtf8Pointer(sysPrompt);

                _indexToSlots[slotIndex].POuter!.sys_prompt = sysPrompt;
            }
            if(topK != null)
            {
                topKPtr = Marshal.AllocHGlobal(sizeof(int));
                Marshal.StructureToPtr(topK.Value, topKPtr, false);

                _indexToSlots[slotIndex].POuter!.top_k = topK.Value;
            }
            if (topP != null)
            {
                topPPtr = Marshal.AllocHGlobal(sizeof(float));
                Marshal.StructureToPtr(topP.Value, topPPtr, false);

                _indexToSlots[slotIndex].POuter!.top_p = topP.Value;
            }
            if (minP != null)
            {
                minPPtr = Marshal.AllocHGlobal(sizeof(float));
                Marshal.StructureToPtr(minP.Value, minPPtr, false);

                _indexToSlots[slotIndex].POuter!.min_p = minP.Value;
            }
            if (temp != null)
            {
                tempPtr = Marshal.AllocHGlobal(sizeof(float));
                Marshal.StructureToPtr(temp.Value, tempPtr, false);

                _indexToSlots[slotIndex].POuter!.temp = temp.Value;
            }
            if (grammar != null
                    && grammar.Trim().Length != 0) // <- TODO: Necessary/wanted?
            {
                grammarStr = CreateUtf8Pointer(grammar);

                _indexToSlots[slotIndex].POuter!.grammar = grammar;
            }

            if (callback != null || setCallbackToNullIfNullGiven)
            {
                _indexToSlots[slotIndex].POuter!.callback = callback;
            }

            MtLlmReset(
                sysPromptStr,
                topKPtr,
                topPPtr,
                minPPtr,
                tempPtr,
                grammarStr,
                slotIndex);

            if (grammarStr != IntPtr.Zero) // If-clause probably unnecessary..
            {
                Marshal.FreeHGlobal(grammarStr);
            }
            if (tempPtr != IntPtr.Zero) // If-clause probably unnecessary..
            {
                Marshal.FreeHGlobal(tempPtr);
            }
            if (minPPtr != IntPtr.Zero) // If-clause probably unnecessary..
            {
                Marshal.FreeHGlobal(minPPtr);
            }
            if (topPPtr != IntPtr.Zero) // If-clause probably unnecessary..
            {
                Marshal.FreeHGlobal(topPPtr);
            }
            if (topKPtr != IntPtr.Zero) // If-clause probably unnecessary..
            {
                Marshal.FreeHGlobal(topKPtr);
            }
            if (sysPromptStr != IntPtr.Zero) // If-clause probably unnecessary..
            {
                Marshal.FreeHGlobal(sysPromptStr);
            }
        }

        /// <remarks>
        /// Does NOT remove the snapshot (eventually kept in memory).
        /// </remarks>
        internal static void DeInit(int slotIndex)
        {
            MtLlmDeInit(slotIndex); // OK, if not initialized.
            _indexToSlots.Remove(slotIndex);
        }

        internal static int GetMaxTokenCount(int slotIndex)
        {
            if (!_indexToSlots.ContainsKey(slotIndex))
            {
                return -3; // Does not seem to be initialized.
            }
            Debug.Assert(GetSlot(slotIndex).POuter != null);

            return MtLlmGetMaxTokenCount(slotIndex);
        }

        internal static int GetCurrentTokenCount(int slotIndex)
        {
            if (!_indexToSlots.ContainsKey(slotIndex))
            {
                return -3; // Does not seem to be initialized.
            }
            Debug.Assert(GetSlot(slotIndex).POuter != null);

            return MtLlmGetCurrentTokenCount(slotIndex);
        }

        internal static int GetTokenCount(
            string text, bool addSpecial, int slotIndex)
        {
            byte[] utf8Bytes;
            IntPtr ptr;
            int buf;

            if (!_indexToSlots.ContainsKey(slotIndex))
            {
                return -4; // Does not seem to be initialized.
            }
            Debug.Assert(GetSlot(slotIndex).POuter != null);

            utf8Bytes = Encoding.UTF8.GetBytes(text.Trim()); // Trims
            ptr = Marshal.AllocHGlobal(utf8Bytes.Length + 1); // Zero term.!

            // Copy byte array to unmanaged RAM:
            Marshal.Copy(utf8Bytes, 0, ptr, utf8Bytes.Length);

            // Add zero terminator string:
            Marshal.WriteByte(ptr + utf8Bytes.Length, (byte)'\0');

            buf = MtLlmGetTokenCount(ptr, addSpecial, slotIndex);

            Marshal.FreeHGlobal(ptr);

            return buf;
        }

        /// <remarks>
        /// Does not support any given message being an empty string.
        /// 
        /// A lot of copying here, there might be a performance-improved way to
        /// get the data to the library..
        /// </remarks>
        internal static bool Import(
            List<string> messages, int slotIndex, bool noLastPromptBegDelim)
        {
            bool succeeded;
            IntPtr messagesPtr;
            List<IntPtr> messagePtrs;

            if (messages.Count == 0)
            {
                Debug.Assert(false); // Must not happen.
                return false;
            }
            if (messages.Any(msg => msg.Length == 0)) // Maybe not necessary.
            {
                Debug.Assert(false); // Must not happen.
                return false;
            }
            if (!_indexToSlots.ContainsKey(slotIndex))
            {
                Debug.Assert(false); // Must not happen.
                return false; // Does not seem to be initialized.
            }
            Debug.Assert(GetSlot(slotIndex).POuter != null);

            Debug.Assert(_activeQuerySlotIndex == -1);

            messagePtrs = new();
            messagesPtr = Marshal.AllocHGlobal(messages.Count * IntPtr.Size);
            for (int i = 0; i < messages.Count; ++i)
            {
                var msgUtf8Bytes = Encoding.UTF8.GetBytes(messages[i]);
                var msgPtr = Marshal.AllocHGlobal(
                        msgUtf8Bytes.Length + 1); // Zero term.!

                // Copy byte array to unmanaged RAM:
                Marshal.Copy(msgUtf8Bytes, 0, msgPtr, msgUtf8Bytes.Length);

                // Add zero terminator string:
                Marshal.WriteByte(msgPtr + msgUtf8Bytes.Length, (byte)'\0');

                // Remember the pointer:
                messagePtrs.Add(msgPtr);

                // Copy to C array:
                Marshal.WriteIntPtr(messagesPtr, i * IntPtr.Size, msgPtr);
            }

            _activeQuerySlotIndex = slotIndex; // For the callback.
            succeeded = MtLlmImport(
                messagesPtr,
                messagePtrs.Count,
                slotIndex,
                noLastPromptBegDelim);
            _activeQuerySlotIndex = -1;

            for (int i = 0; i < messagePtrs.Count; ++i)
            {
                Marshal.FreeHGlobal(messagePtrs[i]);
            }
            Marshal.FreeHGlobal(messagesPtr);
            messagesPtr = IntPtr.Zero;

            return succeeded;
        }

        /// <remarks>
        /// Will (indirectly) trigger calls of <see cref="Callback"></see> via
        /// DLL.
        /// </remarks>
        internal static bool Query(string prompt, int slotIndex)
        {
            bool buf;
            byte[] utf8Bytes;
            IntPtr ptr;

            if (!_indexToSlots.ContainsKey(slotIndex))
            {
                return false; // Does not seem to be initialized.
            }
            Debug.Assert(GetSlot(slotIndex).POuter != null);

            Debug.Assert(_activeQuerySlotIndex == -1);

            utf8Bytes = Encoding.UTF8.GetBytes(prompt);
            ptr = Marshal.AllocHGlobal(utf8Bytes.Length + 1); // Zero term.!

            // Copy byte array to unmanaged RAM:
            Marshal.Copy(utf8Bytes, 0, ptr, utf8Bytes.Length);

            // Add zero terminator string:
            Marshal.WriteByte(ptr + utf8Bytes.Length, (byte)'\0');

            _activeQuerySlotIndex = slotIndex; // For the callback.
            buf = MtLlmQuery(ptr, slotIndex, false, false);
            _activeQuerySlotIndex = -1;

            Marshal.FreeHGlobal(ptr);
            return buf;
        }

        internal static float[]? CreateEmbeddings(string prompt, int slotIndex)
        {
            float[] result;
            int count;
            byte[] promptUtf8Bytes;
            IntPtr promptPtr;
            nint resultPtr;

            if (!_indexToSlots.ContainsKey(slotIndex))
            {
                return null; // Does not seem to be initialized.
            }
            Debug.Assert(GetSlot(slotIndex).POuter != null);

            promptUtf8Bytes = Encoding.UTF8.GetBytes(prompt);
            promptPtr = Marshal.AllocHGlobal(
                promptUtf8Bytes.Length + 1); // Zero term.!

            // Copy byte array to unmanaged RAM:
            Marshal.Copy(promptUtf8Bytes, 0, promptPtr, promptUtf8Bytes.Length);

            // Add zero terminator string:
            Marshal.WriteByte(promptPtr + promptUtf8Bytes.Length, (byte)'\0');

            count = -1;
            resultPtr = MtLlmCreateEmbeddings(promptPtr, slotIndex, ref count);

            Marshal.FreeHGlobal(promptPtr);
            promptPtr = IntPtr.Zero;

            if (resultPtr == IntPtr.Zero)
            {
                return null;
            }
            if(count <= 0)
            {
                Debug.Assert(false); // Must not happen.
                MtLlmFree(resultPtr);
                resultPtr = IntPtr.Zero;
                return null;
            }

            result = new float[count];
            unsafe
            {
                float* unmanagedPtr = (float*)resultPtr;

                for (int i = 0; i < result.Length; ++i)
                {
                    result[i] = unmanagedPtr[i];
                }
            }
            MtLlmFree(resultPtr);
            resultPtr = IntPtr.Zero;

            return result;
        }

        /// <remarks>
        /// Does not support query or any given document being an empty string.
        /// 
        /// A lot of copying here, there might be a performance-improved way to
        /// get the data to the library..
        /// </remarks>
        internal static float[]? CreateRerank(
            string query, List<string> documents, int slotIndex)
        {
            float[] scores;
            byte[] queryUtf8Bytes;
            IntPtr queryPtr;
            nint scoresPtr;
            IntPtr documentsPtr;
            List<IntPtr> documentPtrs;

            if (query.Length == 0)
            {
                return null;
            }

            if (documents.Count == 0)
            {
                return null;
            }

            // Maybe not necessary:
            if (documents.Any(doc => doc.Length == 0))
            {
                return null;
            }

            if (!_indexToSlots.ContainsKey(slotIndex))
            {
                return null; // Does not seem to be initialized.
            }
            Debug.Assert(GetSlot(slotIndex).POuter != null);

            queryUtf8Bytes = Encoding.UTF8.GetBytes(query);
            queryPtr = Marshal.AllocHGlobal(
                queryUtf8Bytes.Length + 1); // Zero term.!

            // Copy byte array to unmanaged RAM:
            Marshal.Copy(queryUtf8Bytes, 0, queryPtr, queryUtf8Bytes.Length);

            // Add zero terminator string:
            Marshal.WriteByte(queryPtr + queryUtf8Bytes.Length, (byte)'\0');

            documentPtrs = new();
            documentsPtr = Marshal.AllocHGlobal(
                documents.Count * IntPtr.Size);
            for(int i = 0; i < documents.Count; ++i)
            {
                var docUtf8Bytes = Encoding.UTF8.GetBytes(documents[i]);
                var docPtr = Marshal.AllocHGlobal(
                        docUtf8Bytes.Length + 1); // Zero term.!

                // Copy byte array to unmanaged RAM:
                Marshal.Copy(docUtf8Bytes, 0, docPtr, docUtf8Bytes.Length);

                // Add zero terminator string:
                Marshal.WriteByte(docPtr + docUtf8Bytes.Length, (byte)'\0');

                // Remember the pointer:
                documentPtrs.Add(docPtr);

                // Copy to C array:
                Marshal.WriteIntPtr(documentsPtr, i * IntPtr.Size, docPtr);
            }

            scoresPtr = MtLlmRerank(
                queryPtr, documentsPtr, documentPtrs.Count, slotIndex);

            Marshal.FreeHGlobal(queryPtr);
            queryPtr = IntPtr.Zero;

            for(int i = 0; i < documentPtrs.Count; ++i)
            {
                Marshal.FreeHGlobal(documentPtrs[i]);
            }
            Marshal.FreeHGlobal(documentsPtr);
            documentsPtr = IntPtr.Zero;

            if (scoresPtr == IntPtr.Zero)
            {
                return null;
            }

            scores = new float[documents.Count];
            unsafe
            {
                float* unmanagedPtr = (float*)scoresPtr;

                for (int i = 0; i < scores.Length; ++i)
                {
                    scores[i] = unmanagedPtr[i];
                }
            }
            MtLlmFree(scoresPtr);
            scoresPtr = IntPtr.Zero;

            return scores;
        }

        internal static bool ReInit(MtLlmP p, int slotIndex)
        {
            Slot slot;
            mt_llm_p? buf;
            IntPtr ptr;

            if(IsInit(slotIndex))
            {
                // Do NOT call .DeInit() to let mt_llm be able to reset-only,
                // if possible (based on parameters given).
                _indexToSlots.Remove(slotIndex);
            }

            buf = CreateParams(p);
            if(buf == null)
            {
                return false;
            }

            slot = new();

            ptr = IntPtr.Zero;
            try
            {
                bool succeeded;
                ptr = Marshal.AllocHGlobal(Marshal.SizeOf(buf.Value));
                Marshal.StructureToPtr(buf, ptr, false);
                succeeded = MtLlmReInit(ptr, slotIndex);
                Marshal.FreeHGlobal(ptr);
                ptr = IntPtr.Zero;

                if(!succeeded)
                {
                    return false;
                }
            }
            catch(Exception)
            {
                if (ptr != IntPtr.Zero) // If is probably not necessary, here..
                {
                    Marshal.FreeHGlobal(ptr);
                }
                return false;
            }

            slot.POuter = new(p);
            _indexToSlots.Add(slotIndex, slot);
            return true;
        }

        /// <returns>
        /// A deep-copy of the slot's parameters or null, if slot is not
        /// initialized.
        /// </returns>
        internal static MtLlmP? GetParams(int slotIndex)
        {
            if(!_indexToSlots.ContainsKey(slotIndex))
            {
                Debug.Assert(false);
                return null;
            }
            return new MtLlmP(_indexToSlots[slotIndex].POuter!);
        }
    }
}
