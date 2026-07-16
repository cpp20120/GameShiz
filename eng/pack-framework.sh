#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
version="${1:-0.9.0-preview.1}"
output_dir="${2:-$repo_root/.artifacts/framework-release/$version}"

if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]]; then
  echo "Invalid framework package version: $version" >&2
  exit 2
fi

if [[ -e "$output_dir" ]] && find "$output_dir" -mindepth 1 -print -quit | grep -q .; then
  echo "Release output directory is not empty: $output_dir" >&2
  echo "Choose another output directory or remove this release artifact directory manually." >&2
  exit 2
fi

mkdir -p "$output_dir"

if [[ "${SKIP_RESTORE:-0}" != "1" ]]; then
  dotnet restore "$repo_root/CasinoShiz.slnx"
fi

projects=(
  framework/BotFramework.Contracts/BotFramework.Contracts.csproj
  framework/BotFramework.Scheduling.Abstractions/BotFramework.Scheduling.Abstractions.csproj
  framework/BotFramework.Sdk/BotFramework.Sdk.csproj
  framework/BotFramework.Sdk.Testing/BotFramework.Sdk.Testing.csproj
  framework/BotFramework.Rest/BotFramework.Rest.csproj
  framework/BotFramework.Telegram.Abstractions/BotFramework.Telegram.Abstractions.csproj
  framework/BotFramework.Discord.Abstractions/BotFramework.Discord.Abstractions.csproj
  framework/BotFramework.Client/BotFramework.Client.csproj
  templates/BotFramework.Templates/BotFramework.Templates.csproj
)

multi_target_projects=(
  framework/BotFramework.Contracts/BotFramework.Contracts.csproj
  framework/BotFramework.Scheduling.Abstractions/BotFramework.Scheduling.Abstractions.csproj
  framework/BotFramework.Sdk/BotFramework.Sdk.csproj
  framework/BotFramework.Sdk.Testing/BotFramework.Sdk.Testing.csproj
  framework/BotFramework.Client/BotFramework.Client.csproj
)

for project in "${multi_target_projects[@]}"; do
  dotnet restore "$repo_root/$project" \
    -p:TargetFrameworks=net8.0%3Bnet10.0 \
    -p:TargetFramework= \
    -p:BuildInParallel=false \
    -p:RestoreUseSkipNonexistentTargets=false
done

for project in "${multi_target_projects[@]}"; do
  for target_framework in net8.0 net10.0; do
    dotnet build "$repo_root/$project" \
      --configuration Release \
      --no-restore \
      -p:TargetFramework="$target_framework" \
      -p:BuildInParallel=false \
      -p:BuildProjectReferences=false
  done
done

for project in "${projects[@]}"; do
  dotnet pack "$repo_root/$project" \
    --configuration Release \
    --no-restore \
    --output "$output_dir" \
    -p:BuildInParallel=false \
    -p:BuildProjectReferences=false \
    -p:Version="$version" \
    -p:PackageVersion="$version" \
    -p:EnablePackageValidation=true
done

expected_packages=(
  BotFramework.Contracts
  BotFramework.Scheduling.Abstractions
  BotFramework.Sdk
  BotFramework.Testing
  BotFramework.Rest
  BotFramework.Telegram.Abstractions
  BotFramework.Discord.Abstractions
  BotFramework.Client
  BotFramework.Templates
)

for package_id in "${expected_packages[@]}"; do
  package="$output_dir/$package_id.$version.nupkg"
  test -f "$package"
done

echo "Packed ${#expected_packages[@]} BotFramework packages at $version"
echo "Output: $output_dir"
