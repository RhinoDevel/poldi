
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
using System.Linq;

namespace Poldi
{
    /// <summary>
    /// To test the classifier ("select") mode.
    /// </summary>
    internal static class SelQueryTest
    {
        private class OptionAndQueries
        {
            /// <summary>
            /// To each of the <see cref="Queries"/>, this is the correct
            /// answer.
            /// </summary>
            internal readonly string Option;

            /// <summary>
            /// The <see cref="Option"/> is the correct answer to all of these
            /// queries.
            /// </summary>
            internal readonly string[] Queries;

            internal OptionAndQueries(string option, string[] queries)
            {
                Option = option;
                Queries = queries;
            }
        };

        private static readonly string _question =
            "What is requested by saying \"{0}\"?";
        private static readonly OptionAndQueries[] _options = [
                new (
                    "A game",
                    [
                        "Let's play a game!",
                        "Let's have us some poker!"
                    ]),
                new (
                    "Music",
                    [
                        "Play Blink-182!",
                        "Let's have us some AC/DC!",
                        "I want some Blues, please.",
                        "Let's listen to the Stones.",
                        "Play Roll Over Beethoven by Chuck Berry.",
                        "Beethoven, please.",

                        "play blink 1 82",
                        "let's have us some AC DC",
                        
                        "Spiele AC/DC!",
                        "Mach Musik an!",
                        "Kannst Du bitte Musik einschalten?",
                        "Ich möchte Schlager hören.",
                        "Lass uns der Musik lauschen!"
                    ]),
                new (
                    "A chat",
                    [
                        "Hi there!",
                        "Hello.",
                        "How are you today?",
                        "How are you?",
                        "How do you do?",
                        "I have some good news.",
                        "Let's talk about my work.",
                        "I'd like to chat a bit.",
                        "What do you want to talk about?",

                        "Hallo",
                        "Wie gehts?",
                        "Guten Morgen!",
                        "Hast Du Zeit?"
                    ]),
                new (
                    "The current date and time",
                    [
                        "What is the date?",
                        "Tell me the time, please.",

                        "Wie spät ist es?",
                        "Hast Du die Zeit?",
                        "Was haben wir heute?",
                        "Welcher Tag ist?",

                        "Was die Zeit?",
                        "Was die Uhrzeit?"
                    ]),
                new (
                    "The current internet IP address",
                    [
                        "What is our external IP?",
                        "What's our internet address?",

                        "Welche externe Internet-Adresse haben wir?",
                        "Welche IP haben wir?"
                    ]),
                new (
                    "Other information",
                    [
                        "Who killed JFK?",
                        "When did Sean Connery die?",
                        "Was Master Splinter initially a rat or a human?",
                        "Why is the sky blue?",
                        "When was Star Wars released?",

                        "Was die Urzeit?"
                    ])
            ];

        internal static string Exec()
        {
            string retVal;
            var options = _options.Select(o => o.Option).ToArray();
            int correctAnswer = 0;

            retVal = "Query";
            retVal += ";";
            retVal += "Correct answer";
            retVal += ";";
            retVal += "Given answer";
            retVal += ";";
            retVal += "Correctly answered";
            retVal += ";";
            retVal += "Sum of relevant digit probabilities";
            retVal += ";";
            retVal += "Maximum overall probability of relevant digits";
            retVal += ";";
            retVal += "Confidence considering relevant digits";

            retVal += Environment.NewLine;

            foreach (var o in _options)
            {
                ++correctAnswer;

                foreach (var query in o.Queries)
                {
                    var digProbsStats = Sel.Create(
                            string.Format(_question, query),
                            options,
                            null,
                            true,
                            false); // <- Do NOT de-initialize select slot.
                    int maxIndex =
                            digProbsStats!.GetMaxIndex(1, options.Length);

                    // Query:
                    retVal += query;

                    // Correct answer:
                    retVal += ";";
                    retVal += o.Option;

                    // Given answer:
                    retVal += ";";
                    retVal += _options[maxIndex - 1] // <- Index into options.
                        .Option;

                    // Correctly answered?
                    retVal += ";";
                    retVal += maxIndex == correctAnswer ? "Yes" : "No";

                    // Sum of relevant digit probabilities:
                    retVal += ";";
                    retVal += digProbsStats
                        .GetSum(1, options.Length)
                        .ToString();

                    // Maximum overall probability of relevant digits:
                    retVal += ";";
                    retVal +=
                        digProbsStats.Probs[maxIndex] // <- Index into probs.
                            .ToString();

                    // Confidence considering relevant digits:
                    retVal += ";";
                    retVal += digProbsStats
                        .GetConfidence(1, options.Length)
                        .ToString();

                    retVal += Environment.NewLine;
                }
            }
            Sel.DeInit(); // In case select slot was used.
            retVal = retVal.Trim();
            return retVal;
        }
    }
}
