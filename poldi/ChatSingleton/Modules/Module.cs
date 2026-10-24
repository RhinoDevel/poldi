
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

// Marcel Timm, RhinoDevel, 2026jun04

namespace Poldi.ChatSingleton.Modules
{
    /// <summary>
    /// Base class for so-called modules. A module represents an alternative
    /// mode to just chatting with the LLM, e.g. Wikipedia or chat history
    /// search.
    /// </summary>
    internal abstract class Module
    {
        internal bool IsSameType(Module o)
        {
            return GetType() == o.GetType();
        }

        internal abstract string GetIntention();
        internal abstract string Exec(
            string input,
            out bool calledByMistake,
            out bool isDone,
            out bool reqImmedUserResp);
        internal abstract Module Create();
    }
}
