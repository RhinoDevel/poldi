
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

// Marcel Timm, RhinoDevel, 2026oct04

//import * as test from "./modules/test.js";
//
// document.body.textContent = test.getString();
//
// for(let i = 0;i < 10; ++i)
// {
//     console.log(`Next nr. is: ${test.getNextNr().toString()}.`);
// }

// Notes:
//
// - To be executed as script of type module on page load.
//
// - Modules have their own scope.
// - Modules have strict mode enabled by default.
// - Module files should have the ending ".mjs" (at least during development),
//   but it depends on the HTTP server, if ".mjs" files will be send to client
//   as JavaScript files (so we use ".js", at least for releases).

// *****************************************************************************
// *** Constants                                                             ***
// *****************************************************************************

// TODO: Can be modified via server's TOML file, so should be modifiable here, too!
const audioToAudioPath = 'audioToAudio';
const cancelPath = 'cancel';

const record = {
    sampleRate: 16000, // Hz
    channels: 1, // Mono
    bitsPerSample: 16 // Two bytes.
};

const play = (() => // IIFE
{
    const retVal = {
        sampleRate: 22050, // Hz
        channels: 1, // Mono
        bytesPerSample: 2,
        blockMs: 200, // Create one playback block every 200 ms.
        blockS: null, // See below.
        blockBytes: null // See below.
    };

    retVal.blockS = retVal.blockMs / 1000;
    retVal.blockBytes = Math.floor(
        retVal.sampleRate * retVal.bytesPerSample * retVal.blockS);

    return retVal;
})();

// Voice box:
const gain = 4;
const columns = 3;
const segments = 9;
const colSegments = [5, 8, 5];
const canvasWidth = 160; // Pixels
const canvasHeight = 240; // Pixels

// *****************************************************************************
// *** "Global" variables                                                    ***
// *****************************************************************************

// Voice box:
let colorIndex = 2;
let g = null;
let analyser = null;
let smooth = 0.0;
let isPlaying = false;

let chunks = null;
let butEle = null;
let audioCtx = null;

let receivalFinished = Promise.resolve();
let resolveReceivalFinished = () => { };

// *****************************************************************************
// *** Functions                                                             ***
// *****************************************************************************

/**
 * - For the voice box.
 */
function currentLevel()
{
    let samples = null;
    let sum = 0.0;

    if(!isPlaying)
    {
        return 0.0;
    }

    samples = new Float32Array(analyser.fftSize);
    analyser.getFloatTimeDomainData(samples);
    
    for (const s of samples)
    {
        sum += s * s;
    }
    
    return Math.sqrt(sum / samples.length);
}

/**
 * - For the voice box.
 */
function drawCanvas()
{   
    const raw = Math.min(1.0, gain * currentLevel());
    
    smooth = smooth < raw ? raw : 0.88 * smooth; // Fast attack, slow release.

    const colW = canvasWidth / columns - 16;
    const segH = canvasHeight / (2 * segments) - 4;
    
    g.clearRect(0, 0, canvasWidth, canvasHeight);
    
    for (let c = 0; c < columns; ++c)
    {
        const x = c * (canvasWidth / columns) + 8;
        const n = colSegments[c];
        const lit = Math.round(smooth * n);
        
        for (let s = 0; s < n; ++s)
        {
            const on = s < lit;
            
            switch(colorIndex)
            {
                case 0:
                {
                    g.fillStyle = on ? '#ff2a2a' : '#2a0707';
                    break;
                }
                case 1:
                {
                    g.fillStyle = on ? '#ffb000' : '#2a1c00';
                    break;
                }
                case 2: // Falls through.
                default:
                {
                    g.fillStyle = on ? '#2aa8ff' : '#07182a';
                    break;
                }
            }
            
            const off = s * (segH + 4);
            
            g.fillRect( // Up from center.
                x, canvasHeight / 2 - off - segH - 2, colW, segH);
            g.fillRect(
                x, canvasHeight / 2 + off + 2, colW, segH); // Down from center.
        }
    }

    if(isPlaying)
    {
        requestAnimationFrame(drawCanvas);
    }
}

