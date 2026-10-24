
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

// Marcel Timm, RhinoDevel, 2024oct08

namespace Poldi.CppInterface.MtLlm
{
    /// <summary>
    /// Equivalent to content of C/C++ file mt_llm_tok_type.h.
    /// </summary>
    internal enum MtLlmTokType
    {
        PROMPT = 1,
        IRQ = 2,
        SAMPLED_NON_EOG_NON_CONTROL = 4,
        SAMPLED_EOG = 5,
        DELIM = 6,
        SYS_PROMPT = 7,
        SAMPLED_CONTROL_NON_EOG = 8,
        MT_TOK_TYPE_THINK_BEGIN = 9,
        MT_TOK_TYPE_THINK_TEXT = 10,
        MT_TOK_TYPE_THINK_END = 11
    }
}
