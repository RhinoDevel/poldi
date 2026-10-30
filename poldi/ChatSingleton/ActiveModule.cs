
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

// Marcel Timm, RhinoDevel, 2026jul07

using System.Collections.Generic;

namespace Poldi.ChatSingleton;

internal class ActiveModule
{
    internal static readonly List<Modules.Module> Modules;

    /// <remarks>
    /// null means LLM mode.
    /// </remarks>
    internal Modules.Module? Current = null;

    internal readonly List<string> Msgs = new();
    internal int MsgsFromBeforeCount = 0;

    internal readonly List<Modules.Module> CalledByMistake = [];

    static ActiveModule()
    {
        Modules = [];

        Modules.Add(new Modules.ChatSearch());
        Modules.Add(new Modules.WikiSearch());
        Modules.Add(new Modules.WwwSearch());

        if(C.D<bool>("music:enable"))
        {
            Modules.Add(new Modules.MusicPlayer());
        }
    }
}