function createWav(pcm)
{
    const wav = new ArrayBuffer(44 + pcm.byteLength);
    const v = new DataView(wav);

    const text = (o, s) =>
        [...s].forEach((c, i) => v.setUint8(o + i, c.charCodeAt(0)));

    text(0, 'RIFF');
    v.setUint32(4, 36 + pcm.byteLength, true);
    text(8, 'WAVE');
    text(12, 'fmt ');
    v.setUint32(16, 16, true);
    v.setUint16(20, 1, true);
    v.setUint16(22, record.channels, true);
    v.setUint32(24, record.sampleRate, true);
    v.setUint32(
        28,
        record.sampleRate * record.channels * record.bitsPerSample / 8,
        true);
    v.setUint16(32, record.channels * record.bitsPerSample / 8, true);
    v.setUint16(34, record.bitsPerSample, true);
    text(36, 'data');
    v.setUint32(40, pcm.byteLength, true);

    new Uint8Array(wav, 44).set(new Uint8Array(pcm));

    return wav;
}

/** Convert chunks given (recorded from microphone) to the format that the Poldi
 *  server accepts/understands.
 *
 * - Output can be played back with "ffplay -f s16le -ar 16000 <filename>",
 *   where ffplay is assuming that the data is mono / single channel.
 */
async function createRawPcm()
{
    const blob = new Blob(chunks);
    const blobBuf = await blob.arrayBuffer();
    const decoded = await audioCtx.decodeAudioData(blobBuf);

    // Resample and downmix to mono, 16000 Hz.

    const offlineCtx = new OfflineAudioContext(
        record.channels, // Must be 1 (for mono)..
        Math.ceil(decoded.duration * record.sampleRate),
        record.sampleRate);

    const src = offlineCtx.createBufferSource();

    src.buffer = decoded;
    src.connect(offlineCtx.destination);
    src.start();

    const audioBuf = await offlineCtx.startRendering();

    // This is mono, 16000 Hz Float32 PCM at this point.

    const samples = audioBuf.getChannelData(0);

    // Two bytes per signed 16-bit sample.

    const retVal = new ArrayBuffer(samples.length * record.bitsPerSample / 8);
    const view = new DataView(retVal);

    const absMinVal = Math.pow(2.0, record.bitsPerSample - 1.0);
    const maxVal = absMinVal - 1.0;

    for (let i = 0; i < samples.length; ++i)
    {
        // Clamp Float32.
        const sample = Math.max(-1.0, Math.min(1.0, samples[i]));

        // Float32 to signed 16-bit integer.
        const pcm = sample < 0.0 ? absMinVal * sample : maxVal * sample;

        // Hard-coded for 2 bytes per sample!
        view.setInt16(2 * i, pcm, true); // Little-endian ("S16_LE").
    }

    return retVal;
}

/** Concatenate two byte arrays.
 *
 * The first argument should hold the PCM data we already received.
 * The second argument should be the newest HTTP chunk.
 */
function appendBytes(first, second)
{
    const retVal = new Uint8Array(first.byteLength + second.byteLength);

    retVal.set(first, 0);
    retVal.set(second, first.byteLength);

    return retVal;
}

/** Convert one block of little-endian PCM16 data into an AudioBuffer and
 *  schedule it for playback.
 *
 *  - Returns the AudioContext time at which the next block should begin.
 */
