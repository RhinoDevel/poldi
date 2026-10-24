
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

// Marcel Timm, RhinoDevel, 2025nov25

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Poldi.ChatMem
{
    internal static class ChatMem
    {
        private static readonly string _memSysPrompt = C.D<string>
            ("mem:sysPrompt");

        /// <remarks>
        /// Called "sessions" here (and not "chats"), because these messages do
        /// not have a chat entry associated, yet.
        /// </remarks>
        private static List<List<Message>>? GetSessions(
            List<Message> ordMsgsWithoutChat)
        {
            var retVal = new List<List<Message>>();

            for(int i = 0; i < ordMsgsWithoutChat.Count; ++i)
            {
                var curMsg = ordMsgsWithoutChat[i];

                if(curMsg.ChatNr != null)
                {
                    Debug.Assert(false);
                    Log.Line("Error: Received at least 1 msg. already having a chat associated!");
                    return null;
                }

                if (0 < i
                        && curMsg.UtcCreatedAt
                            < ordMsgsWithoutChat[i - 1].UtcCreatedAt)
                {
                    Debug.Assert(false);
                    Log.Line("Error: Found at least one msg. with creation date earlier than preceding msg.'s!");
                    return null;
                }

                if (curMsg.MessageIndex == 0)
                {
                    // Add a new message list that is expected to begin with a
                    // message of type "system":

                    if(curMsg.Type != MessageType.System)
                    {
                        Debug.Assert(false);
                        Log.Line("Error: Received at least 1 msg. with index 0 being no system msg.!");
                        return null;
                    }

                    Debug.Assert(i == 0 || 0 < retVal.Count);

                    retVal.Add([curMsg]);
                    continue;
                }

                // Expect a follow-up message belonging to an already created
                // session:

                if(retVal.Count == 0)
                {
                    Debug.Assert(false);
                    Log.Line("Error: No sessions, yet, but 1st msg.'s index is not 0!");
                    return null;
                }

                Debug.Assert(0 < retVal.Last().Count);

                if(curMsg.Type == MessageType.System)
                {
                    Debug.Assert(false);
                    Log.Line("Error: Found at least one follow-up msg. with type \"system\"!");
                    return null;
                }
                if (retVal.Last().Last().Type == curMsg.Type)
                {
                    Debug.Assert(false);
                    Log.Line("Error: Found at least one follow-up msg. with the same type as the previous msg.!");
                    return null;
                }
                if (retVal.Last().Last().MessageIndex + 1 != curMsg.MessageIndex)
                {
                    Debug.Assert(false);
                    Log.Line("Error: Found at least one follow-up msg. with index not being 1 more than previous msg.'s!");
                    return null;
                }

                retVal.Last().Add(curMsg);
            }

            return retVal;
        }

        /// <remarks>
        /// Expects the DB to already being initialized.
        /// </remarks>
        private static bool UpdateWithSession(List<Message> session)
        {
            ExtractResult? extractRes;
            long chatNr;

            // ***********************************************************
            // *** Get summary, statements & embeddings of statements: ***
            // ***********************************************************

            if (!Extract.Exec(
                    new ExtractParams
                    {
                        SysPromptDialog = C.D<string>("mem:sysPromptDialog"),
                        Session = session
                    },
                    out extractRes))
            {
                return false; // Error!
            }
            if(extractRes == null)
            {
                return true; // Not an error.
            }

            // *************************************************
            // *** Remove "duplicates" in extraction result: ***
            // *************************************************

            {
                List<Tuple<string, float[]>?> l =
                    extractRes.StatementAndEmbeddings!
                        .Select(t => (Tuple<string, float[]>?)t)
                        .ToList();

                for(int i = l.Count - 1; 1 <= i; --i)
                {
                    if (l[i] != null)
                    {
                        for (int j = i - 1; 0 <= j; --j)
                        {
                            if (l[j] != null
                                    && ChatMemDb.AreStatementsDuplicates(
                                        l[i]!.Item1, l[j]!.Item1))
                            {
                                l[j] = null;
                            }
                        }
                    }
                }

                extractRes.StatementAndEmbeddings = l
                    .Where(t => t != null)
                    .Cast<Tuple<string, float[]>>()
                    .ToList();
            }

            // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
            // !!! WRITING TO DATABASE STARTS BELOW! !!!
            // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

            // ************************************************
            // *** Remove "duplicates" already being in DB: ***
            // ************************************************

            {
                List<Tuple<Statement, SimDoc>> tuples;

                // Order statements by creation, most-recent last.
                tuples = ChatMemDb.LoadAllStatements(0)
                    .OrderBy(
                        s => s.Nr) // (created-at is second-precision, only..)
                    .Select(
                        s => new Tuple<Statement, SimDoc>(
                            s, new SimDoc(s.Content, s.Embedding)))
                    .ToList();

                if (0 < tuples.Count)
                {
                    // Order is not important, as we already established that the
                    // set of new statements does not hold "duplicates".
                    extractRes.StatementAndEmbeddings.ForEach(
                        t =>
                        {
                            // Removes entry from tuples & DB, if identified as
                            // "duplicate".
                            ChatMemDb.DeleteAllStatementDuplicates(
                                t.Item1, tuples);
                        });
                }
                Sel.DeInit();
            }

            // **************************************
            // *** Write chat with summary to DB: ***
            // **************************************

            chatNr = ChatMemDb.InsertChat(extractRes.Chat!);
            Debug.Assert(0 < chatNr);

            // ***************************************************************
            // *** Associate messages (incl. system msg.) with chat entry: ***
            // ***************************************************************

            ChatMemDb.UpdateMsgChatNrs(chatNr, session);

            // ************************************************************
            // *** Write statements ("memories") with embeddings to DB: ***
            // ************************************************************

            ChatMemDb.InsertStatements(
                chatNr, extractRes.StatementAndEmbeddings!);
            return true;
        }

        internal static void Update(bool skipLastSession)
        {
            List<Message> msgsWithoutChat;
            List<List<Message>>? sessions;

            // **********************
            // *** Initialize DB: ***
            // **********************

            ChatMemDb.ReInit(C.D<string>("mem:dbPath"));

            // ********************************************
            // *** Load messages not already processed: ***
            // ********************************************

            msgsWithoutChat = ChatMemDb.LoadOrdMsgsWithoutChat();
            if (msgsWithoutChat.Count == 0)
            {
                Log.Line("No messages without associated chat found. Doing nothing.");
                return;
            }
            Log.Line($"Found {msgsWithoutChat.Count} messages without an associated chat.");

            // ****************************************************************
            // *** Separate messages into chronologically ordered sessions: ***
            // ****************************************************************

            sessions = GetSessions(msgsWithoutChat);
            if(sessions == null)
            {
                Debug.Assert(false); // Must not get here.
                Log.Line("Failed to create session of message without associated chat! Doing nothing..");
                return;
            }

            // ***************************************************
            // *** Update memory with each session seperately: ***
            // ***************************************************

            for(int i = 0;i < sessions.Count; ++i)
            {
                if(skipLastSession && i + 1 == sessions.Count)
                {
                    Log.Line($"Skipping last session {i + 1} of {sessions.Count}, as wanted (via parameter)..");
                    break;
                }

                Log.Line($"Starting update procedure for session {i + 1} of {sessions.Count}..");
                if (!UpdateWithSession(sessions[i]))
                {
                    Debug.Assert(false); // Must not get here.
                    Log.Line("Failed to update with a session's data, stopping update entirely..");
                    return;
                }
            }
        }

        internal static string GetSysPrompt()
        {
            return _memSysPrompt;
        }
    }
}
