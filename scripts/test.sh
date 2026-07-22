#!/usr/bin/env bash
# Build, cài trình duyệt Playwright, chạy toàn bộ test E2E.
set -e
ROOT="$(cd "$(dirname "$0")/.." && pwd)"

dotnet build "$ROOT/Forum.sln"

PW="$ROOT/tests/Forum.Tests.E2E/bin/Debug/net8.0/playwright.ps1"
if command -v pwsh >/dev/null 2>&1; then
  pwsh "$PW" install chromium
else
  powershell -File "$PW" install chromium
fi

dotnet test "$ROOT/tests/Forum.Tests.E2E/Forum.Tests.E2E.csproj" --no-build --logger "console;verbosity=normal"
