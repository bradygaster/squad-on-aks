#!/bin/sh
set -eu
scratch="$(mktemp -d)"
trap 'test -z "${api_pid:-}" || kill "$api_pid"; rm -rf "$scratch"' EXIT
cp -R Api frontend Directory.Build.props Directory.Packages.props "$scratch/"
cd "$scratch"
dotnet restore Api/Api.csproj --ignore-failed-sources -p:NuGetAudit=false
dotnet build Api/Api.csproj --no-restore -o output
mkdir -p "$HOME"
cd frontend
npm config set registry "https://packagefeedproxy.microsoft.io/npm/"
test "$(npm config get registry)" = "https://packagefeedproxy.microsoft.io/npm/"
cp -R /worker/npm-cache "$scratch/npm-cache"
npm ci --offline --cache "$scratch/npm-cache" --ignore-scripts
npm run build -- --base=./
cd ..
ASPNETCORE_URLS=http://127.0.0.1:8085 dotnet output/Api.dll > "$scratch/api.log" 2>&1 &
api_pid=$!
curl -fsS --retry 20 --retry-connrefused --retry-delay 1 http://127.0.0.1:8085/health/ready
curl -fsS http://127.0.0.1:8085/api/answer | node -e '
let body = "";
process.stdin.on("data", chunk => body += chunk);
process.stdin.on("end", () => {
    const result = JSON.parse(body);
    if (typeof result.answer !== "string" || !result.answer.length || result.answer.length > 200)
        process.exit(1);
    console.log("PASS API health, bounded answer contract, and React production build");
});'
