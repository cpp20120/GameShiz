#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
port="${HORSE_LOAD_TEST_PORT:-18100}"
duration="${HORSE_LOAD_TEST_DURATION:-15s}"
threads="${HORSE_LOAD_TEST_THREADS:-2}"
connections="${HORSE_LOAD_TEST_CONNECTIONS:-32}"
url="http://127.0.0.1:${port}/api/v1/tenants/e2e/scopes/42/horse/info"
ready_url="http://127.0.0.1:${port}/health/ready"
token="horse-load-test"
log_file="$(mktemp "${TMPDIR:-/tmp}/casinoshiz-horse-load-test.XXXXXX.log")"
stats_file="$(mktemp "${TMPDIR:-/tmp}/casinoshiz-horse-load-stats.XXXXXX.tsv")"
wrk_log_file="$(mktemp "${TMPDIR:-/tmp}/casinoshiz-horse-wrk.XXXXXX.log")"
host_pid=""
stats_pid=""
app_pid=""
database_container_id=""
wrk_pid=""
testcontainers_docker_host="${HORSE_LOAD_TEST_DOCKER_HOST:-unix:///var/run/docker.sock}"
docker_cmd=(env "DOCKER_HOST=${testcontainers_docker_host}" docker)
clock_ticks_per_second="$(getconf CLK_TCK)"

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
    *kB) awk -v number="${value%kB}" 'BEGIN { printf "%.2f", number * 1000 / 1024 / 1024 }' ;;
    *) printf '%s' "" ;;
  esac
}

sample_resources() {
  local previous_time=""
  local previous_app_ticks=""
  local previous_wrk_ticks=""

  while kill -0 "$host_pid" 2>/dev/null; do
    local sample_time
    local elapsed_seconds=""
    local app_rss_kib=""
    local app_rss_mib=""
    local app_cpu=""
    local postgres_raw=""
    local postgres_used=""
    local postgres_mib=""
    local postgres_cpu=""
    local postgres_transactions=""
    local wrk_cpu=""

    sample_time="$(date +%s.%N)"
    if [[ -n "$previous_time" ]]; then
      elapsed_seconds="$(awk -v current="$sample_time" -v previous="$previous_time" 'BEGIN { print current - previous }')"
    fi

    if [[ -n "$app_pid" ]]; then
      app_rss_kib="$(ps -o rss= -p "$app_pid" | tr -d ' ' || true)"
      if [[ "$app_rss_kib" =~ ^[0-9]+$ ]]; then
        app_rss_mib="$(awk -v kib="$app_rss_kib" 'BEGIN { printf "%.2f", kib / 1024 }')"
      fi

      local app_ticks
      app_ticks="$(process_cpu_ticks "$app_pid")"
      if [[ -n "$elapsed_seconds" ]]; then
        app_cpu="$(cpu_percent_since_sample "$previous_app_ticks" "$app_ticks" "$elapsed_seconds")"
      fi
      previous_app_ticks="$app_ticks"
    fi

    if [[ -n "$database_container_id" ]]; then
      postgres_raw="$("${docker_cmd[@]}" stats --no-stream --format '{{.MemUsage}}' "$database_container_id" 2>/dev/null || true)"
      postgres_used="$(awk '{print $1}' <<< "$postgres_raw")"
      postgres_mib="$(to_mib "$postgres_used")"
      postgres_cpu="$("${docker_cmd[@]}" stats --no-stream --format '{{.CPUPerc}}' "$database_container_id" 2>/dev/null | sed 's/%//;s/[[:space:]]//g' || true)"
      postgres_transactions="$("${docker_cmd[@]}" exec "$database_container_id" \
        psql -U postgres -d casinoshiz_horse_load_test -Atc \
        'SELECT xact_commit + xact_rollback FROM pg_stat_database WHERE datname = current_database();' \
        2>/dev/null | tr -d '\r' || true)"
    fi

    if [[ -n "$wrk_pid" && -n "$elapsed_seconds" ]]; then
      local wrk_ticks
      wrk_ticks="$(process_cpu_ticks "$wrk_pid")"
      wrk_cpu="$(cpu_percent_since_sample "$previous_wrk_ticks" "$wrk_ticks" "$elapsed_seconds")"
      previous_wrk_ticks="$wrk_ticks"
    fi

    if [[ -n "$app_rss_mib" || -n "$app_cpu" || -n "$postgres_mib" || -n "$postgres_cpu" || -n "$postgres_transactions" || -n "$wrk_cpu" ]]; then
      printf '%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\n' \
        "$sample_time" \
        "${app_rss_mib:-}" \
        "${app_cpu:-}" \
        "${postgres_mib:-}" \
        "${postgres_raw:-}" \
        "${postgres_cpu:-}" \
        "${postgres_transactions:-}" \
        "${wrk_cpu:-}" >>"$stats_file"
    fi

    previous_time="$sample_time"
    sleep 1
  done
}

