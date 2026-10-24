
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

// Marcel Timm, RhinoDevel, 2025nov17

using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Poldi.ChatMem
{
    /// <summary>
    /// The interface to the database.
    /// </summary>
    /// <remarks>
    /// Throws an exception on error.
    /// </remarks>
    internal static class ChatMemDb
    {
        // *********************************************************************
        // *** Static settings for the deletion of "duplicate" statements:   ***
        // *********************************************************************

        private static readonly string[] _yesNo = ["Yes", "No"];
        private static readonly bool _llmWriteToConsole =
            C.D<bool>("llm:writeToConsole");
        private static readonly int _deleteMaxSim =
            C.D<int>("mem:deleteMaxSim");
        private static readonly string _deleteQuestionContains =
            C.D<string>("mem:deleteQuestionContains");
        private static readonly string _deleteQuestionBothTruePossible =
            C.D<string>("mem:deleteQuestionBothTruePossible");

        // *********************************************************************

        /// <remarks>
        /// Enables foreign key support (e.g. restrictions).
        /// 
        /// Use return value with "using" keyword or make sure that it will be
        /// disposed properly in some other kind of way.
        /// </remarks>
        private static SqliteConnection CreateOpenConnection()
        {
            // Do NOT use "using", here (caller does this):
            var retVal = new SqliteConnection(CreateConnStr());

            retVal.Open();

            using var command = new SqliteCommand(
                "PRAGMA foreign_keys = ON;", retVal);

            int pragmaSetResult = command.ExecuteNonQuery();

            if(pragmaSetResult != 0 && pragmaSetResult != 1)
            {
                throw new Exception($"Enabling foreign key support did neither return 0, nor 1, but {pragmaSetResult}.");
            }

            return retVal;
        }

        private static readonly List<string> _ver = new List<string>
        {
            // Version 0:
            """
            CREATE TABLE Version
            (
                Nr integer NOT NULL DEFAULT 0
            );

            INSERT INTO Version ( Nr ) VALUES ( 0 );
            """,

            // Version 1:
            """
            CREATE TABLE Chat
            (
                Nr integer PRIMARY KEY AUTOINCREMENT,
                Utc_created_at text NOT NULL DEFAULT CURRENT_TIMESTAMP,

                Utc_chat_at text NOT NULL,
                Summary text NOT NULL
            );

            CREATE TABLE Message
            (
                Nr integer PRIMARY KEY AUTOINCREMENT,
                Chat_nr integer REFERENCES Chat ON DELETE RESTRICT ON UPDATE RESTRICT,
                Utc_created_at text NOT NULL DEFAULT CURRENT_TIMESTAMP,

                Message_index integer NOT NULL,
                Type integer NOT NULL,
                Content text NOT NULL,
                Interrupted boolean NOT NULL
            );

            CREATE TABLE Statement
            (
                Nr integer PRIMARY KEY AUTOINCREMENT,
                Chat_nr integer REFERENCES Chat ON DELETE RESTRICT ON UPDATE RESTRICT,
                Utc_created_at text NOT NULL DEFAULT CURRENT_TIMESTAMP,

                Statement_index integer NOT NULL,
                Content text NOT NULL,
                Embedding blob NOT NULL
            );
            """,

            /// Version 2:
            """
            ALTER TABLE Message ADD COLUMN Injected_statement_nrs TEXT NOT NULL DEFAULT '';
            """,

            /// Version 3 [only nullable for backwards-compatibility,
            ///            already existing entries need a run of
            ///            ConsoleChat.UpdateEmbeddings()].
            """
            ALTER TABLE Chat ADD COLUMN Embedding blob;
            """
        };

        private static string _filePath = string.Empty;

        private static string CreateConnStr()
        {
            return $"Data Source={_filePath}";
        }

        private static int LoadVer(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();

            command.CommandText = "SELECT Nr FROM Version;";

            using var reader = command.ExecuteReader();

            reader.Read();

            return (int)(long)reader["Nr"];
        }

        /// <param name="connection">
        /// An already open connection.
        /// </param>
        private static void ExecVer(SqliteConnection connection, int i)
        {
            Debug.Assert(0 <= i && i < _ver.Count);

            using var command = connection.CreateCommand();
            int result;

            command.CommandText = _ver[i];

            if (0 < i)
            {
                command.CommandText += Environment.NewLine
                    + Environment.NewLine
                    + $"UPDATE Version SET Nr = {i};";
            }

            result = command.ExecuteNonQuery();
            if (result < 1)
            {
                throw new Exception("Failed to update database!");
            }
        }

        private static void CreateFile()
        {
            Debug.Assert(_filePath.Trim().Length != 0);

            using var connection = CreateOpenConnection();

            ExecVer(connection, 0);
        }

        private static void UpdateFile()
        {
            Debug.Assert(_filePath.Trim().Length != 0);

            int ver;

            if (!System.IO.File.Exists(_filePath))
            {
                CreateFile();
            }

            using var connection = CreateOpenConnection();

            ver = LoadVer(connection);

            for (int i = ver + 1; i < _ver.Count; ++i)
            {
                ExecVer(connection, i);
            }
        }

        private static long LoadLastChatNr()
        {
            Debug.Assert(IsInit());

            long nr;
            string cmdStr;
            using var connection = CreateOpenConnection();
            using var command = connection.CreateCommand();

            cmdStr = "SELECT Nr"
                + " FROM Chat"
                + " ORDER BY Utc_chat_at DESC" // Not created-at.
                + " LIMIT 1";

            command.CommandText = cmdStr;

            using var r = command.ExecuteReader();

            if (!r.Read())
            {
                return 0; // Probably just no entry in DB.
            }
            nr = (long)r["Nr"];
            Debug.Assert(!r.Read()); // The last entry, only.
            Debug.Assert(0 < nr);
            return nr;
        }

        internal static bool IsInit()
        {
            return _filePath.Length != 0;
        }

        internal static Chat? LoadChat(long nr)
        {
            Debug.Assert(IsInit());
            Debug.Assert(0 < nr);

            Chat chat;
            string cmdStr;
            using var connection = CreateOpenConnection();
            using var command = connection.CreateCommand();
            byte[] emb;
            float[] embVec;

            cmdStr = "SELECT Nr"
                    + ", Utc_created_at"

                    + ", Utc_chat_at"
                    + ", Summary"
                    + ", Embedding"
                + " FROM Chat"
                + " WHERE Nr = @Nr";

            command.CommandText = cmdStr;
            command.Parameters.AddWithValue("@Nr", nr);

            using var r = command.ExecuteReader();

            if (!r.Read())
            {
                Debug.Assert(false);
                return null;
            }

            // Only supporting null for backwards-compatibility..
            emb = [];
            if(r["Embedding"] is byte[])
            {
                emb = (byte[])r["Embedding"];
            }
            embVec = new float[emb.Length / sizeof(float)];
            Buffer.BlockCopy(emb, 0, embVec, 0, emb.Length);

            chat = new Chat
            {
                Nr = (long)r["Nr"],
                UtcCreatedAt = DateTime.SpecifyKind(
                    DateTime.Parse((string)r["Utc_created_at"]),
                    DateTimeKind.Utc),

                UtcChatAt = DateTime.SpecifyKind(
                    DateTime.Parse((string)r["Utc_chat_at"]),
                    DateTimeKind.Utc),
                Summary = (string)r["Summary"],
                Embedding = embVec
            };
            Debug.Assert(chat.Nr == nr);
            Debug.Assert(!r.Read()); // The last entry, only.
            return chat;
        }

        internal static Chat? LoadLastChat()
        {
            long nr = LoadLastChatNr();

            if(nr == 0)
            {
                return null;
            }
            return LoadChat(nr);
        }

        private static List<long> GetInjectedStatementNrsAsList(string str)
            => str
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(long.Parse)
                .ToList();

        private static string GetInjectedStatementNrsAsStr(List<long> l)
            => string.Join(',', l);

        private static Message CreateMsg(SqliteDataReader r)
        {
            return new Message
            {
                Nr = (long)r["Nr"],
                ChatNr = r["Chat_nr"] as long?,
                UtcCreatedAt = DateTime.SpecifyKind(
                            DateTime.Parse((string)r["Utc_created_at"]),
                            DateTimeKind.Utc),

                MessageIndex = (int)(long)r["Message_index"],
                Type = (MessageType)(long)r["Type"],
                Content = (string)r["Content"],
                Interrupted = (long)r["Interrupted"] != 0,
                InjectedStatementNrs = GetInjectedStatementNrsAsList(
                    (string)r["Injected_statement_nrs"])
            };
        }

        private static Statement CreateStatement(SqliteDataReader r)
        {
            var emb = (byte[])r["Embedding"];
            var embVec = new float[emb.Length / sizeof(float)];

            Buffer.BlockCopy(emb, 0, embVec, 0, emb.Length);

            return new Statement
            {
                Nr = (long)r["Nr"],
                ChatNr = (long)r["Chat_nr"],
                UtcCreatedAt = DateTime.SpecifyKind(
                        DateTime.Parse((string)r["Utc_created_at"]),
                        DateTimeKind.Utc),

                StatementIndex = (int)(long)r["Statement_index"],
                Content = (string)r["Content"],
                Embedding = embVec
            };
        }

        internal static Message? LoadMsg(long msgNr)
        {
            Debug.Assert(IsInit());
            Debug.Assert(0 < msgNr);

            string cmdStr;
            using var connection = CreateOpenConnection();
            using var command = connection.CreateCommand();

            cmdStr = "SELECT Nr"
                    + ", Chat_nr"
                    + ", Utc_created_at"

                    + ", Message_index"
                    + ", Type"
                    + ", Content"
                    + ", Interrupted"
                    + ", Injected_statement_nrs"
                + " FROM Message"
                + " WHERE Nr = @Nr";

            command.CommandText = cmdStr;
            
            command.Parameters.AddWithValue("@Nr", msgNr);

            using var r = command.ExecuteReader();

            if(!r.Read())
            {
                return null;
            }
            return CreateMsg(r);
        }

        /// <remarks>
        /// Orders by DB nrs., assuming that this ordering reflects the
        /// chronological order of the messages (see assertions).
        /// </remarks>
        internal static List<Message> LoadOrdMsgs(int? chatNr)
        {
            Debug.Assert(IsInit());
            Debug.Assert(chatNr == null || 1 <= chatNr.Value);

            string cmdStr;
            List<Message> messages;
            using var connection = CreateOpenConnection();
            using var command = connection.CreateCommand();

            cmdStr = "SELECT Nr"
                    + ", Chat_nr"
                    + ", Utc_created_at"

                    + ", Message_index"
                    + ", Type"
                    + ", Content"
                    + ", Interrupted"
                    + ", Injected_statement_nrs"
                + " FROM Message"
                + " WHERE"
                    + (chatNr == null
                        ? " Chat_nr IS NULL"
                        : " Chat_nr = @Chatnr")
                + " ORDER BY Nr ASC";

            command.CommandText = cmdStr;

            if(chatNr != null)
            {
                command.Parameters.AddWithValue("@Chatnr", chatNr.Value);
            }

            using var r = command.ExecuteReader();

            messages = new();
            while(r.Read())
            {
                messages.Add(CreateMsg(r));
            }

#if DEBUG
            Debug.Assert(
                messages.Count == 0
                    || (messages[0].MessageIndex == 0
                            && messages[0].Type == MessageType.System));

            for(int i = 1; i < messages.Count; ++i)
            {
                var prev = messages[i - 1];
                var cur = messages[i];

                Debug.Assert(prev.UtcCreatedAt <= cur.UtcCreatedAt);
                // (creation date/times are stored with second-precision, only)

                Debug.Assert(
                    prev.MessageIndex + 1 == cur.MessageIndex
                        || (cur.MessageIndex == 0 // New chat.
                                && cur.Type == MessageType.System));
            }
#endif //DEBUG

            return messages;
        }

        internal static List<Message> LoadOrdMsgsWithoutChat()
        {
            return LoadOrdMsgs(null);
        }

        internal static long LoadLastInsertRowId(SqliteConnection connection)
        {
            string cmdStr;
            using var command = connection.CreateCommand();

            cmdStr = "SELECT last_insert_rowid()";
            command.CommandText = cmdStr;

            return (long)command.ExecuteScalar()!;
        }

        /// <remarks>
        /// Does not care about <see cref="Chat.Nr"/> and
        /// <see cref="Chat.UtcCreatedAt"/> of given <paramref name="chat"/>!
        /// </remarks>
        /// <returns>
        /// DB nr. of created entry.
        /// </returns>
        internal static long InsertChat(Chat chat)
        {
            Debug.Assert(IsInit());

            Debug.Assert(chat.Nr == 0);
            Debug.Assert(chat.UtcCreatedAt == DateTime.MinValue);

            Debug.Assert(DateTime.MinValue < chat.UtcChatAt);
            Debug.Assert(chat.Summary.Trim().Length != 0);

            string cmdStr;
            using var connection = CreateOpenConnection();
            using var command = connection.CreateCommand();
            byte[] embVec;

            cmdStr = "INSERT INTO Chat"
                + " ("
                    + " Utc_chat_at"
                    + ", Summary"
                    + ", Embedding"
                + " ) VALUES ("
                    + " @Utc_chat_at"
                    + ", @Summary"
                    + ", @Embedding"
                + " )";
            command.CommandText = cmdStr;
            command.Parameters.AddWithValue("@Utc_chat_at", chat.UtcChatAt);
            command.Parameters.AddWithValue("@Summary", chat.Summary);

            embVec = new byte[chat.Embedding.Length * sizeof(float)];
            Buffer.BlockCopy(chat.Embedding, 0, embVec, 0, embVec.Length);
            command.Parameters.AddWithValue("@Embedding", embVec);

            if (command.ExecuteNonQuery() != 1)
            {
                throw new Exception(
                    "Failed to write new entry to chat table!");
            }
            return LoadLastInsertRowId(connection);
        }

        internal static void DeleteMsg(Message msg)
        {
            Debug.Assert(IsInit());

            Debug.Assert(0 < msg.Nr);

            string cmdStr;
            using var connection = CreateOpenConnection();
            using var command = connection.CreateCommand();

            cmdStr = "DELETE FROM Message WHERE Nr = @Nr";
            command.CommandText = cmdStr;
            command.Parameters.AddWithValue("@Nr", msg.Nr);

            if (command.ExecuteNonQuery() != 1)
            {
                throw new Exception(
                    "Failed to delete entry from message table!");
            }
        }

        /// <remarks>
        /// Does normally not care about <see cref="Message.Nr"/>,
        /// <see cref="Message.ChatNr"/> and <see cref="Message.UtcCreatedAt"/>
        /// of given <paramref name="msg"/>!
        /// </remarks>
        /// <returns>
        /// DB nr. of created entry.
        /// </returns>
        internal static long InsertMsg(
            Message msg, bool useGivenUtcCreatedAt = false)
        {
            Debug.Assert(IsInit());

            Debug.Assert(msg.Nr == 0);
            Debug.Assert(msg.ChatNr == null);
            Debug.Assert(
                useGivenUtcCreatedAt ||msg.UtcCreatedAt == DateTime.MinValue);

            Debug.Assert(0 <= msg.MessageIndex);
            Debug.Assert(Enum.IsDefined(typeof(MessageType), msg.Type));
            Debug.Assert(msg.Content.Trim().Length != 0);

            string cmdStr;
            using var connection = CreateOpenConnection();
            using var command = connection.CreateCommand();

            cmdStr = "INSERT INTO Message"
                + " ("
                    + " Chat_nr"
                    + ", Message_index"
                    + ", Type"
                    + ", Content"
                    + ", Interrupted"
                    + ", Injected_statement_nrs"
                    + (useGivenUtcCreatedAt ? ", Utc_created_at" : string.Empty)
                + " ) VALUES ("
                    + " NULL"
                    + ", @Message_index"
                    + ", @Type"
                    + ", @Content"
                    + ", @Interrupted"
                    + ", @Injected_statement_nrs"
                    + (useGivenUtcCreatedAt ? ", @Utc_created_at" : string.Empty)
                + " )";
            command.CommandText = cmdStr;
            command.Parameters.AddWithValue(
                "@Message_index", (long)msg.MessageIndex);
            command.Parameters.AddWithValue("@Type", (long)msg.Type);
            command.Parameters.AddWithValue("@Content", msg.Content);
            command.Parameters.AddWithValue("@Interrupted", msg.Interrupted);
            command.Parameters.AddWithValue(
                "@Injected_statement_nrs",
                GetInjectedStatementNrsAsStr(msg.InjectedStatementNrs));
            if(useGivenUtcCreatedAt)
            {
                command.Parameters.AddWithValue(
                    "@Utc_created_at", msg.UtcCreatedAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss"));
            }

            if(command.ExecuteNonQuery() != 1)
            {
                throw new Exception(
                    "Failed to write new entry to message table!");
            }
            return LoadLastInsertRowId(connection);
        }

        internal static Statement? LoadStatement(long statementNr)
        {
            Debug.Assert(IsInit());
            Debug.Assert(0 < statementNr);

            string cmdStr;
            using var connection = CreateOpenConnection();
            using var command = connection.CreateCommand();

            cmdStr = "SELECT Nr"
                    + ", Chat_nr"
                    + ", Utc_created_at"
                    + ", Statement_index"
                    + ", Content"
                    + ", Embedding"
                + " FROM Statement"
                + " WHERE Nr = @Nr";

            command.CommandText = cmdStr;

            command.Parameters.AddWithValue("@Nr", statementNr);

            using var r = command.ExecuteReader();

            if (!r.Read())
            {
                return null;
            }
            return CreateStatement(r);
        }

        internal static List<long> LoadAllChatNrs()
        {
            Debug.Assert(IsInit());

            string cmdStr;
            List<long> chatNrs;
            using var connection = CreateOpenConnection();
            using var command = connection.CreateCommand();

            cmdStr = "SELECT Nr"
                + " FROM Chat"
                + " ORDER BY Nr ASC";

            command.CommandText = cmdStr;

            using var r = command.ExecuteReader();

            chatNrs = new();
            while (r.Read())
            {
                chatNrs.Add((long)r["Nr"]);
            }
            return chatNrs;
        }

        /// <param name="maxMostRecentChats">
        /// If this is higher than 0, it is used as the maximum count of chats
        /// whose statements' nrs. will be returned, where these chats are the
        /// most recent. If 0 or lower, ALL chats' statements' nrs. will be
        /// returned.
        /// </param>
        internal static List<long> LoadAllStatementNrs(
            int maxMostRecentChats)
        {
            Debug.Assert(IsInit());

            string cmdStr;
            List<long> statementNrs;
            using var connection = CreateOpenConnection();
            using var command = connection.CreateCommand();

            cmdStr = "SELECT Nr"
                + " FROM Statement"
                + (0 < maxMostRecentChats
                    ? (" WHERE Chat_nr IN ("
                            + " SELECT Nr"
                                + " FROM Chat"
                                + " ORDER BY Utc_chat_at"
                                + " DESC LIMIT @Maxmostrecentchats"
                        + " )")
                    : string.Empty)
                + " ORDER BY Chat_nr ASC"
                    + ", Statement_index ASC";

            command.CommandText = cmdStr;
            if (0 < maxMostRecentChats)
            {
                command.Parameters.AddWithValue(
                    "@Maxmostrecentchats", (long)maxMostRecentChats);
            }

            using var r = command.ExecuteReader();

            statementNrs = new();
            while (r.Read())
            {
                statementNrs.Add((long)r["Nr"]);
            }
            return statementNrs;
        }

        /// <param name="maxMostRecentChats">
        /// If this is higher than 0, it is used as the maximum count of chats
        /// whose statements will be returned, where these chats are the most
        /// recent. If 0 or lower, ALL chats' statements will be returned.
        /// </param>
        internal static List<Statement> LoadAllStatements(
            int maxMostRecentChats)
        {
            Debug.Assert(IsInit());

            string cmdStr;
            List<Statement> statements;
            using var connection = CreateOpenConnection();
            using var command = connection.CreateCommand();

            cmdStr = "SELECT Nr"
                    + ", Chat_nr"
                    + ", Utc_created_at"
                    + ", Statement_index"
                    + ", Content"
                    + ", Embedding"
                + " FROM Statement"
                + (0 < maxMostRecentChats
                    ? (" WHERE Chat_nr IN ("
                            + " SELECT Nr"
                                + " FROM Chat"
                                + " ORDER BY Utc_chat_at"
                                + " DESC LIMIT @Maxmostrecentchats"
                        + " )")
                    : string.Empty)
                + " ORDER BY Chat_nr ASC"
                    + ", Statement_index ASC";

            command.CommandText = cmdStr;
            if(0 < maxMostRecentChats)
            {
                command.Parameters.AddWithValue(
                    "@Maxmostrecentchats", (long)maxMostRecentChats);
            }

            using var r = command.ExecuteReader();

            statements = new();
            while (r.Read())
            {
                statements.Add(CreateStatement(r));
            }
            return statements;
        }

        private static void DeleteStatement(long statementNr)
        {
            Debug.Assert(IsInit());
            Debug.Assert(0 < statementNr);

            Log.Line($"Entered method for statement nr. {statementNr}.");

            string cmdStr;
            using var connection = CreateOpenConnection();
            using var command = connection.CreateCommand();

            cmdStr = "DELETE FROM Statement WHERE Nr = @Nr";

            command.CommandText = cmdStr;

            command.Parameters.AddWithValue("@Nr", statementNr);

            if (command.ExecuteNonQuery() != 1)
            {
                throw new Exception(
                    "Failed to delete entry from statement table!");
            }
        }

        /// <remarks>
        /// Does not care about <see cref="Statement.Nr"/> and
        /// <see cref="Statement.UtcCreatedAt"/> of given
        /// <paramref name="statement"/>!
        /// </remarks>
        /// <returns>
        /// DB nr. of created entry.
        /// </returns>
        private static long InsertStatement(Statement statement)
        {
            Debug.Assert(IsInit());

            Debug.Assert(statement.Nr == 0);
            Debug.Assert(1 <= statement.ChatNr); // Must already be set!
            Debug.Assert(statement.UtcCreatedAt == DateTime.MinValue);

            Debug.Assert(0 <= statement.StatementIndex);
            Debug.Assert(statement.Content.Trim().Length != 0);
            Debug.Assert(0 < statement.Embedding.Length);

            string cmdStr;
            using var connection = CreateOpenConnection();
            using var command = connection.CreateCommand();
            var embVec = new byte[statement.Embedding.Length * sizeof(float)];

            Buffer.BlockCopy(statement.Embedding, 0, embVec, 0, embVec.Length);

            cmdStr = "INSERT INTO Statement"
                + " ("
                    + " Chat_nr"
                    + ", Statement_index"
                    + ", Content"
                    + ", Embedding"
                + " ) VALUES ("
                    + " @Chat_nr"
                    + ", @Statement_index"
                    + ", @Content"
                    + ", @Embedding"
                + " )";
            command.CommandText = cmdStr;

            command.Parameters.AddWithValue("@Chat_nr", statement.ChatNr);
            command.Parameters.AddWithValue(
                "@Statement_index", (long)statement.StatementIndex);
            command.Parameters.AddWithValue("@Content", statement.Content);
            command.Parameters.AddWithValue("@Embedding", embVec);

            if (command.ExecuteNonQuery() != 1)
            {
                throw new Exception(
                    "Failed to write new entry to statement table!");
            }
            return LoadLastInsertRowId(connection);
        }

        private static void UpdateMsgChatNr(long chatNr, long msgNr)
        {
            Debug.Assert(IsInit());
            Debug.Assert(0 < chatNr);
            Debug.Assert(0 < msgNr);

            string cmdStr;
            using var connection = CreateOpenConnection();
            using var command = connection.CreateCommand();

            cmdStr = "UPDATE Message"
                + " SET Chat_nr = @Chatnr"
                + " WHERE Nr = @Msgnr";

            command.CommandText = cmdStr;
            command.Parameters.AddWithValue("@Chatnr", chatNr);
            command.Parameters.AddWithValue("@Msgnr", msgNr);

            if (command.ExecuteNonQuery() != 1)
            {
                throw new Exception(
                    "Failed to update chat nr. of entry in message table!");
            }
        }

        private static void UpdateMsgChatNr(long chatNr, Message msg)
        {
            Debug.Assert(msg.ChatNr == null);

            UpdateMsgChatNr(chatNr, msg.Nr);
        }

        internal static void UpdateChatEmbedding(long chatNr, float[] embedding)
        {
            Debug.Assert(IsInit());
            Debug.Assert(0 < chatNr);
            Debug.Assert(0 < embedding.Length);

            string cmdStr;
            using var connection = CreateOpenConnection();
            using var command = connection.CreateCommand();
            var embVec = new byte[embedding.Length * sizeof(float)];

            Buffer.BlockCopy(embedding, 0, embVec, 0, embVec.Length);

            cmdStr = "UPDATE Chat"
                + " SET Embedding = @Embedding"
                + " WHERE Nr = @Chatnr";

            command.CommandText = cmdStr;
            command.Parameters.AddWithValue("@Embedding", embVec);
            command.Parameters.AddWithValue("@Chatnr", chatNr);

            if (command.ExecuteNonQuery() != 1)
            {
                throw new Exception(
                    "Failed to update embedding of entry in chat table!");
            }
        }

        internal static void UpdateStatementEmbedding(
            long statementNr, float[] embedding)
        {
            Debug.Assert(IsInit());
            Debug.Assert(0 < statementNr);
            Debug.Assert(0 < embedding.Length);

            string cmdStr;
            using var connection = CreateOpenConnection();
            using var command = connection.CreateCommand();
            var embVec = new byte[embedding.Length * sizeof(float)];

            Buffer.BlockCopy(embedding, 0, embVec, 0, embVec.Length);

            cmdStr = "UPDATE Statement"
                + " SET Embedding = @Embedding"
                + " WHERE Nr = @Statementnr";

            command.CommandText = cmdStr;
            command.Parameters.AddWithValue("@Embedding", embVec);
            command.Parameters.AddWithValue("@Statementnr", statementNr);

            if (command.ExecuteNonQuery() != 1)
            {
                throw new Exception(
                    "Failed to update embedding of entry in statement table!");
            }
        }

        internal static void UpdateMsgChatNrs(long chatNr, List<Message> msgs)
        {
            msgs.ForEach(msg => UpdateMsgChatNr(chatNr, msg));
        }

        /// <summary>
        /// Returns, if statement <paramref name="other"/> will get obsolete by
        /// the (hopefully more recent) statement <paramref name="toKeep"/>.
        /// </summary>
        /// <remarks>
        /// The statements' being duplicates in meaning is just one reason for
        /// this method to be true. Also returns true for contradictions.
        /// 
        /// Does not de-initialize slot <see cref="SlotIndex.Sel"/>.
        /// </remarks>
        internal static bool AreStatementsDuplicates(
            string toKeep, string other)
        {
            Debug.Assert(0 < toKeep.Trim().Length);
            Debug.Assert(0 < other.Trim().Length);

            if (Sel.Create(
                    string.Format(_deleteQuestionContains, toKeep, other),
                    _yesNo,
                    null,
                    _llmWriteToConsole,
                    false)!
                        .GetMaxIndex(1, _yesNo.Length) == 1) // Yes
            {
                Log.Line($"Statement \"{other}\" interpr. as included in \"{toKeep}\"..");
                return true;
            }

            if (Sel.Create(
                    string.Format(
                        _deleteQuestionBothTruePossible, toKeep, other),
                    _yesNo,
                    null,
                    _llmWriteToConsole,
                    false)!
                        .GetMaxIndex(1, _yesNo.Length) == 2) // No
            {
                Log.Line($"Statement \"{other}\" interpr. as contradiction to \"{toKeep}\"..");
                return true;
            }
            return false;
        }

        /// <remarks>
        /// Also modifies <paramref name="tuples"/> on deletion.
        /// </remarks>
        internal static void DeleteAllStatementDuplicates(
            string toKeep, List<Tuple<Statement, SimDoc>> tuples)
        {
            Debug.Assert(0 < tuples.Count);

            List<int>? simIndices;
            List<Tuple<Statement, SimDoc>> candidates;

            simIndices = Sim.GetOrdMostSimIndices(
                toKeep, tuples.Select(t => t.Item2).ToList(), _deleteMaxSim);
            if (simIndices == null)
            {
                Debug.Assert(false); // Should not get here.
                return; // (called method already logged)
            }

            // Must be a list created before the loop, because tuples entries
            // may be removed in loop.
            candidates = simIndices.Select(index => tuples[index]).ToList();

            candidates.ForEach(
                candidate =>
                {
                    if (AreStatementsDuplicates(
                            toKeep, candidate.Item1.Content))
                    {
                        DeleteStatement(candidate.Item1.Nr);
                        tuples.Remove(candidate);
                    }
                });
        }

        internal static void DeleteAllStatementDuplicates()
        {
            List<Tuple<Statement, SimDoc>> tuples;
            int c;

            // Order statements by creation, most-recent last.
            tuples = LoadAllStatements(0)
                .OrderBy(s => s.Nr) // (created-at is second-precision, only..)
                .Select(
                    s => new Tuple<Statement, SimDoc>(
                        s, new SimDoc(s.Content, s.Embedding)))
                .ToList();

            c = tuples.Count;
            for (int i = 0; i < c; ++i)
            {
                var latest = tuples.Last();

                if (tuples.Count == 1)
                {
                    return;
                }
                tuples = tuples.Take(tuples.Count - 1).ToList();

                // Removes "duplicate" from tuples list on detection, too.
                DeleteAllStatementDuplicates(latest.Item1.Content, tuples);
            }
            Sel.DeInit();
        }

        internal static void InsertStatements(
            long chatNr, List<Tuple<string, float[]>> statementAndEmbeddings)
        {
            for(int i = 0; i < statementAndEmbeddings.Count; ++i)
            {
                InsertStatement(
                    new Statement
                    {
                        ChatNr = chatNr,

                        StatementIndex = i,
                        Content = statementAndEmbeddings[i].Item1,
                        Embedding = statementAndEmbeddings[i].Item2
                    });
            }
        }

        internal static void DeInit()
        {
            _filePath = string.Empty;
        }

        internal static void ReInit(string filePath)
        {
            Debug.Assert(filePath.Trim().Length != 0);

            DeInit();
            _filePath = filePath;
            UpdateFile();
        }
    }
}
