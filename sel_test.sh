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

# Marcel Timm, RhinoDevel, 2025nov10
#
# BASH script to test the selection query functionality.

# Hard-coded for a specific system prompt, e.g.:
#
# "You are given a question.\nAll possible answers are given as options, each has a number.\nFind the correct answer.\nRespond by writing the answer's number, only (no prefix)!"

MT_USER_QUERY="$*"

MT_QUESTION="What is requested by saying \"$MT_USER_QUERY\"?"

MT_ANSWERS=(
    "A game"
    "Music"
    "A chat"
    "The current date and time"
    "The current internet IP address"
    "Other informations"
)

MT_ANSWERS_JSON=$(printf '%s\n' "${MT_ANSWERS[@]}" | jq -R . | jq -s -c .)

MT_DATA_JSON=$(jq -nc \
    --arg question "$MT_QUESTION" \
    --argjson options "$MT_ANSWERS_JSON" \
    '{question: $question, options: $options}')

MT_RESPONSE=$(
 curl \
  -N \
  -d "$MT_DATA_JSON" \
  -H "Content-Type: application/json" \
  http://127.0.0.1:8080/selQuery
)

echo "$MT_RESPONSE" | jq

MT_INDEX=$(echo "$MT_RESPONSE" | jq -r '.digProbsMaxIndex')
MT_REAL_INDEX=$((MT_INDEX - 1))

MT_ANSWER="${MT_ANSWERS[$MT_REAL_INDEX]}"

echo "User query: $MT_USER_QUERY"
echo "Selection : $MT_ANSWER ($MT_INDEX)"
