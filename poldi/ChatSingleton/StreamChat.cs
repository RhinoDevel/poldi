
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

// RhinoDevel, Marcel Timm, 2025nov15

using System;
using System.Diagnostics;
using System.IO;

namespace Poldi.ChatSingleton
{
    /// <summary>
    /// Gets inferred answer from LLM and sends it to a
    /// <see cref="TextWriter"/>, which may be a <see cref="StreamWriter"/> that
    /// can be used to (e.g.) return a chunked response to a HTTP request.
    /// </summary>
    /// <remarks>
    /// Uses the singleton <see cref="ChatSingleton"/>.
    /// </remarks>
    internal static class StreamChat
    {
        private static TextWriter? _writer = null;

        /// <param name="p">
        /// <see cref="CallbackParams.ForceOutput"/> is not necessary, here,
        /// because given <see cref="CallbackParams.Piece"/> will always be
        /// output immediately, anyway.
        /// </param>
        private static bool OnSampledNonEogNonCtrl(CallbackParams p)
        {
            Debug.Assert(p.LastText.EndsWith(p.Piece)); // To clarify this.

            try // E.g., in case a connection got closed/lost.
            {
                _writer!.Write(p.Piece);
            }
            catch (Exception e)
            {
                Log.Line($"Error: Exception with message \"{e.Message}\" during stream write attempt! Returning with error..");
                return false;
            }
            _writer.Flush();
            return true;
        }

        /// <remarks>
        /// Modifies given <paramref name="p"/>'s
        /// <see cref="ChatSingletonParams.OnSampledNonEogNonCtrl"/>, at least
        /// for this method's runtime.
        /// </remarks>
        internal static ChatSingletonResult Query(
            ChatSingletonParams p, TextWriter writer)
        {
            Debug.Assert(writer != null);
            Debug.Assert(_writer == null);

            ChatSingletonResult retVal;

            _writer = writer;
            Debug.Assert(p.OnSampledNonEogNonCtrl == null);
            p.OnSampledNonEogNonCtrl = OnSampledNonEogNonCtrl; // Augments
            retVal = ChatSingleton.Query(p);
            p.OnSampledNonEogNonCtrl = null;
            _writer = null;
            return retVal;
        }
    }
}
