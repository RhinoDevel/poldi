
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

namespace Poldi.CppInterface.MtLlm
{
    internal class MtLlmP
    {
        internal Int32 n_gpu_layers;
        
        internal UInt32 n_ctx;
        internal UInt32 n_batch;
        internal UInt32 n_ubatch;
        internal UInt32 threads;
        internal bool type_kv_q8_0;
        internal byte emb_or_rerank;

        internal UInt32 seed;
        internal Int32 top_k;
        internal float top_p;
        internal float min_p;
        internal float temp;

        internal string? grammar;

        internal bool cpu_moe;
        internal Int32 n_cpu_moe;

        internal string? model_file_path;
        internal string? sys_prompt;
        internal string? prompt_beg_delim;
        internal string? prompt_end_delim;
        internal string? sys_prompt_beg_delim;
        internal string? sys_prompt_mid_delim;
        internal string? sys_prompt_end_delim;
        internal string? think_beg_delim;
        internal string? think_end_delim;

        internal bool try_prompts_by_model;
        internal bool enable_thinking;
        internal bool enable_log;
        internal bool enable_llama_cpp_log;

        internal Func<int, string, MtLlmTokType, float[]?, bool>? callback;

        internal MtLlmP() { }

        /// <remarks>
        /// Deep-copies.
        /// </remarks>
        internal MtLlmP(MtLlmP o)
        {
            n_gpu_layers = o.n_gpu_layers;

            n_ctx = o.n_ctx;
            n_batch = o.n_batch;
            n_ubatch = o.n_ubatch;
            threads = o.threads;
            type_kv_q8_0 = o.type_kv_q8_0;
            emb_or_rerank = o.emb_or_rerank;

            seed = o.seed;
            top_k = o.top_k;
            top_p = o.top_p;
            min_p = o.min_p;
            temp = o.temp;

            grammar = o.grammar;

            cpu_moe = o.cpu_moe;
            n_cpu_moe = o.n_cpu_moe;

            model_file_path = o.model_file_path;
            sys_prompt = o.sys_prompt;
            prompt_beg_delim = o.prompt_beg_delim;
            prompt_end_delim = o.prompt_end_delim;
            sys_prompt_beg_delim = o.sys_prompt_beg_delim;
            sys_prompt_mid_delim = o.sys_prompt_mid_delim;
            sys_prompt_end_delim = o.sys_prompt_end_delim;
            think_beg_delim = o.think_beg_delim;
            think_end_delim= o.think_end_delim;

            try_prompts_by_model = o.try_prompts_by_model;
            enable_thinking = o.enable_thinking;
            enable_log = o.enable_log;
            enable_llama_cpp_log = o.enable_llama_cpp_log;

            callback = o.callback;
        }

        internal bool IsEqual(MtLlmP o)
        {
            if(n_gpu_layers != o.n_gpu_layers)
            {
                return false;
            }

            if(n_ctx != o.n_ctx)
            {
                return false;
            }
            if (n_batch != o.n_batch)
            {
                return false;
            }
            if (n_ubatch != o.n_ubatch)
            {
                return false;
            }
            if (threads != o.threads)
            {
                return false;
            }
            if (type_kv_q8_0 != o.type_kv_q8_0)
            {
                return false;
            }
            if (emb_or_rerank != o.emb_or_rerank)
            {
                return false;
            }

            if (seed != o.seed)
            {
                return false;
            }
            if (top_k != o.top_k)
            {
                return false;
            }
            if (top_p != o.top_p)
            {
                return false;
            }
            if (min_p != o.min_p)
            {
                return false;
            }
            if (temp != o.temp)
            {
                return false;
            }

            if (grammar != o.grammar)
            {
                return false;
            }

            if (cpu_moe != o.cpu_moe)
            {
                return false;
            }
            if (n_cpu_moe != o.n_cpu_moe)
            {
                return false;
            }

            if (model_file_path != o.model_file_path)
            {
                return false;
            }
            if (sys_prompt != o.sys_prompt)
            {
                return false;
            }
            if (prompt_beg_delim != o.prompt_beg_delim)
            {
                return false;
            }
            if (prompt_end_delim != o.prompt_end_delim)
            {
                return false;
            }
            if (sys_prompt_beg_delim != o.sys_prompt_beg_delim)
            {
                return false;
            }
            if (sys_prompt_mid_delim != o.sys_prompt_mid_delim)
            {
                return false;
            }
            if (sys_prompt_end_delim != o.sys_prompt_end_delim)
            {
                return false;
            }
            if (think_beg_delim != o.think_beg_delim)
            {
                return false;
            }
            if (think_end_delim != o.think_end_delim)
            {
                return false;
            }

            if (try_prompts_by_model != o.try_prompts_by_model)
            {
                return false;
            }
            if(enable_thinking != o.enable_thinking)
            {
                return false;
            }
            if (enable_log != o.enable_log)
            {
                return false;
            }
            if (enable_llama_cpp_log != o.enable_llama_cpp_log)
            {
                return false;
            }

            if (callback != o.callback)
            {
                return false;
            }

            return true;
        }
    }
}
