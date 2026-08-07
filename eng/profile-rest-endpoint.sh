#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  eng/profile-rest-endpoint.sh --name NAME --pid PID --url URL [options]

Options:
  --duration VALUE             wrk duration (default: 15s)
  --threads N                  wrk threads (default: 2)
  --connections N              wrk connections (default: 32)
  --header VALUE               request header; may be repeated
  --wrk-script FILE            optional Lua script for POST/stateful routes
  --counters VALUE             dotnet-counters providers
  --trace-profile VALUE        dotnet-trace profile (default: dotnet-sampled-thread-time)
  --output-root DIR            artifact root (default: .artifacts/perf)
  --postgres-container ID      optional Docker container for CPU/RAM/TPS
  --postgres-database NAME     optional database name for TPS
  --postgres-user NAME         PostgreSQL user (default: postgres)
  --postgres-password VALUE    optional PostgreSQL password
  --docker-host VALUE          Docker host (default: unix:///var/run/docker.sock)
  --diagnostic-pid PID         PID visible to dotnet diagnostics (default: --pid)
  --diagnostic-tmpdir DIR      TMPDIR containing the target diagnostic socket
EOF
}

name=""
process_id=""
url=""
duration="${PROFILE_DURATION:-15s}"
threads="${PROFILE_THREADS:-2}"
connections="${PROFILE_CONNECTIONS:-32}"
output_root="${PROFILE_OUTPUT_ROOT:-.artifacts/perf}"
counter_providers="${DOTNET_COUNTERS_PROVIDERS:-System.Runtime,Microsoft.AspNetCore.Hosting}"
trace_profile="${DOTNET_TRACE_PROFILE:-dotnet-sampled-thread-time}"
postgres_container=""
postgres_database="${PROFILE_POSTGRES_DATABASE:-}"
postgres_user="${PROFILE_POSTGRES_USER:-postgres}"
postgres_password="${PROFILE_POSTGRES_PASSWORD:-}"
docker_host="${PROFILE_DOCKER_HOST:-unix:///var/run/docker.sock}"
diagnostic_process_id=""
diagnostic_tmpdir="${PROFILE_DIAGNOSTIC_TMPDIR:-}"
wrk_script=""
headers=()

duration_to_seconds() {
  local value="$1"
  case "$value" in
    *s) printf '%s' "${value%s}" ;;
    *m) awk -v value="${value%m}" 'BEGIN { printf "%.0f", value * 60 }' ;;
    *h) awk -v value="${value%h}" 'BEGIN { printf "%.0f", value * 3600 }' ;;
    *) return 1 ;;
  esac
}

while (($# > 0)); do
  case "$1" in
    --name) name="$2"; shift 2 ;;
    --pid) process_id="$2"; shift 2 ;;
    --url) url="$2"; shift 2 ;;
    --duration) duration="$2"; shift 2 ;;
    --threads) threads="$2"; shift 2 ;;
    --connections) connections="$2"; shift 2 ;;
    --output-root) output_root="$2"; shift 2 ;;
    --counters) counter_providers="$2"; shift 2 ;;
    --trace-profile) trace_profile="$2"; shift 2 ;;
    --header) headers+=("$2"); shift 2 ;;
    --wrk-script) wrk_script="$2"; shift 2 ;;
    --postgres-container) postgres_container="$2"; shift 2 ;;
    --postgres-database) postgres_database="$2"; shift 2 ;;
    --postgres-user) postgres_user="$2"; shift 2 ;;
    --postgres-password) postgres_password="$2"; shift 2 ;;
    --docker-host) docker_host="$2"; shift 2 ;;
    --diagnostic-pid) diagnostic_process_id="$2"; shift 2 ;;
    --diagnostic-tmpdir) diagnostic_tmpdir="$2"; shift 2 ;;
    --help|-h) usage; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; usage >&2; exit 2 ;;
  esac
done

if [[ -z "$name" || -z "$process_id" || -z "$url" ]]; then
  echo "--name, --pid and --url are required." >&2
  usage >&2
  exit 2
fi

if [[ ! "$process_id" =~ ^[0-9]+$ ]]; then
  echo "Invalid process id: $process_id" >&2
  exit 2
fi
if [[ -z "$diagnostic_process_id" ]]; then
  diagnostic_process_id="$process_id"
fi
if [[ ! "$diagnostic_process_id" =~ ^[0-9]+$ ]]; then
  echo "Invalid diagnostic process id: $diagnostic_process_id" >&2
  exit 2
fi

for required_command in dotnet-trace dotnet-counters wrk awk ps; do
  if ! command -v "$required_command" >/dev/null 2>&1; then
    echo "Required command is not available: $required_command" >&2
    exit 1
  fi
done

