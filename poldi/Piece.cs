
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

using Poldi.CppInterface.MtLlm;
using System;

namespace Poldi
{
    /// <summary>
    /// Offers a helper method to write a string to console with the foreground
    /// and background color chosen for the (also given) token type.
    /// </summary>
    internal static class Piece
    {
        private static ConsoleColor _defColorBg;
        private static ConsoleColor _defColorFg;

        static Piece()
        {
            _defColorBg = Console.BackgroundColor;
            _defColorFg = Console.ForegroundColor;
        }

        private static Tuple<ConsoleColor, ConsoleColor> GetColorsFor(
            MtLlmTokType t)
        {
            switch (t)
            {
                case MtLlmTokType.PROMPT:
                {
                    return new(ConsoleColor.White, ConsoleColor.Green);
                }
                case MtLlmTokType.IRQ:
                {
                    return new(ConsoleColor.White, ConsoleColor.Red);
                }
                case MtLlmTokType.SAMPLED_NON_EOG_NON_CONTROL:
                {
                    return new(ConsoleColor.White, ConsoleColor.Blue);
                }
                case MtLlmTokType.SAMPLED_EOG:
                {
                    return new(ConsoleColor.Black, ConsoleColor.Yellow);
                }
                case MtLlmTokType.DELIM:
                {
                    return new(ConsoleColor.Black, ConsoleColor.Cyan);
                }
                case MtLlmTokType.SYS_PROMPT:
                {
                    return new(ConsoleColor.White, ConsoleColor.Magenta);
                }
                case MtLlmTokType.SAMPLED_CONTROL_NON_EOG:
                {
                    return new(ConsoleColor.Black, ConsoleColor.Gray);
                }
                case MtLlmTokType.MT_TOK_TYPE_THINK_BEGIN:
                {
                    return new(ConsoleColor.Green, ConsoleColor.Black);
                }
                case MtLlmTokType.MT_TOK_TYPE_THINK_TEXT:
                {
                    return new(ConsoleColor.DarkGreen, ConsoleColor.Green);
                }
                case MtLlmTokType.MT_TOK_TYPE_THINK_END:
                {
                    return new(ConsoleColor.Black, ConsoleColor.Green);
                }

                default:
                {
                    throw new NotImplementedException(
                        "Unsupported token type given!");
                }
            }
        }

        internal static void ConsoleWrite(string piece, MtLlmTokType type)
        {
            var colors = GetColorsFor(type);

            Console.ForegroundColor = colors.Item1;
            Console.BackgroundColor = colors.Item2;
            Console.Write(piece);
            Console.ForegroundColor = _defColorFg;
            Console.BackgroundColor = _defColorBg;
        }
    }
}
