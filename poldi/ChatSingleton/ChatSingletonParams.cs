
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

// RhinoDevel, Marcel Timm, 2026mar06

using Poldi.CppInterface.MtLlm;
using System;

namespace Poldi.ChatSingleton
{
    internal class ChatSingletonParams
    {
        /// <remarks>
        /// <see cref="ChatSingleton.QueryLlmInternal"/> ignores
        /// <see cref="MtLlmP.callback" />, so it should be set to null when
        /// given.
        /// </remarks>
        internal MtLlmP ParamsGiven { get; set; } = new();

        internal string PromptGiven { get; set; } = string.Empty;

        /// <summary>
        /// Is optional. Gets the last inferred piece of type
        /// <see cref="MtLlmTokType.SAMPLED_NON_EOG_NON_CONTROL"/> and the
        /// complete last text retrieved as sampled non-EOG non-control so far.
        /// Must return false on error, true on success.
        /// </summary>
        /// <remarks>
        /// Is mandatory, if modules shall be used via <see cref="UseModules"/>.
        /// </remarks>
        internal Func<CallbackParams, bool>?
            OnSampledNonEogNonCtrl { get; set; }

        internal bool KeepLlmLoaded { get; set; }
        internal bool WriteToConsole { get; set; }
        internal int SlotIndex { get; set; }

        /// <summary>
        /// If enabled, existing memories will be used in (injected into) chat
        /// and new memories will be created from former chats.
        /// </summary>
        internal bool UseMemorySystem { get; set; }

        /// <remarks>
        /// Also needs <see cref="OnSampledNonEogNonCtrl"/> to be set.
        /// </remarks>
        internal bool UseModules { get; set; }

        internal ChatSingletonParams()
        {
            // Nothing done, here.
        }

        /// <remarks>
        /// Deep-copies.
        /// </remarks>
        internal ChatSingletonParams(ChatSingletonParams p)
        {
            ParamsGiven = new(p.ParamsGiven); // Deep-copies
            PromptGiven = p.PromptGiven;
            OnSampledNonEogNonCtrl = p.OnSampledNonEogNonCtrl;
            KeepLlmLoaded = p.KeepLlmLoaded;
            WriteToConsole = p.WriteToConsole;
            SlotIndex = p.SlotIndex;
            UseMemorySystem = p.UseMemorySystem;
            UseModules = p.UseModules;
        }
    }
}
