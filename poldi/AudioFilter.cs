
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

// Marcel Timm, RhinoDevel, 2025nov13

using System;
using System.Collections.Generic;

namespace Poldi
{
    /// <remarks>
    /// These were implemented to be used to alter the TTS voice (for fun).
    /// </remarks>
    internal static class AudioFilter
    {   
        internal static void RectifyFull(List<short> samples)
        {
            for (int i = 0; i < samples.Count; ++i)
            {
                samples[i] = Math.Abs(samples[i]); // Full-wave rectification.
            }
        }

        internal static void RectifyHalf(List<short> samples)
        {
            for (int i = 0; i < samples.Count; ++i)
            {
                // Half-wave rectification.
                samples[i] = samples[i] < 0
                    ? (short)0
                    : samples[i];
            }
        }

        internal static void RingModulation(
            double freqHz,
            double sampleRateHz,
            ref double phase,
            List<short> samples)
        {
            double phaseInc = 2.0 * Math.PI * freqHz / sampleRateHz;

            for (int i = 0; i < samples.Count; ++i)
            {
                samples[i] = (short)((double)samples[i] * Math.Sin(phase));

                phase += phaseInc;
                if (phase > 2.0 * Math.PI)
                {
                    phase -= 2.0 * Math.PI;
                }
            }
        }
    }
}
