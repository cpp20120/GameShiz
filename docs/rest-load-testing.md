# REST load testing for Horse

The repository contains a separate test-only REST host for measuring the Horse game endpoint without using a production JWT or the Kubernetes deployment.

The host starts:

1. a disposable PostgreSQL 17 Testcontainer;
2. the real BotFramework backend and Horse module migrations;
3. the real `Games.Horse.Rest` route;
4. a test authentication scheme accepting only `Bearer horse-load-test`.

The default request path is:

```text
wrk → REST authentication/context → tenant provisioning → HorseService → PostgreSQL horse_bets
```

Tenant provisioning is cache-first. Each process keeps a ten-minute `IMemoryCache`
entry; when Redis is enabled, the same marker is shared through `ICacheStore`.
The first miss still provisions the authoritative PostgreSQL rows, while
parallel misses for the same tenant/scope/binding are coalesced into one
provisioning transaction. A Redis failure is safe: it only causes a cache miss
and PostgreSQL remains the source of truth.

The cache key includes the transport, tenant, scope, channel container, and
topic. This keeps Telegram topics or other channel bindings isolated without
putting game state or balances into Redis.

In the distributed Helm/k3s deployment this cache is enabled on each `game-*`
backend and points to the chart's shared `redis` Service. The REST BFF does not
need a database or Redis connection for this path: it forwards the request to
the game backend, where provisioning and the Horse query execute.

Run the separate test with:

```bash
dotnet restore CasinoShiz.slnx
bash eng/horse-rest-load-test.sh
```

The script prints the `wrk` RPS and latency summary and samples resource usage once per second during the load phase:

- Horse REST application RSS via `ps`;
- Horse REST application CPU via `/proc` process counters;
- PostgreSQL Testcontainer memory via `docker stats`;
- PostgreSQL Testcontainer CPU via `docker stats`;
- PostgreSQL transaction rate via `pg_stat_database.xact_commit + xact_rollback`.

The application is a local .NET process in this test, not a Kubernetes pod. The PostgreSQL side is a real Docker Testcontainer. Defaults are two threads, 32 connections, and 15 seconds. They can be changed without editing the repository:

```bash
HORSE_LOAD_TEST_DURATION=30s \
HORSE_LOAD_TEST_THREADS=4 \
HORSE_LOAD_TEST_CONNECTIONS=128 \
bash eng/horse-rest-load-test.sh
```

To isolate the game read and PostgreSQL `horse_bets` query from the per-request tenant auto-provisioning write, run:

```bash
HORSE_LOAD_TEST_SKIP_TENANT_PROVISIONING=true bash eng/horse-rest-load-test.sh
```

That mode is a diagnostic comparison only. The default mode keeps the production REST middleware path. No production database, JWT, Kubernetes secret, or Telegram API is used.
