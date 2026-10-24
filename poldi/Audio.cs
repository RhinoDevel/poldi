
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

// Marcel Timm, RhinoDevel, 2026sep14

using System;
using System.Collections.Generic;
using System.Linq;

namespace Poldi
{
    internal static class Audio
    {
        /// <remarks>
        /// Fixed for 16000 Hz input an 22050 Hz output sampling rates.
        /// </remarks>
        private const double resampleRatio = 22050.0 / 16000.0;

        /// <summary>
        /// Resample input, which is assumed to have a sampling rate of
        /// 16000 Hz, to a sampling rate of 22050 by linear interpolation.
        /// </summary>
        internal static List<short> ResampleLinear(List<short> input)
        {
            int outLen = (int)Math.Round((double)input.Count * resampleRatio);
            var output = new List<short>(outLen);

            for (int i = 0; i < outLen; ++i)
            {
                var dInI = (double)i / resampleRatio;
                int inI = (int)dInI;
                double frac;
                double dVal;

                if (input.Count - 1 <= inI)
                {
                    output.Add(input.Last());
                    continue;
                }

                frac = dInI - (double)inI;
                dVal = (1.0 - frac) * (double)input[inI]
                        + frac * (double)input[inI + 1];

                output.Add((short)Math.Round(dVal));
            }
            return output;
        }

    }
}
