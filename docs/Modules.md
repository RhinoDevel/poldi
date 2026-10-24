# Modules

When a new user prompt/request is available, Poldi detects (via an LLM used as
[classifier](../poldi/Sel.cs)), if the request is to be interpreted as a chat
message, or if one of the
[modules](../poldi/ChatSingleton/Modules/) shall be activated to process the
current user prompt instead.

If you want to know what the currently implemented
[modules](../poldi/ChatSingleton/Modules/) do, read the following
descriptions.

## Chat history search

If the [classifier](../poldi/Sel.cs) decides that the current user prompt
implies that the user has the intention to search the memory for a specific
topic, the
[chat history search module](../poldi/ChatSingleton/Modules/ChatSearch.cs) is
getting activated.

The module asks the user for confirmation to start the search, first.

The [classifier](../poldi/Sel.cs) decides, if no confirmation is given and the
module is deactivated again, or if the search is really wanted.

If the search is wanted, the topic will be extracted from the user prompt that
initially triggered the
[chat history search module](../poldi/ChatSingleton/Modules/ChatSearch.cs).
The search will also exit early here, if no topic could be extracted.

All chats will be loaded into RAM, then. If there are none, search will exit
early.

The maximum candidate count of chats (configurable via
[settings file](../poldi/mt_config.toml)) having the highest scores calculated
via [reranking model run](../poldi/Rer.cs) (the count of reranker input chat
summaries may have been reduced before the reranker run by an
[embedding vector cosine similarity score ordering](../poldi/Emb.cs), if the
count of chats is higher than the maximum input count of the reranker).
See [Sim singleton](../poldi/Sim.cs).

Beginning with the highest ranking chat, the [classifier](../poldi/Sel.cs)
will look at each chat's summary. The first chat with a summary indicating that
the topic to search for was one topic of that chat, is selected. The module will
exit early, if no such chat was found.

The LLM will summarize everything that was spoken regarding the search topic, in
that chat, while also mentioning the date of that chat.

## Wikipedia search

If the [classifier](../poldi/Sel.cs) comes to the conclusion that the user has
the intention to trigger a Wikipedia search with their current prompt, the
[Wikipedia search module](../poldi/ChatSingleton/Modules/WikiSearch.cs) is
activated.

The module queries the user for the question that shall be answered with the
help of Wikipedia, first.

If the user's answer represents a query (e.g. a question), the Wikipedia module
proceeds. Otherwise, it exits early.

The module tries to extract two topics from the user's question with an LLM.

The same LLM will be queried to get the beginning of a possible answer to the
question. This is done to get an embedding vector that is more similar to the
actual answer to be searched for, later.

That embedding vector (of the beginning-of-an-answer) is then calculated.

The next step is trying to retrieve the actual answer to the user's question.

The first topic extracted from the user's question is used initially for that.

Poldi is querying Wikipedia for pages via that topic (as if you entered the
topic into the search field of the Wikipedia web page).

Only the first returned results are used. Their page titles and (Wikipedia) page
IDs are remembered.

A maybe existing "disambiguation" entry is detected and ignored/skipped.

The [classifier](./../poldi/Sel.cs) can (currently) only distinguish between a
maximum of nine possible answers, so the results will be reduced to the first at
most nine page titles and IDs.

An LLM selects, based on the titles, which article has the highest probability
to hold the answer to the user's question.

The article itself (the so called "extract") is received from Wikipedia. That
article will be formatted so information can be extracted easier, later.

This prepared article is split up into paragraphs.
Each paragraph is split up further, based on the maximum input length of the
embedding vector calculation model to be used.
Some unwanted text is also filtered out here (e.g. enumerations).

Cosine similarity scores of the embedding vectors and the embedding vector of
the beginning-of-an-answer (see above) are [calculated](../poldi/Emb.cs).

If a [reranker](../poldi/Rer.cs) shall be used (a configuration option), the
parts of the article having the highest similarity to the beginning-of-an-answer
and are not more than the maximum input count of the reranking model are order
by that model.

With or without the reranker, the text parts are now ordered by similarity to
the beginning-of-an-answer to the user's original question.

An LLM now tries to extract the actual answer to the question from these text
snippets.

If no articles were found (above) by Wikipedia search or the LLM failed to
extract an answer from the text snippets, the same procedure is done with the
second extracted topic (if such a second topic got initially extracted from the
original question). The article whose extract was already searched for the
answer without success will be ignored (blacklisted).

Even, if the second topic also did not lead to an article that held the answer,
the answer will again be looked for (in the same way as described above) in the
next non-blacklisted page from the search results, where the first topic was
used for the search.

If the answer is not found, the module tells you so, otherwise, the module
gives you the answer and exits.

## Internet search

When the [classifier](../poldi/Sel.cs) decides, that the user prompt represents
a request for an internet search, the
[internet search module](../poldi/ChatSingleton/Modules/WwwSearch.cs) gets
activated.

At first, the module asks the user for the query that shall be responded to by
looking up the answer in the internet.

If the user's response represents a query (e.g. a question), the internet search
module proceeds. Otherwise, it exits early.

If the query is an actual question, the LLM will be used to get the beginning of
a possible answer to that question. This is done to get an embedding vector that
is more similar to the actual answer to be searched for, later.

That embedding vector (of the beginning-of-an-answer) is then calculated.

The [SearXNG](https://docs.searxng.org/) instance (that is configured in the
[TOML file](../poldi/mt_config.toml)) is now queried for the top search results
for the user's original input.

Of these, only the first `[www] maxSearchResults` will be used.

Beginning with the top search result, the search result's website's HTML file
content will be loaded. With [AngleSharp](https://github.com/AngleSharp) and
[Readability.js](https://github.com/mozilla/readability), Poldi tries to
extract a "main article" from that website.

That article will be prepared for chunking, paragraphs will be extracted and
finally, the chunks to-be-used for RAG will be created.

With the help of the cosine similarity score comparance and a reranking score
calculating model (if enabled), the (up to `[www] chunkCountToInject`) chunks
are selected that are most "similar" to the user's query (or to the question's
beginning-of-an-answer, if it is an actual question, see above).

An LLM's context will be filled with these chunks and the LLM will try to
extract the answer.

If successful, the iteration of the top search results is stopped, otherwise,
the next top search result will be chunked and Poldi tries to get the answer
from these chunks, in the same way as described above.

If none of the top search results retrieved by searching via the original user
query lead to an answer, Poldi tries to extract the main topic from the user's
prompt. With that topic, the same search will be done, the top search results
for that topic will be chunked and RAG will be used (as described above) to get
an answer.