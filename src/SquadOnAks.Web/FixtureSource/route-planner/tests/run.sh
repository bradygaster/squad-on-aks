#!/bin/sh

set -eu

root="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
actual="$(sh "$root/src/flight-plan.sh")"
expected='LAUNCH
ORBIT
LAND'

if [ "$actual" != "$expected" ]; then
    printf 'FAIL route order\nexpected:\n%s\nactual:\n%s\n' "$expected" "$actual" >&2
    exit 1
fi

printf 'PASS route order\n'
