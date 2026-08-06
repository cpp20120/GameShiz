# BotFramework.Text

`BotFramework.Text` provides platform-neutral text-processing primitives for
BotFramework modules.

The package contains reusable normalization, tokenization, span mapping,
analysis, matcher, policy, observer and generic message-effect contracts. It
does not define profanity, spam, advertising or censorship rules. Business
behavior belongs to consumer modules such as `TextRules`.

The main pipeline is:

```text
raw text -> normalization -> tokenization -> analyzers -> policies -> effects
```

Register the pipeline in a host with `AddTextProcessing()` and add only the
analyzers, policies and effect handlers owned by the consuming application.
The framework normalizes and tokenizes each input once and keeps the pipeline
platform-independent.

See the [repository text-processing documentation](https://github.com/cpp20120/GameShiz/blob/master/docs/botframework-text.md)
for the complete API and examples.
