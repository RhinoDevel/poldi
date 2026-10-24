
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

// RhinoDevel, Marcel Timm, 2025feb24

using Poldi.CppInterface;
using Poldi.CppInterface.MtLlm;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Poldi.HttpChat
{
    internal static class HttpChat
    {
        /// <remarks>
        /// Also see client code.
        /// </remarks>
        private const string _headerReqImmedUserResp =
            "Request-Immediate-User-Response";

        private static readonly string _webClientPath =
            C.D<string>("http:webClientPath");

        private static MtLlmP? _pChat = null;

        /// <remarks>
        /// <see cref="ChatSingletonParams.OnSampledNonEogNonCtrl"/> will
        /// always be set to null, here (sometimes to-be-modified, later).
        /// </remarks>
        private static ChatSingleton.ChatSingletonParams CreateParams(
            string prompt)
        {
            Debug.Assert(_pChat != null);

            return new ChatSingleton.ChatSingletonParams
            {
                ParamsGiven = _pChat!,
                PromptGiven = prompt,
                OnSampledNonEogNonCtrl = null, // <- Always null, here.
                KeepLlmLoaded = C.D<bool>("llm:keepLoaded"),
                WriteToConsole = C.D<bool>("llm:writeToConsole"),
                SlotIndex = (int)SlotIndex.Chat,
                UseMemorySystem = C.D<bool>("llm:useMemorySystem"),
                UseModules = C.D<bool>("llm:useModules")
            };
        }

        private static string GetBody(HttpListenerRequest req)
        {
            using var r = new StreamReader(req.InputStream);
            var retVal = r.ReadToEnd();

            Log.Line($"Request body: \"{retVal}\".");

            return retVal;
        }

        private static string GetPrompt(string body)
        {
            var queryReq = QueryRequest.CreateFromJson(body);

            // No error handling!

            return queryReq!.Prompt;
        }

        /// <remarks>
        /// Closes <paramref name="resp"/>.
        /// </remarks>
        private static void OnTextToAudioRequestFromPrompt(
            string prompt, HttpListenerResponse resp)
        {
            resp.KeepAlive = false;
            resp.SendChunked = true;

            if (string.IsNullOrWhiteSpace(prompt))
            {
                // (an immediate user response is never requested, here)
                Log.Line("Empty text was given in request, ignoring..");
                return;
            }

            try
            {
                using(var w = new BinaryWriter(resp.OutputStream))
                {
                    // Return value ignored (communication is handled by callback).
                    ChatSingleton.AudioChat.Query(
                        CreateParams(prompt),
                        w,
                        reqImmedUserResp =>
                        {
                            // Should be called once (before the first data is sent).
                            Debug.Assert(resp.Headers[_headerReqImmedUserResp] == null);

                            resp.Headers[_headerReqImmedUserResp] =
                                reqImmedUserResp.ToString().ToLowerInvariant();
                        });
                }
            }
            catch(Exception e)
            {
                Log.Line($"Error: Exception occurred with msg. \"{e.Message}\"!");
            }
            finally
            {
                // Should not be necessary:
                //
                //Log.Line("Before close.");
                // resp.OutputStream.Close(); // <- Already disposed.
                // Log.Line("After stream close");
                resp.Close();
                //Log.Line("After response close.");
            }
        }

        private static void OnTextToAudioRequestFromBody(
            string body, HttpListenerResponse resp)
        {
            OnTextToAudioRequestFromPrompt(GetPrompt(body), resp);
        }

        /// <remarks>
        /// Possible "client" for testing:
        /// 
        /// curl -N -v http://127.0.0.1:8080/cancel
        /// </remarks>
        private static void OnCancelRequest(
            HttpListenerRequest req, HttpListenerResponse resp)
        {
            // Do this here, only!
            ChatSingleton.ChatSingleton.Cancel();

            resp.StatusCode = (int)HttpStatusCode.OK;
            resp.ContentLength64 = 0;
        }

        private static void OnQueryRequest(
            HttpListenerRequest req,
            HttpListenerResponse resp,
            bool isSel)
        {
            var reqBody = GetBody(req);
            string respStr;
            byte[] respBytes;

            if (isSel)
            {
                var selQueryReq = SelQueryRequest.CreateFromJson(reqBody);

                Debug.Assert(selQueryReq != null); // No error handling!

                respStr = JsonSerializer.Serialize(
                    new
                    {
                        text = string.Empty, // No text.
                        digProbs = Sel.Create(
                            selQueryReq.Question,
                            selQueryReq.Options,
                            null, // No explanation.
                            false, // Do NOT write to console.
                            true)! // <- De-initialize, if sel. slot used.
                                .Probs
                    });
            }
            else
            {
                var chatSingletonResult =
                        ChatSingleton.ChatSingleton.Query(
                            CreateParams(GetPrompt(reqBody)));
                
                respStr = JsonSerializer.Serialize(
                        new
                        {
                            text = chatSingletonResult.LastText,
                            digProbs = Array.Empty<double>() // No probs.
                        });
            }

            respBytes = Encoding.UTF8.GetBytes(respStr);

            resp.StatusCode = (int)HttpStatusCode.OK;
            resp.ContentLength64 = respBytes.Length;
            resp.OutputStream.Write(respBytes, 0, respBytes.Length);
        }

        /// <remarks>
        /// Possible "client" for testing:
        /// 
        /// curl -N -v -d '{"question":"What does the user saying the following sentence want: \"Play Beethoven!\"?","options":["User wants to play a game.","User wants to listen to music.","User wants to chat.","User wants to know the current date and time.","User wants to know the current internet IP address.","User wants to know something about a specific topic."]}' -H "Content-Type: application/json" http://127.0.0.1:8080/selQuery
        /// </remarks>
        private static void OnSelQueryRequest(
            HttpListenerRequest req, HttpListenerResponse resp)
        {
            OnQueryRequest(req, resp, true);
        }

        private static void OnChatQueryRequest(
            HttpListenerRequest req, HttpListenerResponse resp)
        {
            ChatSingleton.ChatSingleton.ResetCancel();

            OnQueryRequest(req, resp, false);
        }

        /// <remarks>
        /// Possible "client" for testing:
        /// 
        /// curl -N -v -d '{"prompt": "Erzähle mir eine laaange Piratengeschichte!"}' -H "Content-Type: application/json" http://127.0.0.1:8080/chunked
        /// </remarks>
        private static void OnChunkedRequest(
            HttpListenerRequest req, HttpListenerResponse resp)
        {
            Debug.Assert(_pChat != null);

            string? reqBody;
            string? prompt;

            ChatSingleton.ChatSingleton.ResetCancel();

            using var writer = new StreamWriter(
                resp.OutputStream, Encoding.UTF8);
            
            reqBody = GetBody(req);
            prompt = GetPrompt(reqBody);

            resp.KeepAlive = false;
            resp.SendChunked = true;

            // Return value ignored (communication is handled by callback).
            ChatSingleton.StreamChat.Query(CreateParams(prompt), writer);
        }

        /// <remarks>
        /// Also chunked!
        /// 
        /// Possible "client" for testing:
        /// 
        /// curl -N -v -d '{"prompt": "Wer ist der coolste Mathematiker: Leonhard Euler, Leonardo oder Gauss?"}' -H "Content-Type: application/json" http://127.0.0.1:8080/textToAudio | ./ffplay.exe -f s16be -ar 22050 -
        /// </remarks>
        private static void OnTextToAudioRequest(
            HttpListenerRequest req, HttpListenerResponse resp)
        {
            ChatSingleton.ChatSingleton.ResetCancel();

            OnTextToAudioRequestFromBody(GetBody(req), resp);
        }

        /// <remarks>
        /// Also chunked!
        /// 
        /// Possible "client" for testing:
        /// 
        /// curl -N -v --data-binary @sample.wav -H "Content-Type: application/octet-stream" http://127.0.0.1:8080/audioToAudio | ./ffplay.exe -f s16be -ar 22050 -
        /// </remarks>
        private static void OnAudioToAudioRequest(
            HttpListenerRequest req, HttpListenerResponse resp)
        {
            byte[] wavData;
            float[]? audioData;
            string? text;

            ChatSingleton.ChatSingleton.ResetCancel();

            // ***************************************************
            // *** Get the wave file content from the request: ***
            // ***************************************************

            using (var memStream = new MemoryStream())
            {
                // This should work for chunked or "unchunked" requests:
                //
                req.InputStream.CopyTo(memStream);
                
                wavData = memStream.ToArray();
            }

            // ****************************************************
            // *** Extract audio samples in the correct format: ***
            // ****************************************************

            audioData = Wav.CreateAudioData(wavData);
            Debug.Assert(audioData != null);

            // *****************************************************************
            // *** Transcribe the audio to text, if voice activity detected: ***
            // *****************************************************************

            // TODO: Split this method from initialization of VAD?
            if(Vad.HasVoiceActivity(C.D<string>("vad:modelPath"), audioData))
            {
                // TODO: Split this method from initialization and immediately
                //       initialize after LLM usage (to save time on next request)?
                //
                text = MtStt.Transcribe(
                    C.D<bool>("stt:useGpu"),
                    C.D<int>("stt:threads"),
                    C.D<string>("stt:language"),
                    false,
                    "Poldi",//string.Empty, // TODO: Hard-coded!
                    C.D<string>("stt:modelPath"),
                    audioData,
                    null,
                    out _,
                    null,
                    null,
                    null);
                Debug.Assert(text != null);
                Log.Line($"STT result: \"{text}\"");
            }
            else
            {
                Log.Line("No voice activity detected, setting text to empty string..");
                text = string.Empty;
            }

            // ****************************************************************
            // *** Proceed, as if the transcribed text was received as body ***
            // *** of the request:                                          ***
            // ****************************************************************

            OnTextToAudioRequestFromPrompt(text, resp);
        }

        private static void OnWebClientRequest(
            HttpListenerRequest req, HttpListenerResponse resp)
        {
            Debug.Assert(
                req.Url!.AbsolutePath.StartsWith($"/{_webClientPath}"));

            var webClientFolder = AppContext.BaseDirectory
                    + "webclient"; // Hard-coded
            var indexHtmlFilename = "index.html"; // Hard-coded
            byte[] data;
            string contentType;

            string urlPath;
            string filePath;

            urlPath = req.Url!.AbsolutePath;

            urlPath = urlPath.Substring(($"/{_webClientPath}").Length);

            if(urlPath.Length == 0 || urlPath == "/")
            {
                urlPath = indexHtmlFilename;
            }
            else
            {
                Debug.Assert(urlPath.StartsWith('/'));
                urlPath = urlPath.Substring(1);
            }

            filePath = Path.Combine(
                webClientFolder,
                urlPath.Replace('/', Path.DirectorySeparatorChar));

            // The whole "/../"-related security checks here might be
            // unnecessary, because of the HttpListener setup in Server.cs..
            filePath = Path.GetFullPath(filePath); // RESOLVES the rel. path.
            if(!filePath.StartsWith(
                    Path.GetFullPath(webClientFolder),
                    StringComparison.OrdinalIgnoreCase))
            {
                resp.StatusCode = 403; // Forbidden
                resp.Close();
                return;
            }

            if(!File.Exists(filePath))
            {
                Log.Line($"File \"{filePath}\" was not found.");
                resp.StatusCode = 404; // Not found.
                resp.Close();
                return;
            }

            data = File.ReadAllBytes(filePath);

            resp.ContentLength64 = data.Length;
            switch(Path.GetExtension(filePath))
            {
                case ".html":
                {
                    contentType = "text/html";
                    break;
                }

                case ".js":
                {
                    contentType = "application/javascript";
                    break;
                }

                case ".png":
                {
                    contentType = "image/png";
                    break;
                }

                // Add more, when necessary.

                default:
                {
                    Debug.Assert(false); // Should not get here.
                    contentType = "application/octet-stream";
                    Log.Line($"Warning: Unsupported file extension (file \"{filePath}\"), using \"{contentType}\"..");
                    break;
                }
            }
            resp.ContentType = contentType;
            resp.OutputStream.Write(data);
            resp.Close();
        }

        /// <remarks>
        /// Throws an exception, if one endpoint path starts with the path of
        /// another endpoint (<see cref="Server.Server.GetEndpoint"/>).
        /// </remarks>
        private static List<Server.Endpoint> CreateEndpoints()
        {
            List<Server.Endpoint> retVal = [
                new Server.Endpoint( // Text to text for selection.
                        C.D<string>("http:selQueryPath"),
                        "POST",
                        OnSelQueryRequest,
                        "application/json",
                        false),
                new Server.Endpoint( // Text to text for chat.
                        C.D<string>("http:queryPath"),
                        "POST",
                        OnChatQueryRequest,
                        "application/json",
                        false),
                new Server.Endpoint( // Text to text, chunked response(-s).
                        C.D<string>("http:chunkedPath"),
                        "POST",
                        OnChunkedRequest,
                        "text/plain",
                        false),
                new Server.Endpoint( // Text to audio, chunked response(-s).
                        C.D<string>("http:textToAudioPath"),
                        "POST",
                        OnTextToAudioRequest,
                        "audio/L16",
                        false),
                new Server.Endpoint( // Audio to audio, chunked resp.
                        C.D<string>("http:audioToAudioPath"),
                        "POST",
                        OnAudioToAudioRequest,
                        "audio/L16",
                        false),

                new Server.Endpoint( // Cancel endpoint.
                        C.D<string>("http:cancelPath"),
                        "GET",
                        OnCancelRequest,
                        null,
                        true),

                new Server.Endpoint( // Web client endpoint.
                        _webClientPath,
                        "GET",
                        OnWebClientRequest,
                        null,
                        true)
            ];

            for(int i = 0; i < retVal.Count; ++i)
            {
                for(int j = 0; j < retVal.Count; ++j)
                {
                    if(i == j)
                    {
                        continue;
                    }

                    if (retVal[j].Path.StartsWith(retVal[i].Path))
                    {
                        throw new Exception(
                            $"Endpoint path \"{retVal[j].Path}\" starts with other endpoint path \"{retVal[i].Path}\"!");
                    }
                }
            }

            return retVal;
        }

        /// <remarks>
        /// For Windows, execute something like this as Administrator:
        /// netsh http add urlacl url=http://rog.fritz.box:8080/ user=ROG\marcel
        /// </remarks>
        internal static bool Exec()
        {
            bool retVal;

            MtTts.ReInit(
                C.D<string>("tts:modelPath"), C.D<string>("tts:configPath"));

            // Callback prop. will be set (and reset to null) by ChatSingleton:
            _pChat = LlmPCreate.FromConfigChat(null);

            retVal = Server.Server.Exec(
                C.D<string>("http:url"), CreateEndpoints());
            MtTts.DeInit();
            return retVal;
        }
    }
}
