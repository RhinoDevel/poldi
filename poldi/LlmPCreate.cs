
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

// Marcel Timm, RhinoDevel, 2025feb20

using Poldi.CppInterface.MtLlm;
using System;

namespace Poldi
{
    /// <summary>
    /// Offers methods to load mt_llm parameters (which also include parameters
    /// for llama.cpp) for different usage scenarios.
    /// </summary>
    internal static class LlmPCreate
    {
        private static MtLlmP FromConfig(
            string prefix,
            Func<int, string, MtLlmTokType, float[]?, bool>? callback)
        {
            return new MtLlmP
            {
                n_gpu_layers = C.D<int>($"llm:{prefix}:gpuLayers"),

                seed = uint.MaxValue,
                n_ctx = C.D<uint>($"llm:{prefix}:contextSize"),
                n_batch = C.D<uint>($"llm:{prefix}:maxLogicalDecodeBatchSize"),
                n_ubatch = 
                    C.D<uint>($"llm:{prefix}:maxPhysicalDecodeBatchSize"),
                threads = C.D<uint>("llm:threads"),
                type_kv_q8_0 = C.D<bool>($"llm:{prefix}:typeKvQ80"),
                emb_or_rerank = 0, // Always 0, here. // Hard-coded

                top_k = C.D<int>($"llm:{prefix}:topK"),
                top_p = C.D<float>($"llm:{prefix}:topP"),
                min_p = C.D<float>($"llm:{prefix}:minP"),
                temp = C.D<float>($"llm:{prefix}:temperature"),

                grammar = C.D<string>($"llm:{prefix}:grammar"),

                cpu_moe = C.D<bool>($"llm:{prefix}:cpuMoE"),
                n_cpu_moe = C.D<int>($"llm:{prefix}:nCpuMoE"),

                model_file_path = C.D<string>($"llm:{prefix}:modelPath"),

                sys_prompt = C.D<string>($"llm:{prefix}:sysPrompt"),
                prompt_beg_delim = C.D<string>($"llm:{prefix}:promptBegDelim"),
                prompt_end_delim = C.D<string>($"llm:{prefix}:promptEndDelim"),
                sys_prompt_beg_delim =
                    C.D<string>($"llm:{prefix}:sysPromptBegDelim"),
                sys_prompt_mid_delim =
                    C.D<string>($"llm:{prefix}:sysPromptMidDelim"),
                sys_prompt_end_delim =
                    C.D<string>($"llm:{prefix}:sysPromptEndDelim"),
                think_beg_delim =
                    C.D<string>($"llm:{prefix}:thinkBegDelim"),
                think_end_delim =
                    C.D<string>($"llm:{prefix}:thinkEndDelim"),

                try_prompts_by_model =
                    C.D<bool>($"llm:{prefix}:tryPromptsByModel"),
                enable_thinking = C.D<bool>($"llm:{prefix}:enableThinking"),
                enable_log = C.D<bool>($"llm:enable_log"),
                enable_llama_cpp_log = C.D<bool>($"llm:enable_llama_cpp_log"),

                callback = callback
            };
        }

        /// <summary>
        /// Return configuration for chat model.
        /// </summary>
        internal static MtLlmP FromConfigChat(
            Func<int, string, MtLlmTokType, float[]?, bool>? callback)
        {
            var retVal = FromConfig("chat", callback);

            if (C.D<bool>($"llm:useMemorySystem"))
            {
                Log.Line("Ignoring given system prompt to be able to use memory system..");
                retVal.sys_prompt = ChatMem.ChatMem.GetSysPrompt();
            }

            return retVal;
        }

        /// <summary>
        /// Return configuration for option selection model.
        /// </summary>
        internal static MtLlmP FromConfigSel(
            Func<int, string, MtLlmTokType, float[]?, bool>? callback)
        {
            return new MtLlmP
            {
                threads = C.D<uint>("llm:threads"),
                enable_log = C.D<bool>($"llm:enable_log"),
                enable_llama_cpp_log = C.D<bool>($"llm:enable_llama_cpp_log"),

                type_kv_q8_0 = C.D<bool>($"llm:sel:typeKvQ80"),
                n_gpu_layers = C.D<int>($"llm:sel:gpuLayers"),
                n_ctx = C.D<uint>($"llm:sel:contextSize"),
                n_batch = C.D<uint>($"llm:sel:maxLogicalDecodeBatchSize"),
                n_ubatch = C.D<uint>($"llm:sel:maxPhysicalDecodeBatchSize"),

                top_k = C.D<int>("llm:sel:topK"),
                top_p = C.D<float>("llm:sel:topP"),
                min_p = C.D<float>("llm:sel:minP"),
                temp = C.D<float>("llm:sel:temperature"),

                grammar = C.D<string>("llm:sel:grammar"),

                cpu_moe = C.D<bool>($"llm:sel:cpuMoE"),
                n_cpu_moe = C.D<int>($"llm:sel:nCpuMoE"),
                model_file_path = C.D<string>($"llm:sel:modelPath"),
                sys_prompt = C.D<string>($"llm:sel:sysPrompt"),
                try_prompts_by_model = C.D<bool>($"llm:sel:tryPromptsByModel"),
                enable_thinking = C.D<bool>($"llm:sel:enableThinking"),
                prompt_beg_delim = C.D<string>($"llm:sel:promptBegDelim"),
                prompt_end_delim = C.D<string>($"llm:sel:promptEndDelim"),
                sys_prompt_beg_delim =
                    C.D<string>($"llm:sel:sysPromptBegDelim"),
                sys_prompt_mid_delim =
                    C.D<string>($"llm:sel:sysPromptMidDelim"),
                sys_prompt_end_delim =
                    C.D<string>($"llm:sel:sysPromptEndDelim"),
                think_beg_delim = C.D<string>($"llm:sel:thinkBegDelim"),
                think_end_delim = C.D<string>($"llm:sel:thinkEndDelim"),

                emb_or_rerank = 0, // Always 0, here. // Hard-coded
                seed = uint.MaxValue,
                callback = callback
            };
        }

