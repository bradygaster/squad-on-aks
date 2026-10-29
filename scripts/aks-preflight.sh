#!/usr/bin/env bash
set -u

repo_root="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
failures=0

pass() {
  printf 'PASS %s\n' "$1"
}

fail() {
  printf 'FAIL %s\n' "$1" >&2
  failures=$((failures + 1))
}

require_command() {
  if command -v "$1" >/dev/null 2>&1; then
    pass "$1 is installed"
    return 0
  fi

  fail "$1 is not installed or is not on PATH"
  return 1
}

if require_command dotnet; then
  expected_sdk="$(sed -nE 's/^[[:space:]]*"version"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/p' "$repo_root/global.json" | head -n 1)"
  actual_sdk="$(dotnet --version 2>/dev/null || true)"
  if [[ -n "$expected_sdk" && "$actual_sdk" == "$expected_sdk" ]]; then
    pass ".NET SDK matches global.json ($expected_sdk)"
  else
    fail ".NET SDK mismatch (expected $expected_sdk; found ${actual_sdk:-unavailable})"
  fi

  expected_aspire="$(sed -nE '/"aspire.cli"/,/^[[:space:]]*}/s/^[[:space:]]*"version"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/p' "$repo_root/.config/dotnet-tools.json" | head -n 1)"
  actual_aspire="$(dotnet tool run aspire --version 2>/dev/null || true)"
  actual_aspire="${actual_aspire%%+*}"
  if [[ -n "$expected_aspire" && "$actual_aspire" == "$expected_aspire" ]]; then
    pass "Aspire CLI matches the local tool manifest ($expected_aspire)"
  else
    fail "Aspire CLI mismatch (expected $expected_aspire; found ${actual_aspire:-unavailable}); run dotnet tool restore"
  fi
fi

for tool in az docker kubectl helm curl; do
  require_command "$tool" || true
done

if command -v az >/dev/null 2>&1; then
  active_subscription="$(az account show --query id --output tsv 2>/dev/null || true)"
  if [[ -z "$active_subscription" ]]; then
    fail "Azure CLI is not logged in or has no active subscription; run az login and az account set"
  elif [[ -z "${Azure__SubscriptionId:-}" ]]; then
    fail "Azure__SubscriptionId is not set; set it to the intended subscription before deploying"
  elif [[ "$active_subscription" != "$Azure__SubscriptionId" ]]; then
    fail "Active Azure subscription does not match Azure__SubscriptionId"
  else
    pass "Azure CLI is logged in to the requested subscription"
  fi
fi

for variable in Azure__Location Azure__ResourceGroup Parameters__copilot_token; do
  case "$variable" in
    Azure__Location) value="${Azure__Location:-}" ;;
    Azure__ResourceGroup) value="${Azure__ResourceGroup:-}" ;;
    Parameters__copilot_token) value="${Parameters__copilot_token:-}" ;;
  esac

  if [[ -n "$value" ]]; then
    pass "$variable is configured"
  else
    fail "$variable is missing"
  fi
done

if command -v docker >/dev/null 2>&1; then
  if docker info >/dev/null 2>&1; then
    pass "container runtime is reachable"
  else
    fail "container runtime is not reachable; start Docker or the configured Docker-compatible runtime"
  fi
fi

if command -v kubectl >/dev/null 2>&1; then
  if kubectl version --client --output=json >/dev/null 2>&1; then
    pass "kubectl client is usable"
  else
    fail "kubectl client check failed"
  fi
fi

if command -v helm >/dev/null 2>&1; then
  helm_version="$(helm version --short 2>/dev/null || true)"
  if [[ "$helm_version" =~ ^v?([0-9]+)\.([0-9]+)\.([0-9]+)([-+].*)?$ ]] &&
    (( BASH_REMATCH[1] > 4 || (BASH_REMATCH[1] == 4 && BASH_REMATCH[2] >= 2) )); then
    pass "Helm version is at least 4.2.0 (${BASH_REMATCH[0]%%+*})"
  else
    fail "Helm 4.2.0 or later is required (found ${helm_version:-unavailable})"
  fi
fi

if (( failures > 0 )); then
  printf '\nPreflight failed with %d check(s). Resolve the reported items before running aspire deploy.\n' "$failures" >&2
  exit 1
fi

printf '\nPreflight passed. Run aspire deploy --non-interactive to deploy through the AppHost.\n'
