
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

import * as audioRec from "./audioRec.mjs";

import path from 'node:path';

// *****************************************************************************
// *** STATIC "GLOBAL" CONFIGURATION                                         ***
// *****************************************************************************

// *****************************************************************************
// *** "GLOBAL" VARIABLES                                                    ***
// *****************************************************************************

let sCmdRecordVoiceCmd = null; // Set up by init().

let voiceRecProcess = null;

// *****************************************************************************
// *** PRIVATE FUNCTIONS                                                     ***
// *****************************************************************************

// *****************************************************************************
// *** PUBLIC FUNCTIONS                                                      ***
// *****************************************************************************

export function init(
    clientAbsPath,
    voiceCmdRecFile,
    silenceSeconds,
    maxSeconds)
{
    const voiceCmdWav = `${clientAbsPath}/${voiceCmdRecFile}`;

    sCmdRecordVoiceCmd = {
        // Use branch mt_rec_to_stdout
        // from https://github.com/RhinoDevel/rhasspy-silence.git to let this
        // command forward the recorded data to stdout (which triggers saving
        // of the audio to the wave file).
        exec: path.join(
            clientAbsPath, 'ext', 'rhasspy-silence', 'bin', 'rhasspy-silence'),
        parameters: [
            '--silence-seconds', String(silenceSeconds),
            '--max-seconds', String(maxSeconds),
            '--quiet',
            '>', voiceCmdWav
        ],
        cwd: path.join(clientAbsPath, 'ext', 'rhasspy-silence')
    };
}

/**
 * - Does nothing, if no voice recording process is currently running.
 * - This function will only signal the process to terminate, but NOT wait for
 *   it to actually be terminated.
 */
export function kill()
{
    if(voiceRecProcess !== null)
    {
        //console.log('voiceRec.kill(): voiceRecProcess is NOT null, "killing" audioRec process..');
        audioRec.kill(voiceRecProcess.process);
        voiceRecProcess = null;
    }
    //else
    //{
    //    console.log('voiceRec.kill(): voiceRecProcess is null, doing nothing.');
    //}
}

/**
 * - Throws an exception, if a voice recording process is already running.
 */
export async function exec()
{
    if(voiceRecProcess !== null)
    {
        console.log('voiceRec.kill(): Error: A voice recording process is already running!');
        throw new Error('A voice recording process is already running!');
    }

    voiceRecProcess = {};

    const recPromise = audioRec.to( // Record user.
        sCmdRecordVoiceCmd, voiceRecProcess);
    // => voiceRecProcess.process is set.

    const curVoiceRecProcessPid = voiceRecProcess.process.pid;

    // Waits for the voice recording to end (or the process to be killed).
    await recPromise;

    if(voiceRecProcess !== null
        && curVoiceRecProcessPid === voiceRecProcess.process.pid)
    {
        voiceRecProcess = null;
    }
}
