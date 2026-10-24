
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

// Marcel Timm, RhinoDevel, 2026mar29

using Poldi.CppInterface.MtLlm;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Poldi.ChatMem
{
    /// <summary>
    /// Singleton to extract information from recorded sessions, to store in DB
    /// (to be used later, e.g. for memory retrieval and injection in user
    /// prompts).
    /// </summary>
    internal static class Extract
    {
        private static readonly bool _writeToConsole =
            C.D<bool>("llm:writeToConsole");

        private const string _user = "User";
        private const string _assistant = "Assistant";
        private const string _memQueryEmptyTag = "EMPTY";
        
        internal static readonly int MaxWordsSummary =
            C.D<int>("mem:maxWordsSummary");

        private static readonly double _statementIsUsefulMemMinConfidence =
            C.D<double>("mem:statementIsUsefulMemMinConfidence");
        private static readonly string _statementIsUsefulQuestion =
            C.D<string>("mem:statementIsUsefulQuestion");
        private static readonly string _statementIsUsefulExplanation =
            C.D<string>("mem:statementIsUsefulExplanation");

        /// <summary>
        /// The first part of the single user query used to extract different
        /// kinds of information from the dialog protocol (a summary of the
        /// dialog or statements to be used as memories).
        /// 
        /// The single parameter is the dialog itself.
        /// </summary>
        private static readonly string _dialogPromptBeginFormat;

        /// <summary>
        /// The second part of the single user query to create a summary of/from
        /// the dialog protocol.
        /// 
        /// First parameter is the max. word count for the summary,
        /// second parameter is the date of the dialog.
        /// </summary>
        private static readonly string _dialogTaskSummaryFormat;

        /// <summary>
        /// The second part of the single user query to extract FACTs about the
        /// user from the dialog protocol.
        /// 
        /// The single parameter is the empty tag
        /// <see cref="_memQueryEmptyTag"/>.
        /// </summary>
        private static readonly string _dialogTaskMemoriesFormat_FACT;
        //
        // Maybe add extraction of other kinds of statements than FACTs about
        // the user?

        static Extract()
        {
            string relPath = AppContext.BaseDirectory
                + "ChatMem" // <- Hard-coded!
                + Path.DirectorySeparatorChar.ToString();

            _dialogPromptBeginFormat = File.ReadAllText(
                relPath + "dialogPromptBeginFormat.txt");
            _dialogTaskSummaryFormat = File.ReadAllText(
                relPath + "dialogTaskSummaryFormat.txt");
            _dialogTaskMemoriesFormat_FACT = File.ReadAllText(
                relPath + "dialogTaskMemoriesFormat_FACT.txt");
        }

        /// <summary>
        /// Returns the dialog created as a string from the given
        /// <paramref name="msgs"/>.
        /// </summary>
        /// <remarks>
        /// Returns an empty string, if something unexpected was detected in
        /// input data.
        /// </remarks>
        internal static string GetDialog(List<Message> msgs)
        {
            var dialog = string.Empty;

            for (int i = 0; i < msgs.Count; ++i)
            {
                Debug.Assert(
                    i == 0 || msgs[i].MessageIndex == msgs[i - 1].MessageIndex + 1);
                Debug.Assert(msgs[i].MessageIndex == i + 1);

                var expType = i % 2 == 0
                        ? MessageType.User : MessageType.Assistant;

                if (expType != msgs[i].Type)
                {
                    Log.Line($"Warning: Unexpected message type at index {i}. Doing nothing.");
                    return string.Empty;
                }
                if (msgs[i].Content.Trim().Length == 0)
                {
                    Log.Line($"Warning: Empty or whitespace-only message at index {i}. Doing nothing.");
                    return string.Empty;
                }

                if (i != 0)
                {
                    dialog += Environment.NewLine;
                    dialog += Environment.NewLine;
                }

                dialog += msgs[i].Type == MessageType.User
                    ? _user : _assistant;

                dialog += ":" + Environment.NewLine;
                dialog += Environment.NewLine;
                dialog += msgs[i].Content;
            }
            return dialog;
        }

        private static string CreateSummary(
            string sysPrompt,
            string promptBegin,
            DateTime dialogDate,
            int maxWords)
        {
            Debug.Assert(sysPrompt.Trim().Length != 0);
            Debug.Assert(promptBegin.Trim().Length != 0);
            Debug.Assert(_dialogTaskSummaryFormat.Trim().Length != 0);
            Debug.Assert(
                dialogDate.Kind == DateTimeKind.Local
                    && dialogDate == dialogDate.Date);
            Debug.Assert(0 < maxWords);

            var retVal = Mem.Create(
                    sysPrompt,
                    promptBegin
                        + string.Format(
                            _dialogTaskSummaryFormat,
                            maxWords,
                            Temporal.GetDate(dialogDate)),
                    _writeToConsole,
                    false); // <- Do NOT de-initialize the memory slot on done.

            Log.Line($"Summary length: {Str.GetWordCount(retVal!.Item1)} words, {retVal.Item2} tokens (with specials). Req. were at most {maxWords} words.");
            return retVal.Item1;
        }

        private static IEnumerable<string> GetWithoutUnwantedPrefix(
            IEnumerable<string> statements) =>
                statements
                    .Select( // For Mistral-Small-3.2-24B-Instruct-2506..
                        s => Regex.Replace(s, @"^\s*-+\s*", ""))
                    .Select(// For Phi-4..
                        s => Regex.Replace(s, @"^\s*\d+\.\s*", ""));

        /// <remarks>
        /// If chat slot is not used by <see cref="Mem.Create"/>, this method
        /// will free the <see cref="SlotIndex.Mem"/> slot after use.
        /// </remarks>
        private static List<string> CreateInitialStatements(
            string sysPrompt, string prompt, DateTime dialogDate)
        {
            List<string> statements;
            string allStatements;

            // *****************************************************************
            // *** Extract statements from dialog or its summary:            ***
            // *****************************************************************

            allStatements = Mem.Create(
                    sysPrompt,
                    prompt,
                    _writeToConsole,
                    true)! // <- De-initialize the memory slot on done.
                        .Item1;

            // *****************************************************************
            // *** Return empty list, if no statements found:                ***
            // *****************************************************************

            if (allStatements.Trim().Length == 0)
            {
                // No statements found. Return an empty list.
                return new List<string>();
            }

            if (allStatements.Contains(_memQueryEmptyTag))
            {
                // This assertion can fail. Just added for testing.
                Debug.Assert(allStatements == _memQueryEmptyTag);

                // No information found.
                return new List<string>();
            }

            // *****************************************************************
            // *** Create list of separate statements:                       ***
            // *****************************************************************

            statements = allStatements
                .Split(
                    '\n',
                    StringSplitOptions.RemoveEmptyEntries
                        | StringSplitOptions.TrimEntries)
                .ToList();
            if (statements.Count == 0)
            {
                Debug.Assert(false); // Should not get here.
                return new List<string>();
            }

            // *****************************************************************
            // *** Remove maybe existing leading "-" or "1.", "2.", and so   ***
            // *** on:                                                       ***
            // *****************************************************************

            statements = GetWithoutUnwantedPrefix(statements).ToList();

            // *****************************************************************
            // *** Return list of statements:                                ***
            // *****************************************************************

            return statements;
        }

        /// <remarks>
        /// If chat slot is not used by <see cref="Mem.Create"/>, this method
        /// will free the <see cref="SlotIndex.Mem"/> slot after use (see
        /// <see cref="CreateInitialStatements"/>.
        /// </remarks>
        private static List<string> CreateStatements(
            string sysPrompt, string prompt, DateTime dialogDate)
        {
            Debug.Assert(
                dialogDate.Kind == DateTimeKind.Local
                    && dialogDate == dialogDate.Date);

            List<string> statements;

            // *****************************************************************
            // *** Let LLM create the initial statements (from dialog):      ***
            // *****************************************************************

            statements = CreateInitialStatements(
                sysPrompt, prompt, dialogDate);

            // *****************************************************************
            // *** Remove statements by filters:                             ***
            // *****************************************************************

            // Remove all statements that do not contain the word "user" (don't
            // care about casing):
            statements = statements
                .Where(s => s.ToLower().Contains("user"))
                .ToList();

            // Let the selection LLM filter out statements:
            {
                string[] o = [
                        "Yes",
                        "No"
                    ];

                for (int i = 0; i < statements.Count; ++i)
                {
                    var digProbsStats = Sel.Create(
                            string.Format(_statementIsUsefulQuestion, statements[i]),
                            o,
                            _statementIsUsefulExplanation,
                            true,
                            false); // <- Do NOT de-initialize slot when done.

                    if (digProbsStats!.GetMaxIndex(1, o.Length) != 1)
                    {
                        statements[i] = string.Empty;
                        continue;
                    }
                    if (digProbsStats.GetConfidence(1, o.Length)
                            < _statementIsUsefulMemMinConfidence)
                    {
                        statements[i] = string.Empty;
                    }
                }
                Sel.DeInit(); // In case the select slot was used.

                statements = statements
                    .Where(statement => statement.Trim().Length != 0)
                    .ToList();
            }

            return statements;
        }

        private static void RestoreChatStateAndMemAndSel()
        {
            MtLlm.SnapshotRestore((int)SlotIndex.Chat);

            Debug.Assert(!Mem.GetRestoreChatAfterSlotUse());
            Mem.SetRestoreChatAfterSlotUse(true);

            Debug.Assert(!Sel.GetRestoreChatAfterSlotUse());
            Sel.SetRestoreChatAfterSlotUse(true);
        }
        private static void SaveChatStateAndMemAndSel()
        {
            MtLlm.SnapshotUpdate((int)SlotIndex.Chat);

            Debug.Assert(Mem.GetRestoreChatAfterSlotUse());
            Mem.SetRestoreChatAfterSlotUse(false);

            Debug.Assert(Sel.GetRestoreChatAfterSlotUse());
            Sel.SetRestoreChatAfterSlotUse(false);
        }

        /// <returns>
        /// If successful or not. On error, <paramref name="result"/> will
        /// always be null. On success, <paramref name="result"/> may be set or
        /// not (if not, that is no error).
        /// </returns>
        /// <remarks>
        /// Expects the DB to already be initialized.
        /// </remarks>
        internal static bool Exec(ExtractParams p, out ExtractResult? result)
        {
            // TODO: Add more error handling via return value checks.

            Debug.Assert(p.Session != null);
            Debug.Assert(p.Session.Count != 0);
            Debug.Assert(p.Session.First().MessageIndex == 0);
            Debug.Assert(p.Session.First().Type == MessageType.System);

            List<Message> dialogMsgs;
            int c;
            string dialog;
            string promptBegin;
            string summary;
            List<string> statements;
            List<Tuple<string, float[]>> statementAndEmbeddings;
            DateTime dialogDate;
            bool saveAndRestoreChatState;
            float[]? chatSumEmbVec;

            saveAndRestoreChatState = MtLlm.IsInit((int)SlotIndex.Chat)
                && (Mem.IsUsingChatSlotPossible()
                        || (Sel.IsUsingChatSlotPossible()));

            // *****************************************************************
            // *** Save chat state once, if possible/necessary (to prevent   ***
            // *** Mem and/or Sel singletons doing an update and restore for ***
            // *** each call):                                               ***
            // *****************************************************************

            if (saveAndRestoreChatState)
            {
                SaveChatStateAndMemAndSel();
            }

            // ********************************
            // *** Extract messages to use: ***
            // ********************************

            c = p.Session.Count - 1;
            if (c < 2)
            {
                // This will happen again and again (if session data won't get
                // deleted from DB in some way), but will do no harm.
                Log.Line($"Warning: Found less than 2 messages without chat after latest system message, doing nothing.");
                result = null;
                if (saveAndRestoreChatState)
                {
                    RestoreChatStateAndMemAndSel();
                }
                return true; // <- NOT interpreted as error.
            }
            Log.Line($"Found {c} messages without chat after latest system message.");

            // OK, if last answer from LLM is missing.

            // Ignore the system prompt:
            dialogMsgs = p.Session.GetRange(1, c);

            // ****************************
            // *** Put dialog together: ***
            // ****************************

            dialog = GetDialog(dialogMsgs); // Logs, if necessary.
            if (dialog.Length == 0)
            {
                // This will happen again and again (if session data won't get
                // deleted from DB in some way), but will do no harm.
                result = null;
                if (saveAndRestoreChatState)
                {
                    RestoreChatStateAndMemAndSel();
                }
                return true; // <- NOT interpreted as error.
            }

            // *****************************************************************

            promptBegin = string.Format(_dialogPromptBeginFormat, dialog);
            dialogDate = dialogMsgs[0].UtcCreatedAt.ToLocalTime().Date;

            // ***********************************
            // *** Let LLM create the summary: ***
            // ***********************************

            summary = CreateSummary(
                p.SysPromptDialog!,
                promptBegin,
                dialogDate,
                MaxWordsSummary);

            // **************************************
            // *** Let LLM create the statements: ***
            // **************************************

            statements = CreateStatements(
                p.SysPromptDialog!,
                promptBegin
                    + string.Format(
                        _dialogTaskMemoriesFormat_FACT, _memQueryEmptyTag),
                dialogDate);

            // *********************************************
            // *** Create statements' embedding vectors: ***
            // *********************************************

            {
                var embeddings = Emb.Create(statements);

                if(embeddings == null)
                {
                    // (called method logged on error)

                    result = null;
                    if (saveAndRestoreChatState)
                    {
                        RestoreChatStateAndMemAndSel();
                    }
                    // (we would also get here, if the embedding model's
                    // context size is too small for at least one of the given
                    // statements, but we are assuming that this will never be
                    // the case)
                    return false; // <- Interpreted as error (stops updating).
                }
                Debug.Assert(embeddings.Count == statements.Count);
                statementAndEmbeddings = new();
                for (int i = 0; i < statements.Count; ++i)
                {
                    statementAndEmbeddings.Add(
                        new Tuple<string, float[]>(
                            statements[i], embeddings[i]));
                }
            }

            // ***********************************************
            // *** Create chat summary's embedding vector: ***
            // ***********************************************

            chatSumEmbVec = Emb.Create(summary);
            if (chatSumEmbVec == null)
            {
                // (called method logged on error)

                result = null;
                if (saveAndRestoreChatState)
                {
                    RestoreChatStateAndMemAndSel();
                }
                // (we would also get here, if the embedding model's
                // context size is too small for the chat's summary, but we are
                // assuming that this will never be the case)
                return false; // <- Interpreted as error (stops updating).
            }

            // *****************************************************************

            result = new ExtractResult
            {
                Chat = new Chat
                {
                    // Should be the first user message's date/time:
                    UtcChatAt = dialogMsgs[0].UtcCreatedAt,

                    Summary = summary,
                    Embedding = chatSumEmbVec
                },
                StatementAndEmbeddings = statementAndEmbeddings
            };
            if (saveAndRestoreChatState)
            {
                RestoreChatStateAndMemAndSel();
            }
            return true;
        }
    }
}