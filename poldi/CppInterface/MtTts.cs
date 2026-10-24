
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

// Marcel Timm, RhinoDevel, 2025mar10

using System.Runtime.InteropServices;
using System;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;

namespace Poldi.CppInterface
{
    internal static class MtTts
    {
#if LINUX
        private const string _dllPath =
            "../../../mt_tts/mt_tts/libmttts.so";
#else //LINUX
        private const string _dllPath =
             "..\\..\\..\\mt_tts\\x64\\Release\\mt_tts.dll";
#endif //LINUX

        internal static bool IsInit { get; private set; } = false;

        [DllImport(
            _dllPath,
            EntryPoint = "mt_tts_deinit",
            CallingConvention = CallingConvention.StdCall)]
        private static extern void MtTtsDeInit();

        // Does not seem to be so easy with C#:
        //
        //[DllImport(
        //    _dllPath,
        //    EntryPoint = "mt_tts_raw",
        //    CallingConvention = CallingConvention.StdCall)]
        //private static extern bool MtTtsRaw(
        //    IntPtr input_stream, IntPtr output_stream);

        [DllImport(
            _dllPath,
            EntryPoint = "mt_tts_free_raw",
            CallingConvention = CallingConvention.StdCall)]
        private static extern void MtTtsFreeRaw(IntPtr samples);

        [DllImport(
            _dllPath,
            EntryPoint = "mt_tts_to_raw",
            CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr MtTtsToRaw(
            IntPtr text, ref int out_sample_count);

        [DllImport(
            _dllPath,
            EntryPoint = "mt_tts_to_wav_file",
            CallingConvention = CallingConvention.StdCall)]
        private static extern bool MtTtsToWavFile(
            IntPtr text, IntPtr wav_path);

        [DllImport(
            _dllPath,
            EntryPoint = "mt_tts_reinit",
            CallingConvention = CallingConvention.StdCall)]
        private static extern void MtTtsReInit(
            IntPtr model_onnx_path, IntPtr model_json_path);

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

        internal static void DeInit()
        {
            IsInit = false;

            MtTtsDeInit();
        }

        /// <remarks>
        /// Just assumes that singleton is initialized via <see cref="ReInit"/>.
        /// </remarks>
        internal static List<short> ToRaw(string text)
        {
            List<short> retVal;
            int sampleCount = 0;
            var textStr = CreateUtf8Pointer(text);
            var samples = MtTtsToRaw(textStr, ref sampleCount);

            Debug.Assert(0 < sampleCount);

            retVal = new(sampleCount);

            unsafe
            {
                short* shortPtr = (short*)samples;

                for(int i = 0; i < sampleCount; ++i)
                {
                    retVal.Add(shortPtr[i]);
                }
            }

            Marshal.FreeHGlobal(textStr);
            textStr = nint.Zero;
            MtTtsFreeRaw(samples);
            samples = nint.Zero;
            return retVal;
        }

        /// <remarks>
        /// Just assumes that singleton is initialized via <see cref="ReInit"/>.
        /// </remarks>
        internal static void ToWavFile(string text, string wavPath)
        {
            var textStr = CreateUtf8Pointer(text);
            var wavPathStr = CreateUtf8Pointer(wavPath);

            MtTtsToWavFile(textStr, wavPathStr);

            Marshal.FreeHGlobal(textStr);
            textStr = nint.Zero;
            Marshal.FreeHGlobal(wavPathStr);
            wavPathStr = nint.Zero;
        }

        internal static void ReInit(string modelOnnxPath, string modelJsonPath)
        {
            var modelOnnxPathStr = CreateUtf8Pointer(modelOnnxPath);
            var modelJsonPathStr = CreateUtf8Pointer(modelJsonPath);

            DeInit();

            MtTtsReInit(modelOnnxPathStr, modelJsonPathStr);
            
            Marshal.FreeHGlobal(modelOnnxPathStr);
            modelOnnxPathStr = nint.Zero;
            Marshal.FreeHGlobal(modelJsonPathStr);
            modelJsonPathStr = nint.Zero;

            IsInit = true;
        }
    }
}