report_resources() {
  if [[ ! -s "$stats_file" ]]; then
    echo "Resource sampling: no samples collected."
    return
  fi

  local app_peak_mib
  local app_avg_mib
  local postgres_peak_mib
  local postgres_avg_mib
  local postgres_peak_raw
  local postgres_peak_tps
  local postgres_avg_tps
  local app_peak_cpu
  local app_avg_cpu
  local postgres_peak_cpu
  local postgres_avg_cpu
  local wrk_peak_cpu
  local wrk_avg_cpu
  app_peak_mib="$(awk -F '\t' 'BEGIN { max = 0 } $2 != "" && $2 + 0 > max { max = $2 } END { printf "%.2f", max }' "$stats_file")"
  app_avg_mib="$(awk -F '\t' 'BEGIN { sum = 0; count = 0 } $2 != "" { sum += $2; count++ } END { if (count == 0) print "n/a"; else printf "%.2f", sum / count }' "$stats_file")"
  app_peak_cpu="$(awk -F '\t' 'BEGIN { max = 0 } $3 != "" && $3 + 0 > max { max = $3 } END { if (max == 0) print "n/a"; else printf "%.2f", max }' "$stats_file")"
  app_avg_cpu="$(awk -F '\t' 'BEGIN { sum = 0; count = 0 } $3 != "" { sum += $3; count++ } END { if (count == 0) print "n/a"; else printf "%.2f", sum / count }' "$stats_file")"
  postgres_peak_mib="$(awk -F '\t' 'BEGIN { max = 0 } $4 != "" && $4 + 0 > max { max = $4 } END { printf "%.2f", max }' "$stats_file")"
  postgres_avg_mib="$(awk -F '\t' 'BEGIN { sum = 0; count = 0 } $4 != "" { sum += $4; count++ } END { if (count == 0) print "n/a"; else printf "%.2f", sum / count }' "$stats_file")"
  postgres_peak_raw="$(awk -F '\t' 'BEGIN { max = 0; raw = "" } $4 != "" && $4 + 0 > max { max = $4; raw = $5 } END { print raw }' "$stats_file")"
  postgres_peak_cpu="$(awk -F '\t' 'BEGIN { max = 0 } $6 != "" && $6 + 0 > max { max = $6 } END { if (max == 0) print "n/a"; else printf "%.2f", max }' "$stats_file")"
  postgres_avg_cpu="$(awk -F '\t' 'BEGIN { sum = 0; count = 0 } $6 != "" { sum += $6; count++ } END { if (count == 0) print "n/a"; else printf "%.2f", sum / count }' "$stats_file")"
  postgres_peak_tps="$(awk -F '\t' '
    NR == 1 { previous_time = $1; previous_transactions = $7; next }
    $7 != "" && previous_transactions != "" && $1 > previous_time {
      tps = ($7 - previous_transactions) / ($1 - previous_time)
      if (tps > max) max = tps
    }
    { previous_time = $1; previous_transactions = $7 }
    END { printf "%.2f", max + 0 }
  ' "$stats_file")"
  postgres_avg_tps="$(awk -F '\t' '
    $7 != "" { if (first_transactions == "") { first_time = $1; first_transactions = $7 } last_time = $1; last_transactions = $7 }
    END {
      if (first_transactions == "" || last_time <= first_time) print "n/a"
      else printf "%.2f", (last_transactions - first_transactions) / (last_time - first_time)
    }
  ' "$stats_file")"
  wrk_peak_cpu="$(awk -F '\t' 'BEGIN { max = 0 } $8 != "" && $8 + 0 > max { max = $8 } END { if (max == 0) print "n/a"; else printf "%.2f", max }' "$stats_file")"
  wrk_avg_cpu="$(awk -F '\t' 'BEGIN { sum = 0; count = 0 } $8 != "" { sum += $8; count++ } END { if (count == 0) print "n/a"; else printf "%.2f", sum / count }' "$stats_file")"

  echo "Resource usage during wrk:"
  echo "  Horse REST process RSS: peak ${app_peak_mib} MiB, average ${app_avg_mib} MiB"
  echo "  Horse REST process CPU: peak ${app_peak_cpu}%, average ${app_avg_cpu}%"
  echo "  PostgreSQL container:   peak ${postgres_peak_mib} MiB, average ${postgres_avg_mib} MiB (${postgres_peak_raw})"
  echo "  PostgreSQL container CPU: peak ${postgres_peak_cpu}%, average ${postgres_avg_cpu}%"
  echo "  PostgreSQL transactions: peak ${postgres_peak_tps} TPS, average ${postgres_avg_tps} TPS (xact_commit + xact_rollback)"
  echo "  wrk CPU:                 peak ${wrk_peak_cpu}%, average ${wrk_avg_cpu}%"
}

