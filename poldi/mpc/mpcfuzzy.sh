#!/bin/bash

#    P.O.L.D.I. - Private Offline Local Digital Intelligence
#    Copyright (C) 2026  Marcel Timm, RhinoDevel
#
#    This program is free software: you can redistribute it and/or modify
#    it under the terms of the GNU General Public License as published by
#    the Free Software Foundation, either version 3 of the License, or
#    (at your option) any later version.
#
#    This program is distributed in the hope that it will be useful,
#    but WITHOUT ANY WARRANTY; without even the implied warranty of
#    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
#    GNU General Public License for more details.
#
#    You should have received a copy of the GNU General Public License
#    along with this program.  If not, see <https://www.gnu.org/licenses/>.

# Marcel Timm, RhinoDevel, 2026oct25

# ******************************************************
# *** Fuzzy search and auto-playback via MPC and fzf ***
# ******************************************************

filter="$*"

if [ -z "$filter" ]; then
    echo "No string to search for given." >&2
    exit 1
fi

filter="${filter%"${filter##*[[:alnum:]]}"}"

if [ -z "$filter" ]; then
    echo "Only non-alphanumeric characters in given string." >&2
    exit 1
fi

needle=$(mpc listall | fzf --filter="$filter" | head -n1)

if [ -z "$needle" ]; then
    echo "No match found for \"$filter\"." >&2
    exit 1
fi

echo "Found match for \"$filter\"." >&2

if ! mpc -q clear; then
    echo "Clearing queue failed." >&2
    exit 1
fi

if ! mpc -q add "$needle"; then
    echo "Adding \"$needle\" to the playlist failed." >&2
    exit 1
fi

if ! mpc -q play; then
    echo "Starting playback failed." >&2
    exit 1
fi

desc=$(mpc playlist) || { echo "Reading playlist failed." >&2; exit 1; }
if [ -z "$desc" ]; then
    echo "Playlist seems to be empty." >&2
    exit 1
fi

echo "$desc"
