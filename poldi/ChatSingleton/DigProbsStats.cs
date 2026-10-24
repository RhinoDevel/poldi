
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

// RhinoDevel, Marcel Timm, 2025nov14

using System.Diagnostics;
using System.Linq;

namespace Poldi.ChatSingleton
{
    internal class DigProbsStats
    {
        /// <summary>
        /// The original probabilities (not logits) of the digits, but as
        /// <see langword="double" /> values.
        /// </summary>
        internal readonly double[] Probs;

        internal DigProbsStats(float[] digProbs)
        {
            Debug.Assert(digProbs != null && digProbs.Length == 10);

            Probs = digProbs.Select(dp => (double)dp).ToArray();
        }

        /// <returns>
        /// The maximum value's index into <see cref="Probs"/>, while ignoring
        /// all entries before <paramref name="from"/> index and after
        /// <paramref name="from"/> + <paramref name="len"/> index.
        /// </returns>
        internal int GetMaxIndex(int from, int len)
        {
            Debug.Assert(0 <= from);
            Debug.Assert(2 <= len); // 0 and 1 wouldn't make sense.
            Debug.Assert(from + len <= Probs.Length);

            int retVal = from;
            double maxVal = Probs[retVal];

            for(int i = from + 1; i < from + len; ++i)
            {
                if (maxVal < Probs[i])
                {
                    retVal = i;
                    maxVal = Probs[i];
                }
            }
            return retVal;
        }

        /// <returns>
        /// The sum of all values in <see cref="Probs"/>, while ignoring all
        /// values before <paramref name="from"/> index and after
        /// <paramref name="from"/> + <paramref name="len"/> index.
        /// </returns>
        internal double GetSum(int from, int len)
        {
            Debug.Assert(0 <= from);
            Debug.Assert(2 <= len); // 0 and 1 wouldn't make sense.
            Debug.Assert(from + len <= Probs.Length);

            return Probs.Take(new System.Range(from, from + len)).Sum();
        }

        internal double[]? GetProbsNormalized(int from, int len)
        {
            double sum = GetSum(from, len);

            return Probs
                .Take(new System.Range(from, from + len))
                .Select(prob => prob / sum)
                .ToArray();
        }

        internal double? GetConfidence(int from, int len)
        {
            var probsNormalized = GetProbsNormalized(from, len);

            if(probsNormalized == null)
            {
                return null;
            }

            Debug.Assert(probsNormalized.Length == len);

            return probsNormalized.Max();
        }
    }
}
