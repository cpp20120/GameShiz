#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
feed="$repo_root/.artifacts/local-feed"
rm -rf "$feed"
mkdir -p "$feed"

for project in \
  framework/BotFramework.Contracts/BotFramework.Contracts.csproj \
  framework/BotFramework.Scheduling.Abstractions/BotFramework.Scheduling.Abstractions.csproj \
  framework/BotFramework.Sdk/BotFramework.Sdk.csproj \
  framework/BotFramework.Sdk.Testing/BotFramework.Sdk.Testing.csproj \
  framework/BotFramework.Rest/BotFramework.Rest.csproj \
  framework/BotFramework.Telegram.Abstractions/BotFramework.Telegram.Abstractions.csproj \
  framework/BotFramework.Discord.Abstractions/BotFramework.Discord.Abstractions.csproj \
  framework/BotFramework.Client/BotFramework.Client.csproj \
  templates/BotFramework.Templates/BotFramework.Templates.csproj; do
  # The package consumer is a net10 smoke test; the main CI build restores and
  # validates both net8.0 and net10.0 target assets separately.
  dotnet pack "$repo_root/$project" --configuration Release --output "$feed" --no-restore \
    -p:TargetFrameworks=net10.0 -p:EnablePackageValidation=true
done

dotnet restore "$repo_root/samples/CoinFlip/CoinFlip.Tests/CoinFlip.Tests.csproj" \
  --configfile "$repo_root/samples/CoinFlip/NuGet.config" \
  -p:RestoreUseSkipNonexistentTargets=false

for project in \
  "$repo_root/samples/CoinFlip/CoinFlip.Domain/CoinFlip.Domain.csproj" \
  "$repo_root/samples/CoinFlip/CoinFlip.Contracts/CoinFlip.Contracts.csproj" \
  "$repo_root/samples/CoinFlip/CoinFlip.Application/CoinFlip.Application.csproj" \
  "$repo_root/samples/CoinFlip/CoinFlip.Tests/CoinFlip.Tests.csproj"; do
  dotnet build "$project" \
    --configuration Release \
    --no-restore \
    -p:BuildInParallel=false \
    -p:BuildProjectReferences=false
done

dotnet test "$repo_root/samples/CoinFlip/CoinFlip.Tests/CoinFlip.Tests.csproj" \
  --no-restore \
  --no-build \
  --configuration Release \
  -p:BuildInParallel=false

bash "$repo_root/eng/template-consumer-smoke.sh"
