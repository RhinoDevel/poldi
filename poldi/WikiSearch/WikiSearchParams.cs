
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

// Marcel Timm, RhinoDevel, 2026mar15

using System;

namespace Poldi.WikiSearch
{
    internal class WikiSearchParams
    {
        internal bool UseReranker { get; set; }

        internal int EmbContextLength { get; set; }

        /// <remarks>
        /// Only used, if <see cref="WikiSearch.GetTopics"/> is used.
        /// See <see cref="WikiSearch.Exec"/>.
        /// </remarks>
        internal int MaxTopics { get; set; }

        /// <remarks>
        /// Make sure the context size is large enough.
        /// </remarks>
        internal int ChunkCountToInject { get; set; }

        internal int MinChunkWordCount { get; set; }

        internal Func<string, string, string?>? Infer { get; set; }
        internal Func<string, float[]?>? CreateEmbeddings { get; set; }
        internal Func<string, bool, int>? GetTokenCount { get; set; }
        internal Func<string, string[], int>? GetSelectIndex { get; set; }
    }
}
