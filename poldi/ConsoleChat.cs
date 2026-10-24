
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

using Poldi.CppInterface.MtLlm;
using System;
using System.Diagnostics;

namespace Poldi
{
    internal static class ConsoleChat
    {
        // *********************************************************************
        // *** Fix configuration settings:                                   ***
        // *********************************************************************

        private const string _snapshotFileAbsPath = "mt_snapshot.bin";
        private const int _slotIndex = (int)SlotIndex.Chat;
        private const char _cmdPrefix = '@';
        private static readonly bool _useModules = C.D<bool>("llm:useModules");
        private static readonly bool _useMemorySystem =
            C.D<bool>("llm:useMemorySystem");
        private static readonly bool _keepLlmLoaded =
            C.D<bool>("llm:keepLoaded");
        private static readonly bool _writeToConsole =
            C.D<bool>("llm:writeToConsole");

        // *********************************************************************

        private static void PrintFixConfigSettings()
        {
            Console.WriteLine(
                $"[Snapshot file abs. path: \"{_snapshotFileAbsPath}\"]");
            Console.WriteLine($"[Slot index: {_slotIndex}]");
            Console.WriteLine($"[Command prefix: \"{_cmdPrefix}\"]");
            Console.WriteLine(
                $"[Using memory system: {(_useMemorySystem ? "Yes" : "No")}]");
            Console.WriteLine(
                $"[Using modules: {(_useModules ? "Yes" : "No")}]");
        }

        private static bool SnapshotUpdate()
        {
            Console.WriteLine("[Updating snapshot..]");
            if (!MtLlm.SnapshotUpdate(_slotIndex))
            {
                Console.WriteLine("[Error: Updating snapshot failed!]");
                return false;
                
            }
            Console.WriteLine("[Updating snapshot succeeded.]");
            return true;
        }
        private static bool SnapshotRestore()
        {
            Console.WriteLine("[Restoring snapshot..]");
            if (!MtLlm.SnapshotRestore(_slotIndex))
            {
                Console.WriteLine("[Error: Restoring snapshot failed!]");
                return false;
            }
            Console.WriteLine("[Restoring snapshot succeeded.]");
            return true;
        }

        private static bool SnapshotToFile()
        {
            Console.WriteLine("[Saving snapshot to file..]");
            if (!MtLlm.SnapshotToFile(_slotIndex, _snapshotFileAbsPath))
            {
                Console.WriteLine("[Error: Saving snapshot to file failed!]");
                return false;

            }
            Console.WriteLine("[Saving snapshot to file succeeded.]");
            return true;
        }

        private static bool SnapshotFromFile()
        {
            Console.WriteLine("[Loading snapshot from file..]");
            if (!MtLlm.SnapshotFromFile(_snapshotFileAbsPath, _slotIndex))
            {
                Console.WriteLine(
                    "[Error: Loading snapshot from file failed!]");
                return false;

            }
            Console.WriteLine("[Loading snapshot from file succeeded.]");
            return true;
        }

        private static bool TokenCount()
        {
            int max = MtLlm.GetMaxTokenCount(_slotIndex);
            int cur = MtLlm.GetCurrentTokenCount(_slotIndex);

            Console.WriteLine($"[{cur} of {max} tokens in context.]");
            return true;
        }

        private static void UpdateEmbeddings()
        {
            ChatMem.ChatMemDb.ReInit(C.D<string>($"mem:dbPath"));
            Debug.Assert(ChatMem.ChatMemDb.IsInit());

            foreach(var statementNr in ChatMem.ChatMemDb.LoadAllStatementNrs(0))
            {
                ChatMem.Statement statement =
                    ChatMem.ChatMemDb.LoadStatement(statementNr)!;
                float[] embVec = Emb.Create(statement.Content)!;

                ChatMem.ChatMemDb.UpdateStatementEmbedding(statementNr, embVec);
            }

            foreach(var chatNr in ChatMem.ChatMemDb.LoadAllChatNrs())
            {
                ChatMem.Chat chat = ChatMem.ChatMemDb.LoadChat(chatNr)!;
                float[] embVec = Emb.Create(chat.Summary)!;

                ChatMem.ChatMemDb.UpdateChatEmbedding(chatNr, embVec);
            }
        }