cleanup() {
  if [[ -n "$stats_pid" ]] && kill -0 "$stats_pid" 2>/dev/null; then
    kill "$stats_pid" 2>/dev/null || true
    wait "$stats_pid" 2>/dev/null || true
  fi
  if [[ -n "$host_pid" ]] && kill -0 "$host_pid" 2>/dev/null; then
    kill "$host_pid" 2>/dev/null || true
    wait "$host_pid" 2>/dev/null || true
  fi
  rm -f "$log_file"
  rm -f "$stats_file"
  rm -f "$wrk_log_file"
}
trap cleanup EXIT INT TERM

dotnet run \
  --project "$repo_root/tests/CasinoShiz.HorseRestLoadTest/CasinoShiz.HorseRestLoadTest.csproj" \
  --configuration Release \
  --no-restore \
  >"$log_file" 2>&1 &
host_pid=$!

ready=0
for _ in $(seq 1 180); do
  if curl --fail --silent "$ready_url" -o /dev/null 2>/dev/null; then
    ready=1
    break
  fi

  if ! kill -0 "$host_pid" 2>/dev/null; then
    echo "Horse REST load-test host exited before becoming ready:" >&2
    sed -n '1,240p' "$log_file" >&2
    exit 1
  fi
  sleep 1
done

if [[ "$ready" != 1 ]]; then
  echo "Horse REST load-test host did not become ready:" >&2
  sed -n '1,240p' "$log_file" >&2
  exit 1
fi

curl --fail --silent --show-error \
  -H "Authorization: Bearer ${token}" \
  "$url" \
  -o /dev/null

app_pid="$(sed -n 's/^HORSE_LOAD_TEST_APP_PROCESS_ID //p' "$log_file" | sed -n '1p')"
database_container_id="$(sed -n 's/^HORSE_LOAD_TEST_DATABASE_CONTAINER_ID //p' "$log_file" | sed -n '1p')"
if [[ ! "$app_pid" =~ ^[0-9]+$ ]]; then
  app_pid=""
fi
if [[ ! "$database_container_id" =~ ^[[:xdigit:]]+$ ]]; then
  database_container_id=""
fi

if [[ -z "$database_container_id" ]]; then
  echo "Warning: PostgreSQL container id was not published; database memory will not be sampled." >&2
fi
if [[ -z "$app_pid" ]]; then
  echo "Warning: Horse REST process id was not published; application memory will not be sampled." >&2
fi

echo "Horse REST load test: ${url}"
echo "PostgreSQL: Testcontainers; token: test-only; duration: ${duration}"
echo "Tenant provisioning bypass: ${HORSE_LOAD_TEST_SKIP_TENANT_PROVISIONING:-false}"

wrk \
  -t"$threads" \
  -c"$connections" \
  -d"$duration" \
  --latency \
  -H "Authorization: Bearer ${token}" \
  "$url" >"$wrk_log_file" 2>&1 &
wrk_pid=$!

sample_resources &
stats_pid=$!

if wait "$wrk_pid"; then
  wrk_exit=0
else
  wrk_exit=$?
fi
wrk_pid=""
sed -n '1,240p' "$wrk_log_file"
if (( wrk_exit != 0 )); then
  exit "$wrk_exit"
fi

if [[ -n "$stats_pid" ]] && kill -0 "$stats_pid" 2>/dev/null; then
  kill "$stats_pid" 2>/dev/null || true
  wait "$stats_pid" 2>/dev/null || true
fi
report_resources
