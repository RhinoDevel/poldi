#!/bin/sh

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

# Marcel Timm, RhinoDevel, 2025mar01

# Run via commandline, in Linux.

# curl -v -d '{"prompt": "What is the capital of Danmark?"}' -H "Content-Type: application/json" http://127.0.0.1:8080/query

export LD_LIBRARY_PATH=./poldi/mt_stt/mt_stt/whisper.cpp/build/bin:$LD_LIBRARY_PATH
export LD_LIBRARY_PATH=./poldi/mt_llm/mt_llm/llama.cpp/build/bin:$LD_LIBRARY_PATH
export LD_LIBRARY_PATH=./poldi/mt_llm/mt_llm/llama.cpp/build/common:$LD_LIBRARY_PATH
export LD_LIBRARY_PATH=./poldi/mt_tts/mt_tts/piper/build/pi/lib:$LD_LIBRARY_PATH

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export MT_DOTNET_PATH=""

exec ${MT_DOTNET_PATH}dotnet run --project poldi --configuration Release -p:TargetOS=Linux -- "$@"
