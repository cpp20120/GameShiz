#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  eng/run-rest-profile-matrix.sh --pid PID --base-url URL [options]

Runs scenarios sequentially. The default phase is read, so only safe GET
projections are executed. Stateful/template routes remain in the manifest and
must be run with a prepared wrk Lua script and valid game state.

Options:
  --pid PID                 .NET process to attach to
  --base-url URL            REST host, for example http://127.0.0.1:18100
  --tenant ID               tenant route segment (default: e2e)
  --scope ID                scope route segment (default: 42)
  --game NAME               only one family/module, for example horse
  --phase NAME              read, stateful, template, or all (default: read)
  --duration VALUE          per-scenario wrk duration (default: 15s)
  --threads N               per-scenario wrk threads (default: 2)
  --connections N           per-scenario wrk connections (default: 32)
  --header VALUE            request header; may be repeated
  --diagnostic-pid PID      PID visible to dotnet diagnostics (default: --pid)
  --diagnostic-tmpdir DIR   TMPDIR containing the target diagnostic socket
  --script-dir DIR          Lua scripts named <scenario-id>.lua for stateful routes
  --manifest FILE           scenario manifest override
EOF
}

process_id=""
base_url=""
tenant="e2e"
scope="42"
selected_game=""
phase="read"
duration="${PROFILE_DURATION:-15s}"
threads="${PROFILE_THREADS:-2}"
connections="${PROFILE_CONNECTIONS:-32}"
diagnostic_process_id=""
diagnostic_tmpdir="${PROFILE_DIAGNOSTIC_TMPDIR:-}"
manifest="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/rest-profile-scenarios.csv"
script_dir=""
headers=()

while (($# > 0)); do
  case "$1" in
    --pid) process_id="$2"; shift 2 ;;
    --base-url) base_url="$2"; shift 2 ;;
    --tenant) tenant="$2"; shift 2 ;;
    --scope) scope="$2"; shift 2 ;;
    --game) selected_game="$2"; shift 2 ;;
    --phase) phase="$2"; shift 2 ;;
    --duration) duration="$2"; shift 2 ;;
    --threads) threads="$2"; shift 2 ;;
    --connections) connections="$2"; shift 2 ;;
    --header) headers+=("$2"); shift 2 ;;
    --diagnostic-pid) diagnostic_process_id="$2"; shift 2 ;;
    --diagnostic-tmpdir) diagnostic_tmpdir="$2"; shift 2 ;;
    --script-dir) script_dir="$2"; shift 2 ;;
    --manifest) manifest="$2"; shift 2 ;;
    --help|-h) usage; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; usage >&2; exit 2 ;;
  esac
done

if [[ -z "$process_id" || -z "$base_url" ]]; then
  echo "--pid and --base-url are required." >&2
  usage >&2
  exit 2
fi
if [[ ! -f "$manifest" ]]; then
  echo "Manifest does not exist: $manifest" >&2
  exit 1
fi
if [[ -z "$diagnostic_process_id" ]]; then
  diagnostic_process_id="$process_id"
fi
if [[ "$phase" != read && "$phase" != stateful && "$phase" != template && "$phase" != all ]]; then
  echo "Unsupported phase: $phase" >&2
  exit 2
fi

profile_script="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/profile-rest-endpoint.sh"
base_url="${base_url%/}"
scenario_count=0

while IFS=';' read -r id family module method path scenario_phase notes; do
  [[ -z "$id" || "$id" == \#* || "$id" == id ]] && continue
  [[ -n "$selected_game" && "$module" != "$selected_game" && "$family" != "$selected_game" ]] && continue
  [[ "$phase" != all && "$scenario_phase" != "$phase" ]] && continue
  if [[ "$scenario_phase" == template ]]; then
    echo "SKIP $id: path template requires runtime state ($notes)"
    continue
  fi
  scenario_wrk_script=""
  if [[ "$method" != GET ]]; then
    if [[ -z "$script_dir" || ! -f "$script_dir/$id.lua" ]]; then
      echo "SKIP $id: non-GET scenario requires $id.lua ($notes)"
      continue
    fi
    scenario_wrk_script="$script_dir/$id.lua"
  fi

  route_path="${path#/}"
  url="$base_url/api/v1/tenants/$tenant/scopes/$scope/$module/$route_path"
  profile_args=(
    --name "$id"
    --pid "$process_id"
    --url "$url"
    --duration "$duration"
    --threads "$threads"
    --connections "$connections"
  )
  if [[ "$diagnostic_process_id" != "$process_id" ]]; then
    profile_args+=(--diagnostic-pid "$diagnostic_process_id")
  fi
  if [[ -n "$diagnostic_tmpdir" ]]; then
    profile_args+=(--diagnostic-tmpdir "$diagnostic_tmpdir")
  fi
  for header in "${headers[@]}"; do
    profile_args+=(--header "$header")
  done
  if [[ -n "$scenario_wrk_script" ]]; then
    profile_args+=(--wrk-script "$scenario_wrk_script")
  fi

  echo "PROFILE $id [$method $url]"
  "$profile_script" "${profile_args[@]}"
  scenario_count=$((scenario_count + 1))
done <"$manifest"

echo "Completed profiles: $scenario_count"
