# Digit-logits-based classifier

A relatively small LLM (e.g. 9B or even smaller) can be used as a kind of
classifier without having to rely on the grammar feature of
[llama.cpp](https://github.com/ggml-org/llama.cpp) (faster and simpler).

This is the idea (the details are in the
[code](https://github.com/RhinoDevel/mt_llm/blob/main/mt_llm/mt_llm.cpp)):

Feed the LLM with something like this:

```
What is the user's intention by saying "Play some Robert Johnson!"

Select one of the possible answers below and respond with its number, only.

1: The user wants to know the current temperature.
2: The user wants to do a Wikipedia search.
3: The user wants to listen to music.
4: The user wants to know the current IP address.
5: The user asks about weather information.
```

Obviously you can also replace this with a yes/no question by offering two
answers, only (1 = yes, 2 = no).

When the inference starts, [mt_llm](https://github.com/RhinoDevel/mt_llm) looks
at the tokens being generated.

The mechanism ignores initial control and whitespace tokens.

When the first other token is generated, the inference stops (no further
sampling).

Now, only the digits'(!) raw output logits are looked at.

These logits are converted into probabilities.

The probabilities of possibly existing tokens " 2" and "2" and every other token
that might represent the same digit are added together to a single probability
for that digit (e.g. 2).

100% (or 1.0) is the sum of all probabilities of those digits that were used as
possible answers to the questions.

So for the example above, you would get one probability for 1, 2, 3, 4 and 5,
where their sum would be 1.0.

The output of this classifier then is the digit with the highest of these
probabilities.

You can take a look at the details:

[https://github.com/RhinoDevel/mt_llm/blob/main/mt_llm/mt_llm.cpp](https://github.com/RhinoDevel/mt_llm/blob/main/mt_llm/mt_llm.cpp)

The function

`inference()`

calls

`create_dig_probs()`

under the described circumstances.

I am not a scientist, just a pragmatic software developer, so there may be
better options, but it seems to work fine.

The obvious limitation to a maximum of nine (or ten, with zero) possible options
could be overcome by adding a "None of these." option and asking a follow-up
question with the other possible answers as options (I did not test that, yet).

An interesting side effect is that you can force the LLM to answer any question
this way (at least any multiple-choice question).