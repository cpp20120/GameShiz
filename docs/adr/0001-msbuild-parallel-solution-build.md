# ADR 0001: temporarily serialize solution builds

## Status

Accepted as a temporary SDK workaround (2026-07-11).

## Context

With .NET SDK 10.0.104 / MSBuild 18.0.11, `dotnet build CasinoShiz.slnx --no-restore -m`
fails while evaluating the solution project graph with exit code 1, no diagnostic and no
reported error. The same checkout succeeds with `-m:1`. Setting `BuildInParallel=false`
is therefore not hiding a compilation failure; it avoids an SDK graph-evaluation defect.

Minimal reproduction in this repository:

1. Restore `CasinoShiz.slnx`.
2. Remove or override `BuildInParallel=false`.
3. Run `dotnet build CasinoShiz.slnx --no-restore -m`.
4. Observe `Build FAILED. 0 Warning(s), 0 Error(s)` during `GetTargetFrameworks`.
5. Run the same command with `-m:1`; the solution builds successfully.

## Decision

Keep `BuildInParallel=false` in `Directory.Build.props` until the SDK is upgraded and the
reproduction no longer fails. Individual projects remain safe to build in parallel; only
solution graph traversal is serialized.

## Follow-up

Re-test after every SDK update and remove both the property and this workaround when the
parallel solution build succeeds repeatedly on a clean checkout.
