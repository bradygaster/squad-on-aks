#!/bin/sh
set -eu

repository_root="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
cd "$repository_root"

dotnet run --project test/SquadOnAks.ContainerTests/SquadOnAks.ContainerTests.csproj
