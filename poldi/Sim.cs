
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

// Marcel Timm, RhinoDevel, 2026aug14

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Poldi
{
    /// <summary>
    /// "Sim" as in SIMilarity.
    /// 
    /// Singleton to offer functionality to get the texts out of a given list of
    /// strings that are most similar to a special given text, by using cosine
    /// similarity score comparance of the texts' embeddings and/or a reranking
    /// model.
    /// </summary>
    internal static class Sim
    {
        internal static readonly bool _useReranking =
            C.D<bool>("sim:useReranking");
        internal static readonly int _maxRerankDocuments =
            C.D<int>("sim:maxRerankDocuments");

        /// <returns>
        /// null on error. List of most similar documents' indices into
        /// <paramref name="documents"/> otherwise (sorted by descending order,
        /// <see cref="SimDoc"/> with most similar text first).
        /// </returns>
        internal static List<int>? GetOrdMostSimIndices(
            string query, List<SimDoc> documents, int maxOutputCount)
        {
            Debug.Assert(0 < query.Trim().Length);
            Debug.Assert(
                documents.TrueForAll(
                    d => 0 < d.Text.Trim().Length && 0 < d.Emb.Length));
            Debug.Assert(0 < maxOutputCount);

            List<Tuple<int, string>> indexAndTexts;
            float[]? rerankScores;
            List<Tuple<int, float>> indexAndRerankScores; 

            if(!_useReranking || _maxRerankDocuments < documents.Count)
            {
                // Either reranking scores shall not be used or more documents
                // than reranking module's maximum input count given.
                // => Cosine similarity score of embeddings filter stage is
                //    necessary.

                float[]? queryEmb;
                List<Tuple<int, float>> descDotProdIndices;

                queryEmb = Emb.Create(query);
                if(queryEmb == null)
                {
                    return null; // (called method already logged)
                }

                Debug.Assert(
                    documents.TrueForAll(d => d.Emb.Length == queryEmb.Length));

                descDotProdIndices = Calc.GetDescDotProdIndices(
                    queryEmb, documents.Select(d => d.Emb).ToList());

                indexAndTexts = descDotProdIndices
                    .Take(_useReranking ? _maxRerankDocuments : maxOutputCount)
                    .Select(
                        t => new Tuple<int, string>(
                            t.Item1, documents[t.Item1].Text))
                    .ToList();

                if(!_useReranking)
                {
                    return indexAndTexts
                        .Select(t => t.Item1)
                        .ToList();
                }
            }
            else
            {
                // Cosine similarity score of embeddings filter stage is not
                // necessary.

                indexAndTexts = new();
                for(int i = 0; i < documents.Count; ++i)
                {
                    indexAndTexts.Add(new(i, documents[i].Text));
                }
            }

            // Get the reranker (similarity) scores.

            rerankScores = Rer.Create(
                query, indexAndTexts.Select(t => t.Item2).ToList());
            if (rerankScores == null)
            {
                return null; // (called method already logged)
            }

            Debug.Assert(rerankScores.Length == indexAndTexts.Count);

            indexAndRerankScores = new();
            for(int i = 0; i < rerankScores.Length; ++i)
            {
                indexAndRerankScores.Add(
                    new(indexAndTexts[i].Item1, rerankScores[i]));
            }

            return indexAndRerankScores
                // Order by rerank scores, top score first.
                .OrderByDescending(t => t.Item2)
                // Keep at most given max. count of output documents.
                .Take(maxOutputCount)
                // Return the indices into the given documents list, only.
                .Select(t => t.Item1)
                .ToList();
        }
    }
}
