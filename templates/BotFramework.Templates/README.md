# BotFramework.GameTemplates

Install the `1.0.0` package and run:

```bash
dotnet new install BotFramework.GameTemplates::1.0.0
dotnet new botframework-game -n CoinFlip --module-id coin-flip
```

The defaults generate REST, Telegram, and Discord adapters, atomic persistence, and tests. Use `--channels rest|telegram|discord|all`, `--persistence atomic|event-sourced|none`, and `--include-tests true|false` to tailor the module.

Generated projects pin the BotFramework `1.0.0` package versions so they can
restore outside the repository. To consume a local build, place the generated
module next to a `NuGet.config` whose first package source is the framework
local feed. The generated infrastructure contains a tenant-aware atomic state
key or a minimal event-sourced aggregate according to `--persistence`.
