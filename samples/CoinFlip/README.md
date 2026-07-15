# CoinFlip external module

This sample is intentionally outside `framework/` and `games/`. Its projects consume only packed `BotFramework.*` packages from `../../.artifacts/local-feed`; there are no project references to Host or the demonstration games.

Build the package consumer smoke test from the repository root:

```bash
./eng/package-consumer-smoke.sh
```

The sample demonstrates a pure domain, an atomic application service, an isolated state store, and separate REST, Telegram, and Discord adapters. Every adapter resolves a `TenantContext` before invoking the game.