function scheduleBlock(bytes, nextPlayTime, isLastBlock)
{
    let startTime = null;

    // Make sure we only process complete 16-bit samples.
    const completeByteLength =
        bytes.byteLength - (bytes.byteLength % play.bytesPerSample);

    if (completeByteLength === 0)
    {
        return nextPlayTime;
    }

    const sampleCount = completeByteLength / play.bytesPerSample;

    // Create a mono AudioBuffer at the server's sample rate.
    const audioBuffer = audioCtx.createBuffer(
        play.channels, // Must (probably) be 1 (mono)..
        sampleCount,
        play.sampleRate);

    const channelData = audioBuffer.getChannelData(0);

    // Read the server's PCM data as signed 16-bit, little-endian.
    const view = new DataView(
        bytes.buffer, bytes.byteOffset, completeByteLength);

    for (let i = 0; i < sampleCount; ++i)
    {
        const d = view.getInt16(i * play.bytesPerSample, true);

        // Convert Int16 [-32768, 32767] to Web audio Float32 [-1.0, ~1.0].
        channelData[i] = d / 32768.0;
    }

    // AudioBufferSourceNode is a one-shot node.
    //
    // We create one node per 200 ms playback block, NOT one node per
    // HTTP/network chunk.
    const src = audioCtx.createBufferSource();

    src.buffer = audioBuffer;

    //src.connect(audioCtx.destination);
    src.connect(analyser); // Voice box.

    // Voice box:
    isPlaying = true;
    drawCanvas();

    const now = audioCtx.currentTime;

    // If we don't currently have audio scheduled, start this block immediately.
    // This also handles the case where playback has caught up with the server
    // and there was silence between two pieces of received audio.

    if (nextPlayTime === null || nextPlayTime < now)
    {
        startTime = now;
    }
    else
    {
        startTime = nextPlayTime;
    }

    if(isLastBlock)
    {
        src.onended =
            () =>
            {
                setButIdle();
                isPlaying = false; // Voice box.
            };
    }

    src.start(startTime);

    // The next block should start exactly when this block finishes.
    return startTime + audioBuffer.duration;
}

async function sendCancel()
{
    let response = null;

    try
    {
        response = await fetch(cancelPath, { method: 'GET' });
    }
    catch(err)
    {
        console.log(`Error: Canceling failed with msg. "${err}"!`);
        return false;
    }

    if (!response.ok)
    {
        console.log('Error: Cancel response is not OK!');
        return false;
    }

    return true;
}

/**
 * - Returns a ReadableStreamDefaultReader with access to the server's response
 *   data or null on error.
 */
async function sendAudio(wav)
{
    let response = null;

    try
    {
        response = await fetch(
            audioToAudioPath, 
            {
                method: 'POST',
                headers: { 'Content-Type': 'application/octet-stream' },
                body: wav
            });
    }
    catch(err)
    {
        console.log(`Error: Sending failed with msg. "${err}"!`);
        return null;
    }

    if (!response.ok)
    {
        console.log('Error: Sending response is not OK!');
        return null;
    }
    if (response.body === null)
    {
        // Should never get here.
        console.log('Error: No readable body received from server!');
        return null;
    }

    return response.body.getReader();
}

function setButIdle()
{
    butEle.style['background-color'] = 'green';
    butEle.style.color = 'white';

    butEle.disabled = false;

    butEle.innerText = 'press to start recording';
}
function setButCommunicating()
{
    butEle.style['background-color'] = 'blue';
    butEle.style.color = 'white';

    butEle.disabled = false;

    butEle.innerText = 'communicating.. press to interrupt and start recording';
}
function setButCancel()
{
    butEle.style['background-color'] = '';
    butEle.style.color = '';

    butEle.disabled = true;

    butEle.innerText = 'interrupting..';
}
function setButPrepareForRec()
{
    butEle.style['background-color'] = '';
    butEle.style.color = '';

    butEle.disabled = true;

    butEle.innerText = 'preparing..';
}
function setButStoppingRec()
{
    butEle.style['background-color'] = '';
    butEle.style.color = '';

    butEle.disabled = true;

    butEle.innerText = 'stopping..';
}
function setButRec()
{
    butEle.style['background-color'] = 'red';
    butEle.style.color = 'white';

    butEle.disabled = false;

    butEle.innerText = 'recording.. press to stop recording';
}

/**
 * - (Re-)sets "global" chunks to null.
 * - Lazy-initializes global audioCtx, if not yet initialized.
 */
