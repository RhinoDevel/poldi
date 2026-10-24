
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

// Marcel Timm, RhinoDevel, 2025nov10

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Poldi
{
    internal static class Calc
    {
        private const double _bitsLogBase = 2.0; // 2 for bits.

        /// <param name="normalized">
        /// The values' sum is expected to be 1.0, all values must be zero or
        /// positive.
        /// </param>
        internal static double GetShannonEntropyBits(IList<double> normalized)
        {
            // To get a return value in bits.
            Debug.Assert(_bitsLogBase == 2.0);

            Debug.Assert(normalized.All(val => 0.0 <= val));

            return -1.0
                * normalized
                    .Select(
                        val =>
                            val == 0.0
                                ? 0.0
                                : val * Math.Log(val, _bitsLogBase))
                    .Sum();
        }

        internal static double GetShannonRedundancyBitsFromNormalized(
            IList<double> normalized)
        {
            Debug.Assert(_bitsLogBase == 2.0);

            return 1.0
                - GetShannonEntropyBits(normalized)
                    / Math.Log((double)normalized.Count, _bitsLogBase);
        }

        /// <remarks>
        /// Not performance-optimized.
        /// </remarks>
        internal static float GetDotProd(float[] a, float[] b)
        {
            Debug.Assert(a.Length == b.Length);

            float retVal = 0.0f;

            for(int i = 0; i < a.Length; ++i)
            {
                retVal += a[i] * b[i];
            }
            return retVal;
        }

        /// <remarks>
        /// Not performance-optimized (for a lot of use cases the sorting of the
        /// whole list can be replaced by keeping track of the n higest
        /// results).
        /// </remarks>
        internal static List<Tuple<int, float>> GetDescDotProdIndices(
            float[] a, List<float[]> b)
        {
            var l = new List<Tuple<int, float>>();

            for(int i = 0; i < b.Count; ++i)
            {
                l.Add(new Tuple<int, float>(i, GetDotProd(a, b[i])));
            }

            return l.OrderByDescending(t => t.Item2)
                .ToList();
        }

        /// <returns>
        /// Result of the softargmax (normalized exponential) function.
        /// </returns>
        /// <remarks>
        /// Assumes that all values in <paramref name="z"/> are finite (see
        /// <see cref="double.IsFinite"/>).
        /// </remarks>
        internal static List<double> GetSoftmax(List<double> z)
        {
            Debug.Assert(z.TrueForAll(double.IsFinite));

            double max; // Maximum value of given values.
            List<double> exp; // Shifted exponentials of given values.
            double sum; // Sum of shifted exponentials.
            List<double> softmax; // The softmax result values.

            if(z.Count == 0)
            {
                return []; // Because z.Max() will throw an exception w/o this.
            }

            max = z.Max();
            exp = new(z.Count);
            sum = 0.0;
            for(int i = 0; i < z.Count; ++i)
            {
                // Subtract maximum z value to avoid overflow of exp() result
                // (softmax is invariant to constant shifts by doing that the
                // maximum value to calc. exponential from will be 0 then).
                var expI = Math.Exp(z[i] - max);

                exp.Add(expI);
                sum += expI;
            }
            Debug.Assert(1.0 <= sum); // Must be true, because of shift, above.

            Debug.Assert(exp.TrueForAll(double.IsFinite));
            Debug.Assert(double.IsFinite(sum));

            softmax = exp.Select(expI => expI / sum).ToList();

            Debug.Assert(softmax.TrueForAll(double.IsFinite));

            return softmax;
        }

        /// <returns>
        /// True if the list is empty, contains a single element, or is sorted
        /// in ascending order.
        /// </returns>
        internal static bool IsOrderedAsc(List<double> l)
        {
            for(int i = 1; i < l.Count; ++i)
            {
                if (l[i] < l[i - 1])
                {
                    return false;
                }
            }
            return true;
        }

        /// <remarks>
        /// Expects the given <paramref name="ordAsc"/> values to be sorted,
        /// ascending (top mass at the highest indices).
        /// </remarks>
        internal static double GetTopKSum(List<double> ordAsc, int topK)
        {
            Debug.Assert(IsOrderedAsc(ordAsc));
            Debug.Assert(0 <= topK && topK <= ordAsc.Count);

            return ordAsc.TakeLast(topK).Sum();
        }

        /// <remarks>
        /// Expects the given <paramref name="ordAsc"/> values to be sorted,
        /// ascending (top mass at the highest indices).
        /// </remarks>
        internal static double GetTopPercentSum(
            List<double> ordAsc, int topPercent)
        {
            Debug.Assert(0 <= topPercent && topPercent <= 100);

            // topPercent      topK
            //     /      =      /
            //    100       ordAsc.Count

            // Truncates.
            int topK = ordAsc.Count * topPercent / 100;

            return GetTopKSum(ordAsc, topK);
        }
    }
}
