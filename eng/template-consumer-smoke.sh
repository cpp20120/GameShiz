#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
feed="$repo_root/.artifacts/local-feed"
template_package="$feed/BotFramework.Templates.0.9.0-preview.1.nupkg"
consumer="$(mktemp -d "${TMPDIR:-/tmp}/botframework-template-consumer.XXXXXX")"
hive="$(mktemp -d "${TMPDIR:-/tmp}/botframework-template-hive.XXXXXX")"

cleanup() {
  rm -rf "$consumer" "$hive"
}
trap cleanup EXIT

test -f "$template_package"

export DOTNET_CLI_HOME="$hive"
custom_hive="$hive/template-engine"

dotnet new --debug:custom-hive "$custom_hive" install "$template_package" --force
dotnet new --debug:custom-hive "$custom_hive" botframework-game \
  --name CoinFlip \
  --module-id coin-flip \
  --channels all \
  --persistence atomic \
  --include-tests true \
  --output "$consumer"

while IFS= read -r -d '' project; do
  dotnet restore "$project" \
    --source "$feed" \
    --source https://api.nuget.org/v3/index.json \
    -p:TargetFrameworks=net10.0 \
    --ignore-failed-sources
  dotnet build "$project" --configuration Release \
    -p:TargetFrameworks=net10.0 \
    --no-restore
done < <(find "$consumer" -name '*.csproj' -print0 | sort -z)

dotnet test "$consumer/CoinFlip.Tests/CoinFlip.Tests.csproj" \
  --configuration Release \
  --no-restore \
  -p:TargetFrameworks=net10.0
