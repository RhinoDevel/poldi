
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

// Marcel Timm, RhinoDevel, 2025jun18

// *****************************************************************************
// *** STATIC "GLOBAL" CONFIGURATION                                         ***
// *****************************************************************************

// *****************************************************************************
// *** "GLOBAL" VARIABLES                                                    ***
// *****************************************************************************

// *****************************************************************************
// *** PRIVATE FUNCTIONS                                                     ***
// *****************************************************************************

// *****************************************************************************
// *** PUBLIC FUNCTIONS                                                      ***
// *****************************************************************************

export function getAsStr(cmd)
{
    let retVal = cmd.exec;

    for(let i = 0; i < cmd.parameters.length; ++i)
    {
        retVal += ' ';
        retVal += cmd.parameters[i];
    }
    return retVal;
}