async function onRecordStop(e)
{
    let pcm = null;
    let wav = null;

    // AudioContext time at which the next playback block should begin.
    // null means that playback has currently caught up with the received audio.
    let nextPlayTime = null;

    let didReceiveAudio = false;

    if(chunks.length === 0)
    {
        // Will probably never get here.
        console.log('No chunks on record stop, doing nothing.');
        chunks = null;
        setButIdle();
        return;
    }

    setButCommunicating();

    if(audioCtx === null)
    {
        audioCtx = new AudioContext();

        analyser = audioCtx.createAnalyser();
        analyser.fftSize = 1024;
        analyser.connect(audioCtx.destination);
    }
    else
    {
        console.log('Resuming already initialized audio context..');
        await audioCtx.resume(); // Probably not necessary.
    }

    // Create WAV content.
    pcm = await createRawPcm();
    chunks = null;
    wav = createWav(pcm);
    pcm = null;

    // // For testing:
    // const blob = new Blob(
    //     [wav], { type: "application/audio/wav" });
    // const url = URL.createObjectURL(blob);
    // window.open(url);
    // setTimeout(() => URL.revokeObjectURL(url), 10000); // Clean-up

    receivalFinished = new Promise(
        resolve =>
        {
            resolveReceivalFinished = resolve;
        });

    try
    {
        let pendingBytes = null;
        let hasPartial = false;

        // Send WAV to Poldi server.
        const reader = await sendAudio(wav);

        if(reader == null)
        {
            setButIdle(); // TODO: Show error msg. to user!
            chunks = null;
            return; // Called method logs on error.
        }
        
        wav = null;

        // Retrieve and playback audio response.

        // "Unlimited", because server responses should not be longer than a
        // minute or so.
        pendingBytes = new Uint8Array(0);

        while (true)
        {
            const { value, done } = await reader.read();

            if (done)
            {
                break;
            }
            if (!value || value.byteLength === 0)
            {
                continue;
            }

            didReceiveAudio = true;

            // Add the received bytes to the pending data.
            pendingBytes = appendBytes(pendingBytes, value);

            // Convert complete portions of the pending data into fixed-size
            // playback blocks.
            while (
                // Keep one block for after the loop to be able to detect when
                // the playback is done in scheduleBlock().
                2 * play.blockBytes
                    <= pendingBytes.byteLength)
            {
                const block = pendingBytes.slice(0, play.blockBytes);

                pendingBytes = pendingBytes.slice(play.blockBytes);
                nextPlayTime = scheduleBlock(block, nextPlayTime, false);
            }
        }

        if(play.blockBytes <= pendingBytes.byteLength)
        {
            const block = pendingBytes.slice(0, play.blockBytes);

            pendingBytes = pendingBytes.slice(play.blockBytes);
            hasPartial = play.bytesPerSample <= pendingBytes.byteLength;
            nextPlayTime = scheduleBlock(block, nextPlayTime, !hasPartial);
        }
        else
        {
            hasPartial = play.bytesPerSample <= pendingBytes.byteLength;
        }

        // The server has finished sending audio.
        // There may still be a final partial playback block.
        // Play it as long as it contains complete samples.
        if(hasPartial)
        {
            // A 16-bit sample always consists of two bytes.
            // If the server somehow sent an odd number of bytes, ignore the
            // final incomplete byte (should not happen..).
            const completeByteLength =
                pendingBytes.byteLength
                    - (pendingBytes.byteLength % play.bytesPerSample);

            const finalBlock = pendingBytes.slice(0, completeByteLength);

            nextPlayTime = scheduleBlock(finalBlock, nextPlayTime, true);
        }
    }
    catch(err)
    {
        console.log(
            `Error: Receiving and/or playback failed with msg. "${err}"!`);

        setButIdle(); // TODO: Show error msg. to user instead!
    }
    finally
    {
        resolveReceivalFinished();
        receivalFinished = Promise.resolve();
        resolveReceivalFinished = () => {};

        if(!didReceiveAudio) // In case no audio was received.
        {
            setButIdle();
        }
    }
};

