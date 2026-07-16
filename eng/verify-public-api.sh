#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

packages=(
  BotFramework.Contracts
  BotFramework.Sdk
  BotFramework.Sdk.Testing
  BotFramework.Client
  BotFramework.Rest
  BotFramework.Telegram.Abstractions
  BotFramework.Discord.Abstractions
)

for package in "${packages[@]}"; do
  test -f "$repo_root/public-api/$package.PublicAPI.Shipped.txt"
done

test -f "$repo_root/framework/BotFramework.Client/Generated/GeneratedBotFrameworkClient.cs"
grep -qF -- "Generated using the NSwag toolchain" \
  "$repo_root/framework/BotFramework.Client/Generated/GeneratedBotFrameworkClient.cs"

# The package smoke pack enables the SDK's built-in ApiCompat validation. Keep
# the shipped manifests in the repository as the review-facing API inventory.
grep -qF -- "EnablePackageValidation=true" "$repo_root/eng/package-consumer-smoke.sh"

if grep -R -nE -- "Compile Include=.*BotFramework\.Host|Compile Include=.*BotFramework/Host" \
  "$repo_root/framework/BotFramework.Contracts"; then
  echo "BotFramework.Contracts must not compile-link Host sources." >&2
  exit 1
fi

if grep -R -nE -- '/api/\{[^}]+\}/scopes/\{scopeId\}' \
  "$repo_root/framework" "$repo_root/docs" "$repo_root/samples" "$repo_root/templates"; then
  echo "The removed scope-only REST route is still documented or mapped." >&2
  exit 1
fi

echo "Public API baseline manifests and breaking-route checks passed."
