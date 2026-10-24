
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

import { spawn } from 'node:child_process';

// *****************************************************************************
// *** STATIC "GLOBAL" CONFIGURATION                                         ***
// *****************************************************************************

// *****************************************************************************
// *** "GLOBAL" VARIABLES                                                    ***
// *****************************************************************************

// Initialized via init().
let sCmdPlayAudio = null;
let sCmdPlayAudioRaw = null;

// *****************************************************************************
// *** PRIVATE FUNCTIONS                                                     ***
// *****************************************************************************

// *****************************************************************************
// *** PUBLIC FUNCTIONS                                                      ***
// *****************************************************************************

export function init(cfg)
{
    sCmdPlayAudio = {
        exec: 'aplay',
        // Should not be used, does not allow more than one sound at once.
        //parameters: [ '-D', `plughw:${cfg.card},${cfg.dev}` ]
        //
        // To allow more than one sound at the same time.
        //parameters: [ '-D', 'plug:dmix' ]
        //
        // E.g., if ALSA uses PipeWire, PipeWire does the mixing (so multiple
        // sounds at once work, too) and PipeWire is configured to use the
        // correct output device by default (e.g. configured via wpctl):
        parameters: []
    };

    //sCmdPlayAudioRaw = {
    //    exec: 'ffplay',
    //    parameters: [
    //        '-nodisp', // Maybe not necessary.
    //        '-autoexit',
    //        '-f', `S${String(cfg.bitDepth)}${cfg.endian}E`,
    //        '-ar', `${String(cfg.sampleRate)}`,
    //        '-ac', `${String(cfg.channels)}`,
    //        '-'
    //    ]
    //};
    //
    sCmdPlayAudioRaw = {
        exec: sCmdPlayAudio.exec,
        parameters: [
            ...sCmdPlayAudio.parameters,
            '-f', `S${String(cfg.bitDepth)}_${cfg.endian}E`,
            '-c', `${String(cfg.channels)}`,
            '-r', `${String(cfg.sampleRate)}`
        ]
    };
}

export function raw()
{
    return spawn(
        sCmdPlayAudioRaw.exec,
        sCmdPlayAudioRaw.parameters,
        {
            stdio: [ 'pipe', 'inherit', 'inherit' ]
        });
}

/**
 * - Can be called with await, too.
 */
export function file(absFilePath)
{
    return new Promise(
    	(resolve, reject) =>
    	{
    		const playAudioFile = spawn(
    			sCmdPlayAudio.exec,
    			[
    				...sCmdPlayAudio.parameters,
    				absFilePath
   				],
    			{
    				stdio: [ 'ignore', 'inherit', 'inherit' ] 
    			}
    		);

    		playAudioFile.on(
                'error',
                () =>
                {
                    console.error('Error: Playing audio file!');
                    reject();
                });  
            playAudioFile.on(
                'close',
                code =>
                {
                    //console.log(
                    //    `Playing audio file closed with code ${String(code)}.`);
                    resolve(code);
                });	
    	});
}
