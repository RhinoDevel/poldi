
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

// Marcel Timm, RhinoDevel, 2025mar18

using System;
using System.IO;

namespace Poldi
{
    internal static class Program
    {
        static int Main(string[] args)
        {
            // Will log to text file (see Log.cs) anyway.
            Log.SetLogToConsole(C.D<bool>("global:logToConsole"));

            Log.Line("Entered main method.");

            Log.Line($"[Current (working) directory: \"{Directory.GetCurrentDirectory()}\"]");
            Log.Line($"[Application folder: \"{AppContext.BaseDirectory}\"]");
            Log.Line($"[Process path: \"{Environment.ProcessPath}\"]");
#if LINUX
            Log.Line($"[LD_LIBRARY_PATH={Environment.GetEnvironmentVariable("LD_LIBRARY_PATH")}]");
#endif //LINUX

            ChatMem.ChatMemDb.ReInit(C.D<string>("mem:dbPath")!);
            
            //ChatMem.ChatMemDb.DeleteAllStatementDuplicates();
            //ChatMem.ChatMem.Update(false);

            if (args.Length == 1)
            {
                switch (args[0])
                {
                    case "c": // Console chat.
                    {
                        Log.Line("*** CONSOLE CHAT MODE ***");
                        if (!ConsoleChat.Exec())
                        {
                            return -1;
                        }
                        return 0;
                    }

                    case "s": // Select query test.
                    {
                        Log.Line("*** SELECT QUERY TEST MODE ***");
                        Log.Line(SelQueryTest.Exec());
                        return 0;
                    }

                    case "w": // Wikipedia search.
                    {
                        Log.Line("*** CONSOLE WIKIPEDIA SEARCH MODE ***");
                        if (!ConsoleWiki.Exec())
                        {
                            return -1;
                        }
                        return 0;
                    }

                    default:
                    {
                        Log.Line($"Unsupported argument \"{args[0]}\" given. Ignoring..");
                        break;
                    }
                }
            }

            Log.Line("*** HTTP CHAT MODE.. ***");

            // HTTP chat:
            if (!HttpChat.HttpChat.Exec())
            {
                return -1;
            }

            return 0;
        }
    }
}

// Whisper.cpp tests:
//
//{
//    var speech = new float[10];
//    string text;
//
//    for (int i = 0; i < speech.Length; ++i)
//    {
//        speech[i] = 0.0f;
//    }
//
//    text = CppInterface.MtStt.Transcribe(
//        true,//bool.Parse(C.D["stt:useGpu"]!),
//        int.Parse(C.D["stt:threads"]!),
//        C.D["stt:language"]!,
//        false,
//        string.Empty,
//        "..\\..\\..\\..\\ggml-large-v3-turbo-q5_0.bin",//C.D["stt:modelPath"]!,
//        speech,
//        null,
//        out _,
//        null,
//        null,
//        null)!;
//
//    System.Console.WriteLine($"Whisper.cpp test: Resulting text: \"{text}\"");
//}

// Piper tests:
//
//{
//    CppInterface.MtTts.ReInit(
//        "de_DE-thorsten-high.onnx", "de_DE-thorsten-high.onnx.json");
//    //
//    // To WAV:
//    //
//    CppInterface.MtTts.ToWavFile(
//        "Hallo, das hier ist ein Test. Ich denke, nun funktioniert TTS per Piper über C#. Hurra!",
//        "test1.wav");
//    CppInterface.MtTts.ToWavFile(
//        "Aber geht dies auch ein zweites Mal? Fragen über Fragen..",
//        "test2.wav");
//    //
//    // Raw:
//    //
//    var samples = CppInterface.MtTts.ToRaw(
//            "Ein Schwein macht miau, die Kuh macht wuff, das Schaf sagt oink, der Hund spricht muh!");
//    System.Console.WriteLine($"Piper raw test: Samples min. = {samples.Min()}, max. = {samples.Max()}.");
//    //
//    CppInterface.MtTts.DeInit();
//}

// Embeddings test:
//
//{
//    //const string a = "Geoffrey has a cat.";
//    //const string b = "Geoffrey has no cat.";
//
//    //const string a = "My favorite color is red.";
//    //const string b = "My favorite color is blue.";
//
//    //const string a = "I was born on the 21st of september, in 1974.";
//    //const string b = "The 21.09.1974 is my birthday.";
//
//    const string a = "Can you tell me the name of the town I am living in?";
//    const string b = "I am living in a town called Berlin.";
//
//    CppInterface.MtLlm.MtLlm.ReInit(LlmPCreate.FromConfigEmb(), 0);
//
//    var embA = CppInterface.MtLlm.MtLlm.CreateEmbeddings(a, 0)!;
//    var embB = CppInterface.MtLlm.MtLlm.CreateEmbeddings(b, 0)!;
//
//    var cosSim = Calc.GetDotProd(embA, embB);
//
//    Log.Line($"Cosine similarity = {cosSim}.");
//
//    CppInterface.MtLlm.MtLlm.DeInit(0);
//    return 0;
//}

// Reranker test:
//
//{
//    var query = "What is a whale?";
//    List<string> documents = [
//            "Butterflies eat flowers.",
//            "A whale is an animal.",
//            "I like to eat fish.",
//            "Dogs want food.",
//            "A large mammal that lives in water.",
//            "A whale shark is a dangerous animal."
//        ];
//
//    CppInterface.MtLlm.MtLlm.ReInit(LlmPCreate.FromConfigRer(), 0);
//
//    var scores = CppInterface.MtLlm.MtLlm.CreateRerank(
//            query, documents, 0)!;
//
//    for (int i = 0; i < scores.Length; ++i)
//    {
//        Log.Line($"{i}: \"{documents[i]}\": {scores[i]}");
//    }
//
//    CppInterface.MtLlm.MtLlm.DeInit(0);
//    return 0;
//}
