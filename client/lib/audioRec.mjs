
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

import * as command from "./command.mjs";

import { spawn } from 'node:child_process';

// *****************************************************************************
// *** STATIC "GLOBAL" CONFIGURATION                                         ***
// *****************************************************************************

// *****************************************************************************
// *** "GLOBAL" VARIABLES                                                    ***
// *****************************************************************************

let sCmdRecordAudioStr = null; // Initialized via init().

// *****************************************************************************
// *** PRIVATE FUNCTIONS                                                     ***
// *****************************************************************************

// *****************************************************************************
// *** PUBLIC FUNCTIONS                                                      ***
// *****************************************************************************

export function init(cfg)
{
    const cmdRecordAudio = {
        exec: 'arecord',
        parameters: [
            // Commented out, because it is assumed that the correct recording
            // device is configured, e.g., if ALSA uses PipeWire.
            //'-D', `hw:${cfg.card},${cfg.dev}`,

            '-r', `${String(cfg.sampleRate)}`,
            '-c', `${String(cfg.channels)}`,
            '-f', `S${String(cfg.bitDepth)}_${cfg.endian}E`,
            '-t', 'raw'
        ]
    };

    sCmdRecordAudioStr = command.getAsStr(cmdRecordAudio);

    //console.log(`audioRec.init(): sCmdRecordAudioStr = "${sCmdRecordAudioStr}"`);
}

/**
 * - This function will only signal the process to terminate, but NOT wait for
 *   it to actually be terminated.
 */
export function kill(audioRecProcess)
{
    // Possible, because of detached option set to true [see to() function].
    //console.log('audioRec.kill(): Entered method.');
    process.kill(-audioRecProcess.pid, 'SIGTERM');
}

/**
 * - Can be called with await, too.
 */
export function to(cmd, outProcess)
{
    //console.log('audioRec.to(): Entered method.');
    return new Promise(
        (resolve, reject) =>
        {
            //console.log('audioRec.to(): Entered new Promise().');

            const audioRecProcess = spawn(
                'sh',
                [
                    '-c',
                    sCmdRecordAudioStr + ' | ' + command.getAsStr(cmd)
                ],
                {
                    // Necessary to let kill() function close all (child)
                    // processes belonging to a specific audio rec. process.
                    detached: true,

                    stdio: [ 'ignore', 'inherit', 'inherit' ],
                    cwd: cmd?.cwd // (uses undefined, if there is no cmd.cwd)
                });
    
            // audioRecProcess.on(
            //     'exit',
            //     () => console.log('Record-audio-to process exit reached.'));
            audioRecProcess.on(
                'error',
                err =>
                {
                    console.error('Error: Record-audio-to process!');
                    reject(err);
                });  
            audioRecProcess.on(
                'close',
                code =>
                {
                    //console.log(
                    //    `Record-audio-to process closed with code ${String(code)}.`);
                    resolve(code);
                });

            //console.log('audioRec.to(): Determining, if outProcess is given..');
            if(outProcess !== null && typeof outProcess === 'object')
            {
                //console.log('audioRec.to(): Setting outProcess.process..');
                outProcess.process = audioRecProcess;
            }
        });
}