/**
 * - Called, if access to the microphone is possible/allowed.
 * - Sets "global" chunks to a new empty array on record start.
 */
function mediaStreamHandler(mediaStream)
{
    const mediaRecorder = new MediaRecorder(
        mediaStream,
        {
            audioBitsPerSecond: 128000 // Workaround for "silence" bug in FF157.
        });
    const topDivEle = document.createElement('div');
    const botDivEle = document.createElement('div');
    const canvasEle = document.createElement('canvas');

    // To be able to let the button use the whole space:
    //
    document.documentElement.style.width = '100%';
    document.documentElement.style.height = '100%';
    //
    document.body.style.width = '100%';
    document.body.style.height = '100%';
    document.body.style.margin = '0';

    topDivEle.style.width = '100%';
    topDivEle.style.height = '50%';
    topDivEle.style.display = 'grid';
    topDivEle.style['place-items'] = 'center';
    topDivEle.style.background = 'black';
    document.body.appendChild(topDivEle);

    botDivEle.style.width = '100%';
    botDivEle.style.height = '50%';
    document.body.appendChild(botDivEle);

    // Voice box:
    canvasEle.width = canvasWidth;
    canvasEle.height = canvasHeight;
    canvasEle.style.background = 'black';
    canvasEle.style.height = '100%';
    g = canvasEle.getContext('2d');
    topDivEle.appendChild(canvasEle);
    canvasEle.onclick = () =>
    {
        colorIndex = (++colorIndex) % 3; // Hard-coded length!
        drawCanvas();
    };

    butEle = document.createElement('button');
    botDivEle.appendChild(butEle);
    butEle.onclick = async () =>
    {
        if(mediaRecorder.state === 'inactive')
        {
            setButCancel();

            await sendCancel(); // Tells the server to cancel.

            await receivalFinished; // Waits for receival to actually finish.

            // Play back audio as long as the server did not finish sending
            // (some stuff cannot be canceled, e.g. the classifier and message
            // import).
            if(audioCtx !== null)
            {
                await audioCtx.close(); // Stops audio playback.
                audioCtx = null;

                analyser.disconnect();
                analyser = null;
                smooth = 0.0;
                isPlaying = false; // Necessary?
            }

            chunks = [];
            
            setButPrepareForRec();

            try
            {
                mediaRecorder.start();
            }
            catch(err)
            {
                console.log(
                    `Error: Failed to start recording with msg. "${err}"!`);
                document.body.innerText = 'Failed to start recording.';
            }

            return;
        }

        if(mediaRecorder.state === 'recording')
        {
            setButStoppingRec();
            mediaRecorder.stop();
            return;
        }

        throw `Unsupported media recorder state "${mediaRecorder.state}"!`;
    };
    butEle.style.width = '100%';
    butEle.style.height = '100%';
    butEle.style['font-size'] = '10cqmin';
    setButIdle();

    // Voice box:
    drawCanvas();

    mediaRecorder.onstart = e =>
    {
        setButRec();
    }
    mediaRecorder.ondataavailable = e => chunks.push(e.data);
    mediaRecorder.onstop = onRecordStop;
};

/**
 * - To-be-called after page load.
 */
async function init()
{
    let mediaStream = null;

    if (!window.isSecureContext)
    {
        console.log('Error: The current context is not secure!');
        document.body.innerText = 'The current context is not secure.';
        return;
    }

    try
    {
        mediaStream = await window.navigator.mediaDevices.getUserMedia(
            { audio: true });
    }
    catch(err)
    {
        console.log(`Error: Failed to get user media with msg. "${err}"!`);
        document.body.innerText = 'Failed to get microphone access.';
        return;
    }

    // Does not necessarily work on a mobile device.
    window.addEventListener('beforeunload', event => event.preventDefault());

    mediaStreamHandler(mediaStream);
};

window.addEventListener('load', init); // Triggers execution of initialization.