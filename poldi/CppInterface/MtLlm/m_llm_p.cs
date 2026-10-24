
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

using System;
using System.Runtime.InteropServices;

namespace Poldi.CppInterface.MtLlm
{
    /// <remarks>
    /// Equivalent to C/C++ implementation's struct mt_llm_p.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    internal struct mt_llm_p
    {
        internal Int32 n_gpu_layers;

        internal UInt32 n_ctx;
        internal UInt32 n_batch;
        internal UInt32 n_ubatch;
        internal UInt32 threads;
        internal byte type_kv_q8_0;
        internal byte emb_or_rerank;

        internal UInt32 seed;
        internal Int32 top_k;
        internal float top_p;
        internal float min_p;
        internal float temp;

        [MarshalAs(
            UnmanagedType.ByValArray,
            SizeConst = MtLlmPLen.MT_LLM_P_LEN_GRAMMAR)]
        internal byte[] grammar;

        internal byte cpu_moe;
        internal Int32 n_cpu_moe;

        [MarshalAs(
            UnmanagedType.ByValArray,
            SizeConst = MtLlmPLen.MT_LLM_P_LEN_MODEL_FILE_PATH)]
        internal byte[] model_file_path;

        [MarshalAs(
            UnmanagedType.ByValArray,
            SizeConst = MtLlmPLen.MT_LLM_P_LEN_SYS_PROMPT)]
        internal byte[] sys_prompt;
        [MarshalAs(
            UnmanagedType.ByValArray,
            SizeConst = MtLlmPLen.MT_LLM_P_LEN_PROMPT_BEG_DELIM)]
        internal byte[] prompt_beg_delim;
        [MarshalAs(
            UnmanagedType.ByValArray,
            SizeConst = MtLlmPLen.MT_LLM_P_LEN_PROMPT_END_DELIM)]
        internal byte[] prompt_end_delim;
        [MarshalAs(
            UnmanagedType.ByValArray, 
            SizeConst = MtLlmPLen.MT_LLM_P_LEN_SYS_PROMPT_BEG_DELIM)]
        internal byte[] sys_prompt_beg_delim;
        [MarshalAs(
            UnmanagedType.ByValArray,
            SizeConst = MtLlmPLen.MT_LLM_P_LEN_SYS_PROMPT_MID_DELIM)]
        internal byte[] sys_prompt_mid_delim;
        [MarshalAs(
            UnmanagedType.ByValArray,
            SizeConst = MtLlmPLen.MT_LLM_P_LEN_SYS_PROMPT_END_DELIM)]
        internal byte[] sys_prompt_end_delim;
        [MarshalAs(
            UnmanagedType.ByValArray,
            SizeConst = MtLlmPLen.MT_LLM_P_LEN_THINK_BEG_DELIM)]
        internal byte[] think_beg_delim;
        [MarshalAs(
            UnmanagedType.ByValArray,
            SizeConst = MtLlmPLen.MT_LLM_P_LEN_THINK_END_DELIM)]
        internal byte[] think_end_delim;

        internal byte try_prompts_by_model;
        internal byte enable_thinking;
        internal byte enable_log;
        internal byte enable_llama_cpp_log;

        internal IntPtr callback;
    }
}
