#!/bin/sh
set -eu

repo_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
workspace="$repo_root/.artifacts/native-casting-probe"
upstream="$repo_root/.artifacts/upstream-squad"
upstream_revision="d5076d91ac8d0bcc46c8f9c7b6d82f6774b60581"

rm -rf "$workspace"
rm -rf "$upstream"
mkdir -p "$workspace"
git -C "$workspace" init --quiet
printf '# Native casting probe fixture\n' > "$workspace/README.md"

git -C "$repo_root/.artifacts" init --quiet upstream-squad
git -C "$upstream" remote add origin https://github.com/bradygaster/squad.git
git -C "$upstream" fetch --depth 1 origin "$upstream_revision"
git -C "$upstream" checkout --quiet --detach FETCH_HEAD

(
  cd "$workspace"
  node "$upstream/index.cjs" init
)

dotnet run --project "$repo_root/test/SquadOnAks.StackProbe" -- "$workspace"
