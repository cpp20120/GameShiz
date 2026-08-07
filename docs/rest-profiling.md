# REST profiling matrix

The repository has one attach-oriented profiling harness for every .NET REST
game backend. It keeps the benchmark and diagnostics separate from game code.

Each run produces an isolated directory under `.artifacts/perf/<scenario>/<utc>`:

- `trace.nettrace` — CPU sampling trace for Visual Studio, PerfView, or
  `dotnet-trace` consumers;
- `counters.csv` — runtime and ASP.NET counters;
- `resources.tsv` — app, `wrk`, and optional PostgreSQL CPU/RSS/TPS samples;
- `wrk.log` — throughput and latency output;
- `summary.txt` — compact result for comparison.

Install the diagnostic tools once:

```bash
dotnet tool install --global dotnet-trace
dotnet tool install --global dotnet-counters
```

## One endpoint

The target process must already be running and diagnostics must be enabled.
The script attaches to its PID, starts `dotnet-trace` and `dotnet-counters`,
runs `wrk`, then stops both collectors and writes the artifacts.

```bash
eng/profile-rest-endpoint.sh \
  --name horse-info \
  --pid "$APP_PID" \
  --url http://127.0.0.1:18100/api/v1/tenants/e2e/scopes/42/horse/info \
  --header 'Authorization: Bearer horse-load-test' \
  --duration 15s \
  --threads 2 \
  --connections 32
```

The default trace profile is `dotnet-sampled-thread-time`. CPU percentages use
the usual Linux convention where 100% is one logical CPU; 1000% means roughly
ten logical CPUs.

## Sequential read matrix

`eng/rest-profile-scenarios.csv` is the route inventory. The matrix runner
profiles safe GET projections one by one, so traces for different games never
mix:

The current measured baseline is recorded in
[rest-baseline.md](rest-baseline.md). It includes the exact parameters,
successful read routes, state-miss/error routes, and games that require a
separate stateful fixture before they can be measured safely.

```bash
eng/run-rest-profile-matrix.sh \
  --pid "$APP_PID" \
  --base-url http://127.0.0.1:18100 \
  --header 'Authorization: Bearer horse-load-test' \
  --duration 15s
```

To profile one game only:

```bash
eng/run-rest-profile-matrix.sh \
  --pid "$APP_PID" \
  --base-url http://127.0.0.1:18100 \
  --game horse \
  --header 'Authorization: Bearer horse-load-test'
```

The matrix includes stateful POST/DELETE and parameterized routes, but skips
them by default. They need a dedicated `wrk` Lua script, valid request bodies,
and deterministic setup/cleanup state. They should be profiled separately
after the read-only baseline is collected.

When a script named after a scenario exists, the matrix runner can execute a
stateful route too:

```bash
eng/run-rest-profile-matrix.sh \
  --pid "$APP_PID" \
  --base-url http://127.0.0.1:18100 \
  --game horse \
  --phase stateful \
  --script-dir eng/profile-scripts/horse \
  --header 'Authorization: Bearer horse-load-test'
```

For example, `eng/profile-scripts/horse/horse-bet.lua` would be used for the
`horse-bet` row. The script is intentionally external to the game module so
test setup and request state remain visible in the profiling harness.

## Scope and limitations

The harness does not silently profile a Kubernetes replica or production
traffic. For the local k3d cluster, the REST process is visible from the host,
while its diagnostic socket remains inside the pod mount namespace. The
existing k3s image contains `nsenter`, so the host PID and the pod diagnostic
PID can be passed separately:

```bash
REST_PID=74236
REST_DIAGNOSTIC_TMPDIR="/proc/$REST_PID/root/tmp"
REPO=/home/cppshizoid/Code/CazinoShiz

docker run --rm --pid=host --privileged \
  --env REST_DEV_TOKEN \
  --entrypoint /bin/sh \
  rancher/k3s:v1.31.5-k3s1 \
  -c "nsenter -t 1 -m -n -u -i -- sh -c 'export PATH=/home/cppshizoid/.dotnet/tools:\$PATH; cd $REPO; \
    $REPO/eng/run-rest-profile-matrix.sh \
      --pid $REST_PID \
      --diagnostic-pid 1 \
      --diagnostic-tmpdir '$REST_DIAGNOSTIC_TMPDIR' \
      --base-url http://127.0.0.1:8080 \
      --header 'Host: api.casinoshiz.localhost' \
      --header 'Authorization: Bearer \$REST_DEV_TOKEN'"'"
```

The command is a local-dev example only. Run it from the repository root and
inject the token in the helper shell. The harness keeps the host PID for
CPU/RSS sampling and uses the diagnostic PID only for `dotnet-trace` and
`dotnet-counters`. PostgreSQL samples remain optional; k3s PostgreSQL is not a
host Docker container, so this path reports app/wrk resources and runtime
artifacts without inventing database TPS. The dev profiling deployment should
set `RateLimit__Enabled=false`; the chart default is `true`, so this does not
change production behavior.
