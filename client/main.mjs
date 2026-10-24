
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

// Marcel Timm, RhinoDevel, 2026jul18

import * as audioPlay from "./lib/audioPlay.mjs";
import * as audioRec from "./lib/audioRec.mjs"; // To initialize, here.
import * as wakeWord from "./lib/wakeWord.mjs";
import * as voiceRec from "./lib/voiceRec.mjs";

import cfg from './config.mjs';

import fs from 'node:fs';
import path from 'node:path';
import { createInterface } from "node:readline";
import { Readable } from 'node:stream';
import { strict as assert } from 'node:assert';

import { fetch, Agent } from 'undici';

// Notes:
//
// - Modules are singletons, if imported from multiple other modules/files,
//   the variables (etc.) of the imported modules hold the same content and
//   the same changes are visible from all importing modules/files.
// - Modules have their own scope.
// - Modules have strict mode enabled by default.
// - For web development:
//   Module files should have the ending ".mjs" (at least during development),
//   but it depends on the HTTP server, if ".mjs" files will be send to client
//   as JavaScript files (so we use ".js", at least for releases).
// - For NodeJS development:
//   Module files should have the ending ".mjs".

// *****************************************************************************
// *** STATIC "GLOBAL" CONFIGURATION                                         ***
// *****************************************************************************

const logPrefix = '>>> ';

//          START => IDLE         <======================================\\
//                   ||                                                  ||
// wakeword detected ||                                                  ||
//                   \/                                                  ||
//                   RECORDING    <=================\\                   ||
//                   ||        /\                   ||                   ||
//    voice recorded ||        ||                   ||                   ||
//                   \/        ||                   ||                   ||
//                   SENDING | CANCELING            ||                   ||
//                           |                      ||                   ||
//                   ||        /\                   || immed             ||
//  response started ||        || wakeword detected || && playback done  ||
//                   \/        ||                   ||                   ||
//                   PLAYING      ==================//                   ||
//                   immed=                                              ||
//                                               !immed && playback done ||
//                                =======================================//
//
// immed: Boolean flag, will be set in PLAYING state, after response started.
//        Indicates, if the server requested an immediate answer from the user.
const State = Object.freeze(
    {
        IDLE: 'IDLE',
        RECORDING: 'RECORDING',
        SENDING: 'SENDING',
        PLAYING: 'PLAYING',
        CANCELING: 'CANCELING'
    });

const undiciAgent = new Agent(
    {
        //keepAlive: true, // Can be enabled for more recent versions of undici.

        // These parameters are the reason to use undici, because the server may
        // takes a lot of time before sending anything as response, if it has to
        // import a lot of messages from the most recent chat that shall be
        // continued: 
        headersTimeout: 0,
        bodyTimeout: 0
    });

const rl = createInterface({ input: process.stdin });

// Also see server source code.
const headerReqImmedUserResp = 'Request-Immediate-User-Response';

// *****************************************************************************
// *** PRIVATE FUNCTIONS                                                     ***
// *****************************************************************************

function log(msg)
{
    console.log(`${logPrefix}${String(msg)}`);
}

function shutdown(signal)
{
    log(`Entered shutdown function with signal "${signal}"`);

    wakeWord.kill(); // OK, if not running.
    voiceRec.kill(); // OK, if not running.

    // TODO: Add more?!

    process.exit(0);
}

function onEnterPressed()
{
    log('Enter was pressed.');
    wakeWord.kill();
}

function init()
{
    process.on('SIGINT', () => shutdown('SIGINT'));
    process.on('SIGTERM', () => shutdown('SIGTERM'));

    rl.on("line", onEnterPressed);

    audioPlay.init(cfg.audio.play);
    audioRec.init(cfg.audio.rec);
    wakeWord.init(cfg.clientAbsPath, cfg.wakeWordModelFile);
    voiceRec.init(
        cfg.clientAbsPath,
        cfg.voiceCmdRecFile,
        cfg.voiceRec.silenceSeconds,
        cfg.voiceRec.maxSeconds);
}

async function cancel()
{
    await fetch(
        cfg.serverUrl + cfg.request.cancel,
        { 
            method: 'GET',
            dispatcher: undiciAgent
        });

    log('Sent cancel request to server.');
}

async function onIdle()
{
    log(`Entered state "${State.IDLE}".`);

    log('Waiting for wake word getting detected..');
    await wakeWord.detect(null); // Waits for wake word.
    log('Wake word was detected.');

    return State.RECORDING;
}

