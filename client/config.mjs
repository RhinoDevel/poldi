
// Marcel Timm, RhinoDevel, 2025jun20

import path from 'node:path';

export default {
    // The URL of the server.
    serverUrl: 'http://xmg.fritz.box:8080/',

    // The different paths used (after the server URL).
    request: {
        audioToAudio: 'audioToAudio',
        cancel: 'cancel'
    },

    // Absolute path of the (this) client application's (root) folder.
    clientAbsPath: path.join('/', 'home', 'marc', 'Documents', 'poldi', 'client'),

    audio: {
        play: { // Settings for module audioPlay.
            // These are not used (see audioPlay.mjs, also the comment there).
            //card: '0',
            //dev: '0',

            // For playback of raw audio (e.g. from stream).
            channels: 1,
            bitDepth: 16,
            sampleRate: 22050,
            endian: 'L'/*'B'*/
            // Signed integer samples are implied.
        },

        rec: { // Settings for module audioRec.
            // These are not used (see audioRec.mjs, also the comment there).
            //card: '2',
            //dev: '0',

            // For recording of raw audio:
            channels: 1,
            bitDepth: 16,
            sampleRate: 16000,
            endian: 'L'/*'B'*/,
            // Signed integer samples are implied.
        }
    },

    // Subfolder for sound files (root is .clientAbsPath).
    soundFolder: 'sounds',
    
    // Sound file names.
    soundFiles: {
        wakeWordDetected: 'wake_word_detected.wav',
        processing: 'processing.wav'
    },

    // File to store latest voice (command) recording (root is .clientAbsPath).
    voiceCmdRecFile: 'voice_cmd_rec.wav',

    voiceRec: {
        silenceSeconds: 5,
        maxSeconds: 30
    },

    // Wake word model file's path (root is .clientAbsPath).
    wakeWordModelFile: path.join(
        'ext', 'openWakeWord-cpp', 'models', 'hey_jarvis_v0.1.onnx')
    //
    // Alternatively use "Hey Poldi", although the wake word detection rate may
    // not be as good as with the models from the openWakeWord-cpp repository:
    //wakeWordModelFile: path.join('wakeword', 'hey_poldi.onnx')
};