if ! kill -0 "$process_id" 2>/dev/null; then
  echo "Process is not running: $process_id" >&2
  exit 1
fi

if [[ -n "$wrk_script" && ! -f "$wrk_script" ]]; then
  echo "wrk script does not exist: $wrk_script" >&2
  exit 1
fi

timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
run_dir="$output_root/$name/$timestamp"
mkdir -p "$run_dir"

load_seconds="$(duration_to_seconds "$duration" || printf '%s' 15)"
diagnostic_seconds=$((load_seconds + 10))
diagnostic_days=$((diagnostic_seconds / 86400))
diagnostic_remainder=$((diagnostic_seconds % 86400))
diagnostic_hours=$((diagnostic_remainder / 3600))
diagnostic_remainder=$((diagnostic_remainder % 3600))
diagnostic_minutes=$((diagnostic_remainder / 60))
diagnostic_seconds_remainder=$((diagnostic_remainder % 60))
diagnostic_duration="$(printf '%02d:%02d:%02d:%02d' \
  "$diagnostic_days" "$diagnostic_hours" "$diagnostic_minutes" "$diagnostic_seconds_remainder")"

app_log="$run_dir/app-metadata.txt"
resources_file="$run_dir/resources.tsv"
wrk_log="$run_dir/wrk.log"
trace_log="$run_dir/dotnet-trace.log"
counters_log="$run_dir/dotnet-counters.log"
trace_file="$run_dir/trace.nettrace"
counters_file="$run_dir/counters.csv"

printf '%s\n' \
  "name=$name" \
  "pid=$process_id" \
  "diagnostic_pid=$diagnostic_process_id" \
  "diagnostic_tmpdir=${diagnostic_tmpdir:-default}" \
  "url=$url" \
  "duration=$duration" \
  "diagnostic_duration=$diagnostic_duration" \
  "threads=$threads" \
  "connections=$connections" \
  "counter_providers=$counter_providers" \
  "trace_profile=$trace_profile" \
  "started_at_utc=$timestamp" >"$app_log"

clock_ticks_per_second="$(getconf CLK_TCK)"
trace_pid=""
counters_pid=""
sampler_pid=""
wrk_pid=""

process_cpu_ticks() {
  local pid="$1"
  if [[ "$pid" =~ ^[0-9]+$ && -r "/proc/${pid}/stat" ]]; then
    awk '{ print $14 + $15 }' "/proc/${pid}/stat" 2>/dev/null || true
  fi
}

cpu_percent_since_sample() {
  local previous_ticks="$1"
  local current_ticks="$2"
  local elapsed_seconds="$3"
  if [[ "$previous_ticks" =~ ^[0-9]+([.][0-9]+)?$ \
        && "$current_ticks" =~ ^[0-9]+([.][0-9]+)?$ \
        && "$elapsed_seconds" =~ ^[0-9]+([.][0-9]+)?$ ]]; then
    awk -v previous="$previous_ticks" \
      -v current="$current_ticks" \
      -v elapsed="$elapsed_seconds" \
      -v ticks="$clock_ticks_per_second" \
      'BEGIN { if (elapsed > 0) printf "%.2f", (current - previous) / ticks / elapsed * 100 }'
  fi
}

to_mib() {
  local value="$1"
  case "$value" in
    *GiB) awk -v number="${value%GiB}" 'BEGIN { printf "%.2f", number * 1024 }' ;;
    *MiB) awk -v number="${value%MiB}" 'BEGIN { printf "%.2f", number }' ;;
    *KiB) awk -v number="${value%KiB}" 'BEGIN { printf "%.2f", number / 1024 }' ;;
    *GB) awk -v number="${value%GB}" 'BEGIN { printf "%.2f", number * 1000 / 1024 }' ;;
    *MB) awk -v number="${value%MB}" 'BEGIN { printf "%.2f", number * 1000 / 1024 }' ;;
    *) printf '%s' "" ;;
  esac
}

