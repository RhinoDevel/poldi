
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

namespace Poldi.CppInterface.MtLlm
{
    /// <remarks>
    /// Holds constant values equivalent to those defined in mt_llm_p.h.
    /// </remarks>
    internal class MtLlmPLen
    {
        internal const int MT_LLM_P_LEN_GRAMMAR = 511 + 1;
        internal const int MT_LLM_P_LEN_MODEL_FILE_PATH = 255 + 1;
        internal const int MT_LLM_P_LEN_PROMPT_BEG_DELIM = 128 + 1;
        internal const int MT_LLM_P_LEN_PROMPT_END_DELIM = 128 + 1;
        internal const int MT_LLM_P_LEN_SYS_PROMPT_BEG_DELIM = 128 + 1;
        internal const int MT_LLM_P_LEN_SYS_PROMPT_MID_DELIM = 128 + 1;
        internal const int MT_LLM_P_LEN_SYS_PROMPT_END_DELIM = 128 + 1;
        internal const int MT_LLM_P_LEN_SYS_PROMPT = 4095 + 1;
        internal const int MT_LLM_P_LEN_THINK_BEG_DELIM = 64 + 1;
        internal const int MT_LLM_P_LEN_THINK_END_DELIM = 64 + 1;
    }
}