        private static void ExecCmd(string cmd, out bool reInit, out bool exit)
        {
            reInit = false;
            exit = false;
            switch (cmd)
            {
                // Update the snapshot of the LLM's context in RAM.
                case "snapshot update":
                {
                    SnapshotUpdate(); // Writes to console.
                    return;
                }

                // Restore the snapshot stored in RAM to be used as context.
                case "snapshot restore":
                {
                    SnapshotRestore(); // Writes to console.
                    return;
                }

                // Write the LLM's context snapshot already being in RAM to the
                // snapshot on disk.
                case "snapshot to file":
                {
                    SnapshotToFile(); // Writes to console.
                    return;
                }

                // Load the snapshot from the file on disk into RAM (does NOT
                // restore it to be used as context automatically).
                case "snapshot from file":
                {
                    SnapshotFromFile(); // Writes to console.
                    return;
                }

                case "token count":
                {
                    TokenCount(); // Writes to console.
                    return;
                }

                // Updates ALL embeddings in the database. Must be used, if the
                // embedding model to be used was changed.
                case "update embeddings":
                {
                    UpdateEmbeddings();
                    return;
                }

                case "reset":
                {
                    Console.WriteLine("[Triggering LLM reset..]");
                    MtLlm.Reset(
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        false,
                        _slotIndex);
                    return;
                }

                case "reinit":
                {
                    Console.WriteLine("[Triggering LLM (re-)initialization..]");
                    reInit = true;
                    return;
                }

                case "exit":
                {
                    Console.WriteLine("[Triggering exit..]");
                    exit = true;
                    return;
                }

                // BASH's CTRL + L ("clear -x").
                case "cls":
                {
                    Console.Write("\x1b[2J\x1b[H");
                    return;
                }

                default:
                {
                    Console.WriteLine($"[Unsupported command \"{cmd}\" given. Doing nothing..]");
                    return;
                }
            }
        }

        private static string ReadUserInput()
        {
            string? buf;

            Console.WriteLine();
            while (true)
            {
                Console.Write("> ");
                buf = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(buf))
                {
                    return buf.Trim();
                }
                Console.WriteLine("[Please enter a user prompt or a command.]");
            }
        }

        internal static bool Exec()
        {
            bool reInit = true;
            var parameters = LlmPCreate.FromConfigChat(null);

            // CTRL+C shall interrupt the LLM during inference:
            Console.CancelKeyPress +=
                (_, e) =>
                {
                    e.Cancel = true;

                    ChatSingleton.ChatSingleton.Cancel();
                };

            PrintFixConfigSettings();
            Console.WriteLine( // Hard-coded
                $"[Chat model path: {C.D<string>($"llm:chat:modelPath")}]");
            Console.WriteLine( // Hard-coded
                $"[Database path: {C.D<string>("mem:dbPath")}]");

            while (true)
            {
                string userInput;

                if (reInit)
                {
                    reInit = false;

                    // Actual re-initialization will be done by ChatSingleton.
                    MtLlm.DeInit(_slotIndex);
                }

                userInput = ReadUserInput();
                if(userInput.StartsWith(_cmdPrefix))
                {
                    // The user input seems to be a command.

                    bool exit;

                    ExecCmd(userInput[1..], out reInit, out exit);
                    if (exit)
                    {
                        break; // => Exit application.
                    }
                    continue;
                }

                // The user input is interpreted as prompt for the LLM.

                // Trick to let Mem and Sel use the chat slot from the start.
                if (_keepLlmLoaded && !MtLlm.IsInit(_slotIndex))
                {
                    MtLlm.ReInit(parameters, _slotIndex);
                }

                ChatSingleton.ChatSingleton.ResetCancel();

                ChatSingleton.ChatSingleton.Query(
                    new ChatSingleton.ChatSingletonParams
                    {
                        ParamsGiven = parameters,
                        PromptGiven = userInput,
                        OnSampledNonEogNonCtrl =
                            p =>
                            {
                                if(p.ForceOutput)
                                {
                                    Console.WriteLine(p.Piece);
                                }
                                return true;
                            },

                        KeepLlmLoaded = _keepLlmLoaded,

                        WriteToConsole = _writeToConsole,
                        SlotIndex = _slotIndex,

                        UseMemorySystem = _useMemorySystem,

                        UseModules = _useModules
                    });
            }

            MtLlm.DeInit(_slotIndex);
            MtLlm.SnapshotClear(_slotIndex);
            return true;
        }
    }
}