        /// <summary>
        /// Return configuration for memory extraction model.
        /// </summary>
        internal static MtLlmP FromConfigMem(
            Func<int, string, MtLlmTokType, float[]?, bool>? callback)
        {
            return FromConfig("mem", callback);
        }

        /// <summary>
        /// Return configuration for embedding vector creation model.
        /// </summary>
        internal static MtLlmP FromConfigEmb()
        {
            const string prefix = "emb";

            return new MtLlmP
            {
                n_gpu_layers = C.D<int>($"llm:{prefix}:gpuLayers"),

                seed = uint.MaxValue,
                n_ctx = C.D<uint>($"llm:{prefix}:contextSize"),
                n_batch = C.D<uint>($"llm:{prefix}:maxLogicalDecodeBatchSize"),
                n_ubatch =
                    C.D<uint>($"llm:{prefix}:maxPhysicalDecodeBatchSize"),
                threads = C.D<uint>("llm:threads"),
                type_kv_q8_0 = C.D<bool>($"llm:{prefix}:typeKvQ80"),
                emb_or_rerank = 1, // Always 1, here. // Hard-coded

                top_k = 0,
                top_p = 0.0f,
                min_p = 0.0f,
                temp = 0.0f,

                grammar = string.Empty,

                cpu_moe = false,
                n_cpu_moe = 0,

                model_file_path = C.D<string>($"llm:{prefix}:modelPath"),

                sys_prompt = string.Empty,
                prompt_beg_delim = string.Empty,
                prompt_end_delim = string.Empty,
                sys_prompt_beg_delim = string.Empty,
                sys_prompt_mid_delim = string.Empty,
                sys_prompt_end_delim = string.Empty,
                think_beg_delim = string.Empty,
                think_end_delim = string.Empty,

                try_prompts_by_model = false,
                enable_thinking = false,
                enable_log = C.D<bool>($"llm:enable_log"),
                enable_llama_cpp_log = C.D<bool>($"llm:enable_llama_cpp_log"),

                callback = null
            };
        }

        /// <summary>
        /// Return configuration for reranking model.
        /// </summary>
        internal static MtLlmP FromConfigRer()
        {
            const string prefix = "rer";

            return new MtLlmP
            {
                n_gpu_layers = C.D<int>($"llm:{prefix}:gpuLayers"),

                seed = uint.MaxValue,
                n_ctx = C.D<uint>($"llm:{prefix}:contextSize"),
                n_batch = C.D<uint>($"llm:{prefix}:maxLogicalDecodeBatchSize"),
                n_ubatch = C.D<uint>($"llm:{prefix}:maxPhysicalDecodeBatchSize"),
                threads = C.D<uint>("llm:threads"),
                type_kv_q8_0 = C.D<bool>($"llm:{prefix}:typeKvQ80"),
                emb_or_rerank = 2, // Always 2, here. // Hard-coded

                top_k = 0,
                top_p = 0.0f,
                min_p = 0.0f,
                temp = 0.0f,

                grammar = string.Empty,

                cpu_moe = false,
                n_cpu_moe = 0,

                model_file_path = C.D<string>($"llm:{prefix}:modelPath"),

                sys_prompt = string.Empty,
                prompt_beg_delim = string.Empty,
                prompt_end_delim = string.Empty,
                sys_prompt_beg_delim = string.Empty,
                sys_prompt_mid_delim = string.Empty,
                sys_prompt_end_delim = string.Empty,
                think_beg_delim = string.Empty,
                think_end_delim = string.Empty,

                try_prompts_by_model = false,
                enable_thinking = false,
                enable_log = C.D<bool>($"llm:enable_log"),
                enable_llama_cpp_log = C.D<bool>($"llm:enable_llama_cpp_log"),

                callback = null
            };
        }
    }
}
