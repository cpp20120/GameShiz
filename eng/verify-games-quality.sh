#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
layout_only=false
format_check=false
fail_on_multi=false
apply_split=false
skip_restore=false
coverage_check=false

for arg in "$@"; do
  case "$arg" in
    --layout-only) layout_only=true ;;
    --format) format_check=true ;;
    --fail-on-multi) fail_on_multi=true ;;
    --apply-split) apply_split=true ;;
    --no-restore) skip_restore=true ;;
    --coverage) coverage_check=true ;;
    *) echo "Unknown argument: $arg" >&2; exit 2 ;;
  esac
done

layout_args=(--root "$repo_root/games")
if $fail_on_multi; then
  layout_args+=(--fail-on-multi)
fi
if $apply_split; then
  layout_args+=(--apply --fail-on-multi)
fi
python3 "$repo_root/eng/csharp_layout.py" "${layout_args[@]}"

if $layout_only; then
  exit 0
fi

if ! $skip_restore; then
  while IFS= read -r project; do
    echo "==> dotnet restore ${project#"$repo_root/"}"
    dotnet restore "$project" \
      --force-evaluate \
      --nologo \
      -p:AllowMissingPrunePackageData=true \
      -p:NuGetAudit=false \
      -v:q
  done < <(find "$repo_root/games" -name '*.csproj' -not -path '*/bin/*' -not -path '*/obj/*' -print | sort)
fi

while IFS= read -r project; do
  echo "==> dotnet build ${project#"$repo_root/"}"
  dotnet build "$project" \
    --no-restore \
    --configuration Release \
    --warnaserror \
    --nologo \
    -v:minimal
done < <(find "$repo_root/games" -name '*.csproj' -not -path '*/bin/*' -not -path '*/obj/*' -print | sort)

if $format_check; then
  while IFS= read -r project; do
    echo "==> dotnet format ${project#"$repo_root/"}"
    dotnet format "$project" \
      --no-restore \
      --verify-no-changes \
      --severity warn \
      --verbosity quiet
  done < <(find "$repo_root/games" -name '*.csproj' -not -path '*/bin/*' -not -path '*/obj/*' -print | sort)
fi

if rg -n "ConfigureAwait" "$repo_root/games" --glob '!**/bin/**' --glob '!**/obj/**'; then
  echo "ConfigureAwait is forbidden in games." >&2
  exit 1
fi

if $coverage_check; then
  test_project="$repo_root/tests/CasinoShiz.Tests/CasinoShiz.Tests.csproj"
  echo "==> dotnet test ${test_project#"$repo_root/"} --collect:XPlat Code Coverage"
  dotnet test "$test_project" \
    --no-restore \
    --collect:"XPlat Code Coverage" \
    --nologo \
    -v:minimal

  coverage_file="$(find "$repo_root/tests" -type f -name coverage.cobertura.xml -printf '%T@ %p\n' | sort -n | tail -n 1 | cut -d' ' -f2-)"
  if [[ -z "$coverage_file" ]]; then
    echo "Coverage report was not produced." >&2
    exit 1
  fi
  python3 "$repo_root/eng/check-domain-coverage.py" "$coverage_file"
fi

git -C "$repo_root" diff --check
echo "Games quality checks passed."
