
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

// Marcel Timm, RhinoDevel, 2025dec07

using System;

namespace Poldi.ChatMem
{
    /// <summary>
    /// Represents an entry of the DB table STATEMENT.
    /// </summary>
    internal class Statement
    {
        internal long Nr { get; set; } = 0;
        internal long ChatNr { get; set; } = 0;
        internal DateTime UtcCreatedAt { get; set; } = DateTime.MinValue;

        internal int StatementIndex { get; set; } = -1;
        internal string Content { get; set; } = string.Empty;
        internal float[] Embedding { get; set; } = new float[0];
    }
}
