
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

// Marcel Timm, RhinoDevel, 2026mar29

using System;
using System.Collections.Generic;

namespace Poldi.ChatMem
{
    internal class AugmentParams
    {
        internal required string Prompt { get; set; }
        internal required DateTime PromptDateTime { get; set; }
        internal required List<Message> LastMsgs { get; set; }
        internal required bool ConstraintsParagraph { get; set; }
        internal required bool MemoryParagraph { get; set; }
        internal required List<long>? MemoryParagraphFixedInjectStatementNrs { get; set; }
    }
}
