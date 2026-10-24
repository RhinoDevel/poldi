#!/usr/bin/env bash
set -o nounset
set -o errexit
set -o pipefail

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

# Marcel Timm, RhinoDevel, 2026jul25

# ******************************************************************************
# *** OPTIONS                                                                ***
# ******************************************************************************

WAIT_FOR_KEY_PRESS_AFTER_RUN=true

# ******************************************************************************
# *** Constants                                                              ***
# ******************************************************************************

LOG_PREFIX=">>> "

# Not really constants, but we act like it:
PATH_ROOT="$PWD"
PATH_STT="$PATH_ROOT/poldi/mt_stt"
PATH_LLM="$PATH_ROOT/poldi/mt_llm"
PATH_TTS="$PATH_ROOT/poldi/mt_tts"
PATH_TTS_ESPEAK_DATA="$PATH_TTS/mt_tts/piper/build/pi/share/espeak-ng-data"
PATH_TTS_ESPEAK_DATA_DEST="$PATH_ROOT/poldi/bin/Release/net8.0"
PATH_CLIENT="$PATH_ROOT/client"

# ******************************************************************************
# *** Helper functions                                                       ***
# ******************************************************************************

log() {
    printf '%s%s' "$LOG_PREFIX" "$*"
}

log_line() {
    log "$@"
    printf '\n'
}

wait_for_key_press() {
    log '*** Press any key to continue. ***'
    read -n 1 -s -r </dev/tty
    echo
}

RUN() {
    log_line "EXECUTING: $*"
    "$@"

    if "$WAIT_FOR_KEY_PRESS_AFTER_RUN"; then
        wait_for_key_press
    fi
}

# ******************************************************************************
# *** The actual script ("main")                                             ***
# ******************************************************************************

RUN git submodule update --init --recursive

RUN cd "$PATH_STT"
RUN ./mt_init.sh

RUN cd "$PATH_LLM"
RUN ./mt_init.sh

RUN cd "$PATH_TTS"
RUN ./mt_init.sh

RUN cd "$PATH_ROOT"
RUN ./mt_dotnet_build.sh

RUN cp -r "$PATH_TTS_ESPEAK_DATA" "$PATH_TTS_ESPEAK_DATA_DEST/"

RUN cd "$PATH_CLIENT"
RUN ./mt_init.sh
