#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

cd "$repo_root/src/SquadOnAks.Web"
dotnet run \
  --project "$repo_root/test/SquadOnAks.AguiSmoke/SquadOnAks.AguiSmoke.csproj" \
  --no-restore
