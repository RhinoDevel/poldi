
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

// Marcel Timm, RhinoDevel, 2026jun12

using Poldi.CppInterface.MtLlm;
using System.Collections.Generic;
using System.Diagnostics;

namespace Poldi
{
    /// <summary>
    /// Singleton to be used to create embedding vectors representing given
    /// text(-s).
    /// </summary>
    /// <remarks>
    /// Assumes that the slot with index <see cref="_slotIndex"/> is only
    /// used by this singleton (no check, if parameters are equal).
    /// </remarks>
    internal static class Emb
    {
        private static readonly bool _keepLoaded =
            C.D<bool>("llm:emb:keepLoaded");
        private const int _slotIndex = (int)SlotIndex.Emb;

        private static readonly MtLlmP _p;

        static Emb()
        {
            _p = LlmPCreate.FromConfigEmb();
        }

        private static bool IsInit() => MtLlm.IsInit(_slotIndex);

        private static bool Init()
        {
            if (!MtLlm.ReInit(_p, _slotIndex))
            {
                Log.Line("Error: Failed to (re-)initialize!");
                return false;
            }
            return true;
        }

        /// <remarks>
        /// Assumes that the slot is already correctly initialized.
        /// </remarks>
        private static float[]? CreateTrusted(string text)
        {
            Debug.Assert(IsInit());

            var retVal = MtLlm.CreateEmbeddings(text, _slotIndex);

            if (retVal == null)
            {
                Log.Line("Error: Failed to create embeddings!");
            }
            return retVal; // May be null, here.
        }

        /// <remarks>
        /// Useful, if keep-loaded is wanted/configured, but the memory needs to
        /// get freed for something else.
        /// 
        /// Just does nothing, if not initialized.
        /// </remarks>
        internal static void DeInit()
        {
            MtLlm.DeInit(_slotIndex);
        }

        internal static string GetModelFilePath() => _p.model_file_path!;

        internal static int? GetMaxTokenCount()
        {
            int maxTokenCount;

            if (!IsInit()) // Initialize, if not already done.
            {
                if (!Init())
                {
                    return null; // (called method logged)
                }
            }

            // If keep-loaded is wanted/configured:
            //
            // - As we are assuming that the slot is only used from here and
            //   because of that, the parameters are correct, we do not need
            //   to re-initialize.

            maxTokenCount = MtLlm.GetMaxTokenCount(_slotIndex);

            if (!_keepLoaded)
            {
                DeInit();
            }

            return maxTokenCount;
        }

        /// <summary>
        /// Configure <see cref="_keepLoaded"/> and the embedding stuff in the
        /// config file and just use this method and don't care about anything
        /// else.
        /// </summary>
        /// <remarks>
        /// Logs, if an error happened.
        /// </remarks>
        internal static float[]? Create(string text)
        {
            Debug.Assert(_keepLoaded || !IsInit());

            float[]? vec;

            if (!IsInit()) // Initialize, if not already done.
            {
                if (!Init())
                {
                    return null; // (called method logged)
                }
            }

            // If keep-loaded is wanted/configured:
            //
            // - As we are assuming that the slot is only used from here and
            //   because of that, the parameters are correct, we do not need
            //   to re-initialize.
            //
            // - We also do not need to reset, because mt_llm will do that for
            //   us when using the embedding creation functionality.

            vec = CreateTrusted(text); // (logs on error)

            if (!_keepLoaded)
            {
                DeInit();
            }

            return vec; // May be null, here.
        }

        /// <summary>
        /// Configure <see cref="_keepLoaded"/> and the embedding stuff in the
        /// config. file and just use this method and don't care about anything
        /// else.
        /// </summary>
        /// <remarks>
        /// Logs, if an error happened.
        /// </remarks>
        internal static List<float[]>? Create(List<string> texts)
        {
            List<float[]>? l;

            if (!IsInit()) // Initialize, if not already done.
            {
                if (!Init())
                {
                    return null; // (called method logged)
                }
            }

            // If keep-loaded is wanted/configured:
            //
            // - As we are assuming that the slot is only used from here and
            //   because of that, the parameters are correct, we do not need
            //   to re-initialize.
            //
            // - We also do not need to reset, because mt_llm will do that for
            //   us when using the embedding creation functionality.

            l = new List<float[]>();
            foreach(var text in texts)
            {
                var vec = CreateTrusted(text); // (logs on error)

                if(vec == null)
                {
                    l = null;
                    break;
                }
                l.Add(vec);
            }

            if (!_keepLoaded)
            {
                DeInit();
            }
            return l; // May be null, here.
        }
    }
}
