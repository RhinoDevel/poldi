
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

// Marcel Timm, RhinoDevel, 2025mar17

using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Poldi.CppInterface
{
    internal static class MtStt
    {
#if LINUX
        private const string _dllPath =
           "../../../mt_stt/mt_stt/libmtstt.so";
#else //LINUX
        private const string _dllPath =
             "..\\..\\..\\mt_stt\\x64\\MT CUDA Release\\mt_stt_CUDA.dll"; // TODO: Hard-coded for CUDA support!
#endif //LINUX
        
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void OnProgressDelegate(int progress);

        [DllImport(
            _dllPath,
            EntryPoint = "mt_stt_transcribe_with_data",
            CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr MtSttTranscribeWithData(
            bool use_gpu,
            int n_threads,
            IntPtr language,
            bool translate_to_en,
            IntPtr initial_prompt,
            IntPtr model_data,
            ulong model_data_len,
            IntPtr audio_data_arr,
            int audio_data_length,
            nint on_progress_func,
            out IntPtr opt_out_word_probs,
            ref int opt_out_word_probs_count,
            IntPtr opt_out_parts_ret_val_indices,
            IntPtr opt_parts_audio_data_indices,
            IntPtr opt_parts_audio_data_limits,
            int opt_parts_length);

        [DllImport(
            _dllPath,
            EntryPoint = "mt_stt_transcribe_with_file",
            CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr MtSttTranscribeWithFile(
            bool use_gpu,
            int n_threads,
            IntPtr language,
            bool translate_to_en,
            IntPtr initial_prompt,
            IntPtr model_file_path,
            IntPtr audio_data_arr,
            int audio_data_length,
            nint on_progress_func,
            out IntPtr opt_out_word_probs,
            ref int opt_out_word_probs_count,
            IntPtr opt_out_parts_ret_val_indices,
            IntPtr opt_parts_audio_data_indices,
            IntPtr opt_parts_audio_data_limits,
            int opt_parts_length);

        [DllImport(
            _dllPath,
            EntryPoint = "mt_stt_free",
            CallingConvention = CallingConvention.StdCall)]
        private static extern void MtSttFree(IntPtr ptr);

        [DllImport(
            _dllPath,
            EntryPoint = "mt_stt_cancel",
            CallingConvention = CallingConvention.StdCall)]
        private static extern void MtSttCancel();

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

        private static int GetUtf8CodePointLen(byte first)
        {
            if ((first & 0b1000_0000) == 0)
            {
                return 1; // 1-byte (ASCII).
            }
            if ((first & 0b1110_0000) == 0b1100_0000)
            {
                return 2; // 2-byte.
            }
            if ((first & 0b1111_0000) == 0b1110_0000)
            {
                return 3; // 3-byte.
            }
            if ((first & 0b1111_1000) == 0b1111_0000)
            {
                return 4; // 4-byte.
            }
            throw new ArgumentException("Invalid UTF-8 first byte.");
        }

        private static int GetUtf16IndexFromUtf8(nint utf8Ptr, int byteOffset)
        {
            int utf16Index = 0, curByteOffset = 0;

            unsafe
            {
                byte* bytePtr = (byte*)utf8Ptr;

                while (true)
                {
                    var curByte = bytePtr[curByteOffset];
                    int codePointLen;

                    // Check for null terminator:
                    if (curByte == 0) // '\0'
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(byteOffset), "Byte offset is out of range.");
                    }

                    if (curByteOffset == byteOffset)
                    {
                        return utf16Index;
                    }

                    codePointLen = GetUtf8CodePointLen(curByte);
                    if (byteOffset < curByteOffset + codePointLen)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(byteOffset),
                            "Byte offset does not align with a UTF-8 code point.");
                    }

                    curByteOffset += codePointLen;

                    // Increment UTF-16 index, accounting for surrogate pairs
                    // / Surrogate pair for 4-byte UTF-8:
                    ++utf16Index;
                    if (1 < codePointLen && 0xF0 <= curByte)
                    {
                        ++utf16Index;
                    }
                }
            }
        }

        internal static void Cancel()
        {
            Log.Line("Cancelling STT..");
            MtSttCancel();
        }

        /// <remarks>
        /// The result will automatically be trimmed (if not null).
        /// </remarks>
        internal static string? Transcribe(
            bool use_gpu,
            int n_threads,
            string languageStr,
            bool translate_to_en,
            string initialPromptStr,
            string modelPathStr,
            float[] audio_data_arrFloats,
            Action<int>? on_progress,
            out float[]? wordProbs,
            int[]? opt_out_parts_ret_val_indices,
            int[]? opt_parts_audio_data_indices,
            int[]? opt_parts_audio_data_limits)
        {
            Debug.Assert(0 < n_threads);

            string? result;
            IntPtr wordProbsPtr;
            int wordProbsCount;
            nint resultPtr;

            { // Limits scope.
                var language = CreateUtf8Pointer(languageStr);
                var initial_prompt = CreateUtf8Pointer(initialPromptStr);
                var model_file_path = CreateUtf8Pointer(modelPathStr);

                GCHandle opt_out_parts_ret_val_indicesHandle = default,
                    opt_parts_audio_data_indicesHandle = default,
                    opt_parts_audio_data_limitsHandle = default;

                // Avoid garbage collector to move the floating-point array:
                //
                var audio_data_arrHandle = GCHandle.Alloc(
                        audio_data_arrFloats, GCHandleType.Pinned);

                if (opt_out_parts_ret_val_indices != null)
                {
                    Debug.Assert(0 < opt_out_parts_ret_val_indices.Length);

                    Debug.Assert(
                        opt_parts_audio_data_indices != null
                            && opt_parts_audio_data_limits != null);

                    Debug.Assert(
                        opt_out_parts_ret_val_indices.Length
                            == opt_parts_audio_data_indices.Length);
                    Debug.Assert(
                        opt_out_parts_ret_val_indices.Length
                            == opt_parts_audio_data_limits.Length);

                    opt_out_parts_ret_val_indicesHandle = GCHandle.Alloc(
                        opt_out_parts_ret_val_indices, GCHandleType.Pinned);
                    opt_parts_audio_data_indicesHandle = GCHandle.Alloc(
                        opt_parts_audio_data_indices, GCHandleType.Pinned);
                    opt_parts_audio_data_limitsHandle = GCHandle.Alloc(
                        opt_parts_audio_data_limits, GCHandleType.Pinned);
                }

                wordProbsPtr = IntPtr.Zero;
                wordProbsCount = -1;
                resultPtr = MtSttTranscribeWithFile( // Takes ownership!
                    use_gpu,
                    n_threads,
                    language,
                    translate_to_en,
                    initial_prompt,
                    model_file_path,
                    audio_data_arrHandle.AddrOfPinnedObject(),
                    audio_data_arrFloats.Length,
                    on_progress == null
                        ? nint.Zero
                        : Marshal.GetFunctionPointerForDelegate(
                            new OnProgressDelegate(on_progress)),
                    out wordProbsPtr,
                    ref wordProbsCount,
                    opt_out_parts_ret_val_indices != null
                        ? opt_out_parts_ret_val_indicesHandle.AddrOfPinnedObject()
                        : nint.Zero,
                    opt_out_parts_ret_val_indices != null
                        ? opt_parts_audio_data_indicesHandle.AddrOfPinnedObject()
                        : nint.Zero,
                    opt_out_parts_ret_val_indices != null
                        ? opt_parts_audio_data_limitsHandle.AddrOfPinnedObject()
                        : nint.Zero,
                    opt_out_parts_ret_val_indices != null
                        ? opt_out_parts_ret_val_indices.Length
                        : 0);

                audio_data_arrHandle.Free();
                if (opt_out_parts_ret_val_indices != null)
                {
                    opt_out_parts_ret_val_indicesHandle.Free();
                    opt_parts_audio_data_indicesHandle.Free();
                    opt_parts_audio_data_limitsHandle.Free();
                }

                Marshal.FreeHGlobal(initial_prompt);
                Marshal.FreeHGlobal(language);
                Marshal.FreeHGlobal(model_file_path);
            }

            if (resultPtr == IntPtr.Zero)
            {
                if (wordProbsPtr != IntPtr.Zero)
                {
                    Debug.Assert(false); // Must not get here.
                    MtSttFree(wordProbsPtr);
                }
                wordProbs = null;
                return null;
            }

            if (wordProbsCount == 0)
            {
                if (wordProbsPtr != IntPtr.Zero)
                {
                    Debug.Assert(false); // Must not get here.
                    MtSttFree(wordProbsPtr);
                    wordProbsPtr = IntPtr.Zero;
                }
                wordProbs = new float[0];
            }
            else
            {
                if (wordProbsPtr == IntPtr.Zero)
                {
                    wordProbs = null;
                    return null;
                }

                unsafe
                {
                    float* unmanagedPtr = (float*)wordProbsPtr;

                    wordProbs = new float[wordProbsCount];
                    for (int i = 0; i < wordProbsCount; ++i)
                    {
                        wordProbs[i] = unmanagedPtr[i];
                    }
                }

                MtSttFree(wordProbsPtr);
                wordProbsPtr = IntPtr.Zero;
                wordProbsCount = -1;
            }

            // Transform the UTF-8/byte indices into UTF-16/"C#" indices:
            //
            if (opt_out_parts_ret_val_indices != null)
            {
                for (int i = 0; i < opt_out_parts_ret_val_indices.Length; ++i)
                {
                    int curTextIndex = opt_out_parts_ret_val_indices[i];

                    if (curTextIndex == -1)
                    {
                        continue;
                    }

                    opt_out_parts_ret_val_indices[i] = GetUtf16IndexFromUtf8(
                        resultPtr, curTextIndex);
                }
            }

            result = Marshal.PtrToStringUTF8(resultPtr);
            Debug.Assert(result != null);
            Debug.Assert(
                opt_out_parts_ret_val_indices == null
                    || opt_out_parts_ret_val_indices.All(
                        i => i == -1 || (0 <= i && i < result.Length)));
            MtSttFree(resultPtr);
            return result.Trim();
        }
    }
}