sample_resources() {
  local previous_time=""
  local previous_app_ticks=""
  local previous_wrk_ticks=""
  local docker_cmd=(env "DOCKER_HOST=$docker_host" docker)
  local postgres_exec=("${docker_cmd[@]}" exec)
  if [[ -n "$postgres_password" ]]; then
    postgres_exec+=( -e "PGPASSWORD=$postgres_password" )
  fi

  while [[ -n "$wrk_pid" ]] && kill -0 "$wrk_pid" 2>/dev/null; do
    local sample_time
    local elapsed_seconds=""
    local app_rss_mib=""
    local app_cpu=""
    local wrk_cpu=""
    local postgres_raw=""
    local postgres_mib=""
    local postgres_cpu=""
    local postgres_transactions=""

    sample_time="$(date +%s.%N)"
    if [[ -n "$previous_time" ]]; then
      elapsed_seconds="$(awk -v current="$sample_time" -v previous="$previous_time" 'BEGIN { print current - previous }')"
    fi

    local app_rss_kib
    app_rss_kib="$(ps -o rss= -p "$process_id" | tr -d ' ' || true)"
    if [[ "$app_rss_kib" =~ ^[0-9]+$ ]]; then
      app_rss_mib="$(awk -v kib="$app_rss_kib" 'BEGIN { printf "%.2f", kib / 1024 }')"
    fi

    local app_ticks
    app_ticks="$(process_cpu_ticks "$process_id")"
    if [[ -n "$elapsed_seconds" ]]; then
      app_cpu="$(cpu_percent_since_sample "$previous_app_ticks" "$app_ticks" "$elapsed_seconds")"
    fi
    previous_app_ticks="$app_ticks"

    if [[ -n "$elapsed_seconds" ]]; then
      local wrk_ticks
      wrk_ticks="$(process_cpu_ticks "$wrk_pid")"
      wrk_cpu="$(cpu_percent_since_sample "$previous_wrk_ticks" "$wrk_ticks" "$elapsed_seconds")"
      previous_wrk_ticks="$wrk_ticks"
    fi

    if [[ -n "$postgres_container" ]]; then
      local postgres_stats
      postgres_stats="$("${docker_cmd[@]}" stats --no-stream --format '{{.CPUPerc}}\t{{.MemUsage}}' "$postgres_container" 2>/dev/null || true)"
      local postgres_stats_cpu
      postgres_stats_cpu="$(awk -F '\t' '{ gsub(/%/, "", $1); print $1 }' <<< "$postgres_stats")"
      postgres_cpu="$postgres_stats_cpu"
      local postgres_used
      postgres_used="$(awk -F '\t' '{ print $2 }' <<< "$postgres_stats" | awk '{ print $1 }')"
      postgres_mib="$(to_mib "$postgres_used")"

      if [[ -n "$postgres_database" ]]; then
        postgres_transactions="$("${postgres_exec[@]}" "$postgres_container" \
          psql -U "$postgres_user" -d "$postgres_database" -Atc \
          'SELECT xact_commit + xact_rollback FROM pg_stat_database WHERE datname = current_database();' \
          2>/dev/null | tr -d '\r' || true)"
      fi
    fi

    printf '%s\t%s\t%s\t%s\t%s\t%s\t%s\n' \
      "$sample_time" \
      "${app_rss_mib:-}" \
      "${app_cpu:-}" \
      "${wrk_cpu:-}" \
      "${postgres_mib:-}" \
      "${postgres_cpu:-}" \
      "${postgres_transactions:-}" >>"$resources_file"

    previous_time="$sample_time"
    sleep 1
  done
}

stop_diagnostic_process() {
  local diagnostic_pid="$1"
  [[ -n "$diagnostic_pid" ]] || return 0
  if kill -0 "$diagnostic_pid" 2>/dev/null; then
    for _ in $(seq 1 20); do
      if ! kill -0 "$diagnostic_pid" 2>/dev/null; then
        break
      fi
      sleep 1
    done
    if kill -0 "$diagnostic_pid" 2>/dev/null; then
      kill -INT "$diagnostic_pid" 2>/dev/null || true
      for _ in $(seq 1 5); do
        if ! kill -0 "$diagnostic_pid" 2>/dev/null; then
          break
        fi
        sleep 1
      done
    fi
    if kill -0 "$diagnostic_pid" 2>/dev/null; then
      kill -TERM "$diagnostic_pid" 2>/dev/null || true
    fi
  fi
  wait "$diagnostic_pid" 2>/dev/null || true
}

cleanup() {
  if [[ -n "$wrk_pid" ]] && kill -0 "$wrk_pid" 2>/dev/null; then
    kill -TERM "$wrk_pid" 2>/dev/null || true
    wait "$wrk_pid" 2>/dev/null || true
  fi
  if [[ -n "$sampler_pid" ]] && kill -0 "$sampler_pid" 2>/dev/null; then
    kill -TERM "$sampler_pid" 2>/dev/null || true
    wait "$sampler_pid" 2>/dev/null || true
  fi
  stop_diagnostic_process "$trace_pid"
  stop_diagnostic_process "$counters_pid"
}
trap cleanup EXIT INT TERM

echo "Profiling $name (PID $process_id)"
if [[ "$diagnostic_process_id" != "$process_id" || -n "$diagnostic_tmpdir" ]]; then
  echo "Diagnostics target: PID $diagnostic_process_id (TMPDIR ${diagnostic_tmpdir:-default})"
fi
echo "Output: $run_dir"

