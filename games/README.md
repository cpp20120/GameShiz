# Game bounded-context structure

Game code is split by dependency direction, not by Telegram command name.

```text
Games.<Context>.Contracts     interfaces, DTOs and portable domain records
          ↑
          ├── Games.<Context> backend services, rules, persistence, jobs, migrations
          ├── Games.<Context>.Telegram Telegram handlers and presentation
          └── Games.<Context>.Transport.Grpc transport adapters only
```

Rules:

- Contracts must not reference Telegram, Dapper/Npgsql, CAP or gRPC.
- Backend projects must not reference `Telegram.Bot` or Telegram adapter projects.
- Telegram projects parse updates and render replies; they call contract interfaces.
- Transport projects implement those interfaces and hide protobuf/channel details.
- `CasinoShiz.Host` composes backend and Telegram modules locally.
- `CasinoShiz.Backend` and `CasinoShiz.TelegramBff` compose the same contracts over gRPC.
- Background settlement remains backend-owned; client delivery crosses a semantic port/event.

Exceptions are explicit: PixelBattle uses HTTP/SSE for the WebApp,
`Games.NativeDice.Transport.Grpc` serves the five native-dice contexts, and
`Games.Horse.Rendering` is shared presentation-neutral rendering code.

Some recently split contexts still use linked source entries in their `.csproj`
(`Compile Include`/`Compile Remove`). This preserves behavior during migration but
should eventually be replaced by physical file moves. Empty source directories are
not part of the intended structure.