async function onRecording()
{
    log(`Entered state "${State.RECORDING}".`);

    await audioPlay.file( // Signal to user.
        path.join(
            cfg.clientAbsPath,
            cfg.soundFolder,
            cfg.soundFiles.wakeWordDetected));

    log('Starting voice recording..');
    await voiceRec.exec(); // Record user.
    log('Voice was recorded.');

    audioPlay.file( // Make "processing" sound (in the background!).
        path.join(
            cfg.clientAbsPath, cfg.soundFolder, cfg.soundFiles.processing));

    // For testing.
    //await audioPlay.file(path.join(cfg.clientAbsPath, cfg.voiceCmdRecFile));

    return State.SENDING;
}

async function onSending()
{
    log(`Entered state "${State.SENDING}".`);

    return {
        state: State.PLAYING,
        response: await fetch( // <- Waits for the first response "packet".
            cfg.serverUrl + cfg.request.audioToAudio,
            {
                method: 'POST',
                headers: { 'Content-Type': 'application/octet-stream' },
                body: fs.createReadStream(
                    path.join(cfg.clientAbsPath, cfg.voiceCmdRecFile)),
                duplex: 'half', // TODO: Necessary? I forgot, why..
                dispatcher: undiciAgent
            })
    };
}

async function onPlaying(response)
{
    log(`Entered state "${State.PLAYING}".`);

    assert(response !== null, 'Expected response variable to be set.');

    const speaker = {
        stream: Readable.fromWeb(response.body),
        process: audioPlay.raw()
    };
    const reqImmedUserResp = // Hard-coded use of "true", see server code.
        response.headers.get(headerReqImmedUserResp) === 'true';
    const wakeWordDetect = {};
    let wakeWordWasDetected = false;

    log(`Immed. user resp. is${(reqImmedUserResp ? '' : ' NOT')} requested.`);
    
    // speaker.process error logging is done below.

    speaker.process.stdin.on(
        'error', () => console.error('Error: Speaker process standard input!'));
    
    log('Preparing for playback..');
    speaker.stream.pipe(speaker.process.stdin);

    assert(wakeWordDetect !== null && typeof wakeWordDetect === 'object');

    wakeWord.detect(wakeWordDetect); // <- In the background / not waiting here.

    assert('process' in wakeWordDetect);

    // Wait for wakeword being detected or playback to finish or fail (assuming
    // that none of these did already happen before getting here..).
    await new Promise(
        resolve =>
        {
            wakeWordDetect.process.once(
                'close',
                () =>
                {
                    log('Wakeword detection process closed.');
                    wakeWordWasDetected = true;
                    resolve();
                });

            speaker.process.once(
                'close',
                () =>
                {
                    log('Speaker process closed.');
                    resolve();
                });

            // (on error, logging is already done above)
            speaker.process.once(
                'error',
                () =>
                {
                    log('Error: Speaker process!');
                    resolve();
                });
        });

    if(wakeWordWasDetected)
    {
        return { state: State.CANCELING, speaker: speaker };
    }

    wakeWord.kill();

    if(reqImmedUserResp)
    {
        return { state: State.RECORDING, speaker: null };
    }
    return { state: State.IDLE, speaker: null };
}

async function onCanceling(speaker)
{
    log(`Entered state "${State.CANCELING}".`);

    assert(speaker !== null, 'Expected speaker variable to be set.');

    let lateChunkCount = 0;

    await cancel(); // (logs)

    // Wait for the HTTP response to end:
    log('Waiting for stream to finish..');
    for await (const _chunk of speaker.stream)
    {
        ++lateChunkCount; // Nothing to do, here.
    }
    if(lateChunkCount !== 0)
    {
    	log(`Received ${String(lateChunkCount)} late chunks.`);
    }

    log('Unpiping..');
    speaker.stream.unpipe(speaker.process.stdin);

    log('Calling .end() on playback\'s standard input..');
    speaker.process.stdin.end()/*.destroy()*/;

    log('Killing playback process..');
    speaker.process.kill('SIGINT'); // "CTRL + C"

    return State.RECORDING;
}

async function main()
{
    let state = State.IDLE; // This is the initial state.
    let response = null;
    let speaker = null;

    init();

    while(true)
    {
        switch(state)
        {
            case State.IDLE:
            {
                state = await onIdle();
                break;
            }
            case State.RECORDING:
            {
                state = await onRecording();
                break;
            }
            case State.SENDING:
            {
                assert(
                    response === null,
                    'Expected response variable to be unset.');

                ({ state, response } = await onSending());
                break;
            }
            case State.PLAYING:
            {
                assert(
                    speaker === null,
                    'Expected speaker variable to be unset.');

                ({ state, speaker } = await onPlaying(response));
                response = null;
                break;
            }
            case State.CANCELING:
            {
                state = await onCanceling(speaker);
                speaker = null;
                break;
            }

            default:
            {
                log(`main(): Error: Unsupported state "${String(state)}" given!`);
                throw new Error(`Unsupported state "${String(state)}" given!`);
            }
        }
    }
}

// *****************************************************************************
// *** ENTRY                                                                 ***
// *****************************************************************************

await main();
