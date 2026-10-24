
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

// Marcel Timm, RhinoDevel, 2026oct08

// Notes:
//
// - Modules have their own scope.
// - Modules have strict mode enabled by default.
// - Module files should have the ending ".mjs" (at least during development),
//   but it depends on the HTTP server, if ".mjs" files will be send to client
//   as JavaScript files (so we use ".js", at least for releases).

// ***************************************
// *** Static constants and variables: ***
// ***************************************

let s_lastNr = 0;

// **************************
// *** Private functions: ***
// **************************

function incrementLastNr()
{
    ++s_lastNr;
};

// *************************
// *** Public functions: ***
// *************************

export function getString()
{
    return 'This is some test string returned from test_getString().';
};

export function getNextNr()
{
    incrementLastNr();

    return s_lastNr;
};
