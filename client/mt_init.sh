#!/usr/bin/env bash
set -o nounset
set -o errexit
set -o pipefail

# Marcel Timm, RhinoDevel, 2026jul25

# ******************************************************************************
# *** SYSTEM SETTING (configure for your computer)                           ***
# ******************************************************************************

JOBS=10

# ******************************************************************************
# *** OPTIONS                                                                ***
# ******************************************************************************

WAIT_FOR_KEY_PRESS_AFTER_RUN=true

URL_OPENWAKEWORD_ONNX_PATH="https://github.com/microsoft/onnxruntime/releases/download/v1.28.0/"
OPENWAKEWORD_ONNX_NAME="onnxruntime-linux-x64-1.28.0"
OPENWAKEWORD_ONNX_FILE="$OPENWAKEWORD_ONNX_NAME.tgz"
URL_OPENWAKEWORD_ONNX="$URL_OPENWAKEWORD_ONNX_PATH$OPENWAKEWORD_ONNX_FILE"

# ******************************************************************************
# *** Constants                                                              ***
# ******************************************************************************

LOG_PREFIX=">>> "

# Not really constants, but we act like it:
PATH_ROOT="$PWD"
PATH_OPENWAKEWORD="$PATH_ROOT/ext/openWakeWord-cpp"
PATH_RHASSPYSILENCE="$PATH_ROOT/ext/rhasspy-silence"

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

log_line "System settings:"
log_line "- parallel jobs: $JOBS"
wait_for_key_press

RUN git submodule update --init --recursive

RUN npm install

RUN cd "$PATH_OPENWAKEWORD"
RUN curl --fail -LO "$URL_OPENWAKEWORD_ONNX"
RUN tar xvzf "$OPENWAKEWORD_ONNX_FILE"
RUN mkdir lib
RUN ln -s "../$OPENWAKEWORD_ONNX_NAME" lib/$(uname -m)
RUN make

RUN cd "$PATH_ROOT"
RUN python3 -m venv client_venv
RUN source client_venv/bin/activate
RUN cd "$PATH_RHASSPYSILENCE"
RUN pip install -r requirements.txt
# The following hack makes webrtcvad version 2.0.10 run with Python 3.13.
RUN cd $(python -c 'import site; print(site.getsitepackages()[0])')
RUN sed -i '/^import pkg_resources$/s/^/# /' webrtcvad.py
RUN sed -i "s/^__version__ = pkg_resources\.get_distribution('webrtcvad')\.version$/__version__ = \"MT-Hack-Version\"/" webrtcvad.py