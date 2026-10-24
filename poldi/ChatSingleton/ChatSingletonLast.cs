
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

namespace Poldi.ChatSingleton
{
    internal class ChatSingletonLast
    {
        /// <remarks>
        /// Currently used for debugging, only.
        /// </remarks>
        internal MtLlmTokType? Type = null;

        /// <remarks>
        /// Currently used for debugging, only.
        /// </remarks>
        internal string Query = string.Empty;

        internal string System = string.Empty;

        /// <summary>
        /// Gets filled with the LLM's response via callback, but only with the
        /// tokens of type
        /// <see cref="MtLlmTokType.SAMPLED_NON_EOG_NON_CONTROL"/>, which
        /// represent the response/answer of the LLM being inferred during a
        /// run.
        /// </summary>
        internal string Response = string.Empty;
    }
}
