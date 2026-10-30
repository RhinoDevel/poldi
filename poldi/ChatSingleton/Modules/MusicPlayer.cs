
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

// Marcel Timm, RhinoDevel, 2026oct30

using Poldi.Search;
using System;
using System.Diagnostics;

namespace Poldi.ChatSingleton.Modules;

internal class MusicPlayer : Module
{
    private enum State
    {
        Start,
        ExtractOnly,
        BeginPlayback,
        PlayingBack,
        Done
    }

    private static readonly bool _writeToConsole =
        C.D<bool>("llm:writeToConsole");

    private const string _intention = "To listen to music.";

    // *************************************************************************

    private State _state = State.Start;
    private string _artistAndOrTitle = string.Empty;

    internal override Module Create() => new MusicPlayer();

    internal override string GetIntention() => _intention;

    private static string? GetArtistAndOrTitle(string input)
    {
        var sysPrompt =
                """
                You will be given a task.
                Follow the instructions completely.
                """;
        var prompt =
                $"""
                    Extract the artist and/or title of the song from the following user request.

                    User request: "{input}"

                    Answer with that extracted information, only (e.g. "Blink-182 - Voyeur", "AC/DC - Shoot To Thrill" or "Robert Johnson - Hellhound On My Trail")!
                    If no artist and/or title can be extracted, answer with only "{SearchHelper.NotFoundResponse}" instead.
                    """;
        var tuple = Mem.Create(
                sysPrompt,
                prompt,
                _writeToConsole,
                true); // <- De-initialize, if memory slot was used.
        string artistAndOrTitle;

        if (tuple == null)
        {
            Debug.Assert(false); // Should not get here.
            return null;
        }

        artistAndOrTitle = tuple.Item1;

        if (artistAndOrTitle.Trim().Length == 0)
        {
            Debug.Assert(false); // Should not get here.
            return null;
        }

        if (artistAndOrTitle.ToLower().Contains(SearchHelper.NotFoundResponse.ToLower()))
        {
            // It was not possible to extract the artist and/or title.
            return null;
        }

        Log.Line($"Extracted artist and/or title \"{artistAndOrTitle}\" from user input \"{input}\".");
        return artistAndOrTitle;
    }

    private string OnStart(
        string input, out bool isDone, out bool reqImmedUserResp)
    {
        string? artistAndOrTitle;

        isDone = false;
        
        artistAndOrTitle = GetArtistAndOrTitle(input);
        if(artistAndOrTitle == null)
        {
            Debug.Assert(_artistAndOrTitle == string.Empty);
            _state = State.ExtractOnly;
            reqImmedUserResp = true;
            return "What do you want to listen to?"; // TODO: Hard-coded (language)!
        }

        _artistAndOrTitle = artistAndOrTitle;
        _state = State.BeginPlayback;
        reqImmedUserResp = false;
        return string.Empty;
    }

    private string OnExtractOnly(
        string input, out bool isDone, out bool reqImmedUserResp)
    {
        string? artistAndOrTitle;

        isDone = false;

        artistAndOrTitle = GetArtistAndOrTitle(input);
        if (artistAndOrTitle == null)
        {
            Debug.Assert(_artistAndOrTitle == string.Empty);
            isDone = true;
            _state = State.Done;
            reqImmedUserResp = false;
            return "Sorry, I didn't get that. Exiting music player."; // TODO: Hard-coded (language)!
        }

        _artistAndOrTitle = artistAndOrTitle;
        _state = State.BeginPlayback;
        reqImmedUserResp = false;
        return string.Empty;
    }

    private string OnBeginPlayback(out bool isDone, out bool reqImmedUserResp)
    {
        Debug.Assert(_artistAndOrTitle.Trim().Length != 0);

        // TODO: Implement actually starting playback!

        isDone = false;
        reqImmedUserResp = false;
        _state = State.PlayingBack;
        return $"Playing {_artistAndOrTitle}."; // TODO: Hard-coded (language) and unnecessary (logic problem?!)!
    }

    private string OnPlayingBack(out bool isDone, out bool reqImmedUserResp)
    {
        // TODO: Implement actually stopping playback!

        isDone = true;
        reqImmedUserResp = false;
        _state = State.Done;
        return "Stopped playback."; // TODO: Hard-coded (language)!
    }

    internal override string Exec(
        string input,
        out bool calledByMistake,
        out bool isDone,
        out bool reqImmedUserResp)
    {
        calledByMistake = false;

        do
        {
            var output = string.Empty;

            switch (_state)
            {
                case State.Start:
                {
                    output = OnStart(input, out isDone, out reqImmedUserResp);
                    break;
                }

                case State.ExtractOnly:
                {
                    output = OnExtractOnly(
                        input, out isDone, out reqImmedUserResp);
                    break;
                }

                case State.BeginPlayback:
                {
                    output = OnBeginPlayback(out isDone, out reqImmedUserResp);
                    break;
                }

                case State.PlayingBack:
                {
                    // Always just stopping music, no matter what input.
                    output = OnPlayingBack(out isDone, out reqImmedUserResp);
                    break;
                }

                case State.Done: // Should not be called, if in done state.
                default:
                {
                    Debug.Assert(false); // Must not get here.
                    throw new Exception("Unsupported state given!");
                }
            }

            if (isDone)
            {
                // The modules is done with its whole task.

                Debug.Assert(0 < output.Trim().Length); // LLM resp. required.

                Debug.Assert(_state == State.Done);
                return output;
            }

            // The module is NOT done with the whole task.
            Debug.Assert(_state != State.Done);

            if (0 < output.Length)
            {
                // The module needs new user input for its next step.

                // The output must not be whitespace-only.
                Debug.Assert( 0 < output.Trim().Length);
                return output;
            }

            // The module does not need new user input to proceed (indicated by
            // an empty output string).
        } while (true);
    }
}
