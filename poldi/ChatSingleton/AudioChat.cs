
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

// RhinoDevel, Marcel Timm, 2025nov15

using Poldi.CppInterface;
using Poldi.CppInterface.MtLlm;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Poldi.ChatSingleton
{
    /// <summary>
    /// Singleton to handle chatting with the LLM via speech-to-text input and
    /// text-to-speech output.
    /// </summary>
    /// <remarks>
    /// Uses the singleton <see cref="ChatSingleton"/>.
    /// </remarks>
    internal static class AudioChat
    {
        private static int _playbackRate = 22050; // Hard-coded. See client.

        private static int? _ttsRateVal = null;
        private static int _ttsRate
        {
            get
            {
                if (_ttsRateVal == null)
                {
                    using var doc = JsonDocument.Parse(
                        File.ReadAllText(C.D<string>("tts:configPath")));

                    // This is hard-coded for Piper TTS usage.
                    _ttsRateVal = doc.RootElement
                                     .GetProperty("audio")
                                     .GetProperty("sample_rate")
                                     .GetInt32();

                    Debug.Assert(_ttsRateVal != null);
                }
                return _ttsRateVal.Value;
            }
        }

        private static BinaryWriter? _writer = null;

        private static string _transferBuf = string.Empty;

        private static Action<bool>? _setReqImmedUserResp = null;

        //private static double _audioFilter_RingModulation_phase = 0.0;

        /// <remarks>
        /// Will (re-)set <see cref="_setReqImmedUserResp"/> to null during
        /// first call, because that action shall be called one time, only.
        /// </remarks>
        private static bool Send(string trimmedText, bool reqImmedUserResp)
        {
            Debug.Assert(trimmedText.Length != 0);
            Debug.Assert(MtTts.IsInit);

            List<short> samples;

            // TODO: Use another thread (to keep LLM inferring?)!?
            
            samples = MtTts.ToRaw(trimmedText);

            // If a model with 16000 Hz instead of 22050 Hz sampling rate is
            // used for TTS, automatically rescale.
            if(_ttsRate != _playbackRate)
            {
                Debug.Assert(_playbackRate == 22050);
                if (_ttsRate == 16000)
                {
                    samples = Audio.ResampleLinear(samples);
                }
                else
                {
                    Debug.Assert(false); // Should not get here with Piper TTS.
                    Log.Line($"Warning: Unsupported TTS sampling rate of {_ttsRate} Hz (playback assumes {_playbackRate} Hz).");
                }
            }

            //AudioFilter.RectifyHalf(samples);
            //AudioFilter.RingModulation(
            //    300,
            //    (double)_playbacRate/*22050.0,
            //    ref _audioFilter_RingModulation_phase,
            //    samples);

            if(_setReqImmedUserResp != null)
            {
                _setReqImmedUserResp(reqImmedUserResp);
                _setReqImmedUserResp = null; // To be called once, only.
            }

            foreach (short sample in samples) // TODO: Simplify?
            {
                var bytes = BitConverter.GetBytes(sample);

                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(bytes);
                }

                try // In case the connection got closed/lost.
                {
                    _writer!.Write(bytes);
                }
                catch (Exception e)
                {
                    Log.Line($"Error: Exception with message \"{e.Message}\" during audio-send attempt! Returning with error..");
                    return false;
                }
            }
            _writer!.Flush();
            return true;
        }

        /// <remarks>
        /// Will clear the transfer buffer <see cref="_transferBuf"/>, if
        /// <paramref name="p"/>'s <see cref="CallbackParams.ForceOutput"/> is
        /// set to true.
        /// </remarks>
        private static bool OnSampledNonEogNonCtrl(CallbackParams p)
        {
            if(p.ForceOutput)
            {
                Debug.Assert(p.Piece.Length == p.Piece.Trim().Length);
                Debug.Assert(0 < p.Piece.Length);

                Log.Line($"Forced-output text to TTS (also clearing transfer buffer): \"{p.Piece}\"");
                _transferBuf = string.Empty;

                if(!Send(p.Piece, p.ReqImmedUserResp))
                {
                    // TODO: Can happen, if the connection to the client is lost!
                    Debug.Assert(false);
                    return false;
                }
                return true;
            }

            Debug.Assert(p.LastText.EndsWith(p.Piece)); // To clarify this.

            _transferBuf += p.Piece;

            // TODO: Make sure that the last inferred text is always send,
            //       even, if it does not end with one of the punctuation
            //       marks!

            // TODO: Improve detection!
            if (_transferBuf.TrimEnd().EndsWith('.') // TODO: Skip, if preceded by a number!
                || _transferBuf.TrimEnd().EndsWith('!')
                || _transferBuf.TrimEnd().EndsWith('?')
                || _transferBuf.TrimEnd().EndsWith(':'))
            {
                // Send!

                // TODO: Hard-coded, maybe put in config. file?
                var replacedText = _transferBuf.Replace("**", "");

                _transferBuf = string.Empty;

                var trimmedText = replacedText.Trim();

                //Log.Line($"Trimmed(!) text to TTS: \"{trimmedText}\"");

                if (!Send(trimmedText, p.ReqImmedUserResp))
                {
                    Debug.Assert(false); // Must not happen.
                    return false;
                }
            }
            return true; // Do not send, yet.
        }

        /// <param name="setReqImmedUserResp">
        /// To be called once, before <paramref name="writer"/> is fed the
        /// first time, to signalize the client, if it shall request an
        /// immediate response from the user, without waiting for the user
        /// saying the wakeword, first.
        /// </param>
        /// <remarks>
        /// Modifies given <paramref name="p"/>'s
        /// <see cref="ChatSingletonParams.OnSampledNonEogNonCtrl"/>, at least
        /// for this method's runtime.
        /// </remarks>
        internal static ChatSingletonResult Query(
            ChatSingletonParams p,
            BinaryWriter writer,
            Action<bool> setReqImmedUserResp)
        {
            Debug.Assert(MtTts.IsInit);

            Debug.Assert(writer != null);
            Debug.Assert(_writer == null);
            Debug.Assert(_transferBuf == string.Empty);
            Debug.Assert(_setReqImmedUserResp == null);

            ChatSingletonResult retVal;

            // Trick to let Mem and Sel use the chat slot from the start.
            Debug.Assert(p.SlotIndex == (int)SlotIndex.Chat);
            if (p.KeepLlmLoaded && !MtLlm.IsInit(p.SlotIndex))
            {
                MtLlm.ReInit(p.ParamsGiven, p.SlotIndex);
            }

            _writer = writer;
            _setReqImmedUserResp = setReqImmedUserResp;
            Debug.Assert(p.OnSampledNonEogNonCtrl == null);
            p.OnSampledNonEogNonCtrl = OnSampledNonEogNonCtrl; // Augments
            retVal = ChatSingleton.Query(p);
            p.OnSampledNonEogNonCtrl = null;
            _writer = null;
            _transferBuf = string.Empty;
            _setReqImmedUserResp = null; // (although should already be null).
            return retVal;
        }
    }
}
