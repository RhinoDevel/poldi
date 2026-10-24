
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

// Wakewords: https://github.com/fwartner/home-assistant-wakewords-collection/tree/main/en

import * as audioRec from "./audioRec.mjs";

import path from 'node:path';
import { strict as assert } from 'node:assert';

// *****************************************************************************
// *** "GLOBAL" VARIABLES                                                    ***
// *****************************************************************************

let sCmdWakeWordDetect = null; // Set up by init().

let detectProcess = null;

// *****************************************************************************
// *** PUBLIC FUNCTIONS                                                      ***
// *****************************************************************************

export function init(clientAbsPath, wakeWordModelFile)
{
    sCmdWakeWordDetect = {
        // Use branch mt_exit_on_detect
        // from https://github.com/RhinoDevel/openWakeWord-cpp.git to let this
        // command exit on detection of a wake word.
        exec: path.join(
            clientAbsPath, 'ext', 'openWakeWord-cpp', 'build', 'openwakeword'),
        parameters: [ '--model', path.join(clientAbsPath, wakeWordModelFile) ],
        cwd: path.join(clientAbsPath, 'ext', 'openWakeWord-cpp')
    };
}

/**
 * - Does nothing, if no wakeword detection process is currently running.
 * - This function will only signal the process to terminate, but NOT wait for
 *   it to actually be terminated.
 */
export function kill()
{
    if(detectProcess !== null)
    {
        //console.log('wakeWord.kill(): detectProcess is NOT null, "killing" audioRec process..');
        audioRec.kill(detectProcess.process);
        detectProcess = null;
    }
    //else
    //{
    //    console.log('wakeWord.kill(): detectProcess is null, doing nothing.');
    //}
}

/**
 * - Throws an exception, if a wakeword detection process is already running.
 */
export async function detect(outProcess)
{
    //console.log('wakeWord.detect(): Entered method.');

    if(detectProcess !== null)
    {
        //console.log('wakeWord.detect(): Error: detectProcess is NOT null!');
        throw new Error('A wakeword detection process is already running!');
    }

    detectProcess = {};

    //console.log('wakeWord.detect(): Calling audioRec.to()..');
    const recPromise = audioRec.to(sCmdWakeWordDetect, detectProcess);
    // => detectProcess.process is set.

    assert('process' in detectProcess);

    //console.log('wakeWord.detect(): Determining, if outProcess.process is to be set..');
    if(outProcess !== null && typeof outProcess === 'object')
    {
        //console.log('wakeWord.detect(): Setting outProcess.process..');
        outProcess.process = detectProcess.process;
    }

    const curDetectProcessPid = detectProcess.process.pid;

    // Waits for the wake word to be detected (or the process to be killed).
    await recPromise;

    if(detectProcess !== null
        && curDetectProcessPid === detectProcess.process.pid)
    {
        //console.log('wakeWord.detect(): Setting detectProcess to null..');
        detectProcess = null;
    }
}