diagnostic_env=()
if [[ -n "$diagnostic_tmpdir" ]]; then
  diagnostic_env=(env "TMPDIR=$diagnostic_tmpdir")
fi

"${diagnostic_env[@]}" dotnet-counters collect \
  --process-id "$diagnostic_process_id" \
  --counters "$counter_providers" \
  --refresh-interval 1 \
  --format csv \
  --duration "$diagnostic_duration" \
  --output "$counters_file" \
  >"$counters_log" 2>&1 &
counters_pid=$!

"${diagnostic_env[@]}" dotnet-trace collect \
  --process-id "$diagnostic_process_id" \
  --profile "$trace_profile" \
  --duration "$diagnostic_duration" \
  --format NetTrace \
  --output "$trace_file" \
  >"$trace_log" 2>&1 &
trace_pid=$!

sleep 2
if ! kill -0 "$trace_pid" 2>/dev/null; then
  echo "dotnet-trace exited before load; see $trace_log" >&2
  exit 1
fi
if ! kill -0 "$counters_pid" 2>/dev/null; then
  echo "dotnet-counters exited before load; see $counters_log" >&2
  exit 1
fi

wrk_args=("-t$threads" "-c$connections" "-d$duration" "--latency")
for header in "${headers[@]}"; do
  wrk_args+=( -H "$header" )
done
if [[ -n "$wrk_script" ]]; then
  wrk_args+=( -s "$wrk_script" )
fi
wrk_args+=("$url")

wrk "${wrk_args[@]}" >"$wrk_log" 2>&1 &
wrk_pid=$!
sample_resources &
sampler_pid=$!

if wait "$wrk_pid"; then
  wrk_exit=0
else
  wrk_exit=$?
fi
wrk_pid=""

if [[ -n "$sampler_pid" ]] && kill -0 "$sampler_pid" 2>/dev/null; then
  kill -TERM "$sampler_pid" 2>/dev/null || true
  wait "$sampler_pid" 2>/dev/null || true
fi
sampler_pid=""
stop_diagnostic_process "$trace_pid"
trace_pid=""
stop_diagnostic_process "$counters_pid"
counters_pid=""

report_file="$run_dir/summary.txt"
{
  echo "Profile: $name"
  echo "PID: $process_id"
  echo "URL: $url"
  echo "Artifacts: $run_dir"
  echo
  cat "$wrk_log"
  echo
  if [[ -s "$resources_file" ]]; then
    awk -F '\t' '
      BEGIN { app_cpu_sum = app_cpu_count = wrk_cpu_sum = wrk_cpu_count = 0; db_cpu_sum = db_cpu_count = tx_first = tx_last = tx_first_time = tx_last_time = 0 }
      $3 != "" { if ($3 > app_cpu_peak) app_cpu_peak = $3; app_cpu_sum += $3; app_cpu_count++ }
      $4 != "" { if ($4 > wrk_cpu_peak) wrk_cpu_peak = $4; wrk_cpu_sum += $4; wrk_cpu_count++ }
      $6 != "" { if ($6 > db_cpu_peak) db_cpu_peak = $6; db_cpu_sum += $6; db_cpu_count++ }
      $7 != "" { if (tx_first_count == 0) { tx_first = $7; tx_first_time = $1; tx_first_count = 1 } tx_last = $7; tx_last_time = $1 }
      END {
        printf "Resource summary:\n"
        if (app_cpu_count) printf "  app CPU: peak %.2f%%, average %.2f%%\n", app_cpu_peak, app_cpu_sum / app_cpu_count; else print "  app CPU: n/a"
        if (wrk_cpu_count) printf "  wrk CPU: peak %.2f%%, average %.2f%%\n", wrk_cpu_peak, wrk_cpu_sum / wrk_cpu_count; else print "  wrk CPU: n/a"
        if (db_cpu_count) printf "  PostgreSQL CPU: peak %.2f%%, average %.2f%%\n", db_cpu_peak, db_cpu_sum / db_cpu_count; else print "  PostgreSQL CPU: n/a"
        if (tx_first_count && tx_last_time > tx_first_time) printf "  PostgreSQL transactions: %.2f TPS\n", (tx_last - tx_first) / (tx_last_time - tx_first_time); else print "  PostgreSQL transactions: n/a"
      }
    ' "$resources_file"
  else
    echo "Resource summary: no samples collected."
  fi
} | tee "$report_file"

echo "Artifacts saved to $run_dir"
exit "$wrk_exit"

# Usage:
#   eng/profile-rest-endpoint.sh --name horse-info --pid 1234 \
#     --url http://127.0.0.1:18100/api/v1/tenants/e2e/scopes/42/horse/info \
#     --header 'Authorization: Bearer horse-load-test'
