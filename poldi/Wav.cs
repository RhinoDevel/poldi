
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

// Marcel Timm, RhinoDevel, 2025mar18

using System;
using System.Diagnostics;

namespace Poldi
{
    /// <summary>
    /// Singleton offering WAV-related helper methods.
    /// </summary>
    internal static class Wav
    {
        private const int _chunkIdLen = 4;
        private const int _chunkSizeLen = 4;

        private const int _chunkMinLen = _chunkIdLen + _chunkSizeLen;

        private const int _chunkLenMasterRiff = 4 + 4 + 4;

        private const string _chunkIdData = "data";

        private static int FindChunk(byte[] wavData, string chunkId)
        {
            Debug.Assert(0 < chunkId.Length);

            int i = _chunkLenMasterRiff; // Skips master RIFF chunk.

            while (i < wavData.Length - _chunkMinLen)
            {
                var curChunkId = System.Text.Encoding.ASCII.GetString(
                        wavData, i, _chunkSizeLen);

                if (curChunkId == chunkId)
                {
                    return i; // Found chunk with given/wanted ID.
                }

                i += _chunkMinLen + BitConverter.ToInt32(
                        wavData, i + _chunkIdLen);
            }
            return -1;
        }

        private static int FindChunkData(byte[] wavData)
        {
            return FindChunk(wavData, _chunkIdData);
        }

        /// <summary>
        /// Get raw audio data from given WAV data.
        /// </summary>
        /// <remarks>
        /// Hard-coded for one signed short integer per sample, mono.
        /// 
        /// Also normalizes to range -1.0 to almost 1.0.
        /// </remarks>
        internal static float[]? CreateAudioData(byte[] wavData)
        {
            int chunkDataIndex, dataSize, dataIndex, sampleCount;
            float[] audioData;

            chunkDataIndex = FindChunkData(wavData);
            if(chunkDataIndex == -1)
            {
                return null; // Not found.
            }

            dataSize = BitConverter.ToInt32(
                wavData, chunkDataIndex + _chunkIdLen);

            Debug.Assert(dataSize % 2 == 0);

            sampleCount = dataSize / 2; // 16-bit samples.
            
            audioData = new float[sampleCount];

            dataIndex = chunkDataIndex + _chunkMinLen;

            for (int i = 0; i < sampleCount; ++i)
            {
                var sample = BitConverter.ToInt16(wavData, dataIndex + 2 * i);

                audioData[i] = (float)sample / 32768.0f; // Normalizes
            }
            return audioData;
        }
    }
}
