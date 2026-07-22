# Build, cài trình duyệt Playwright, chạy toàn bộ test E2E.
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

dotnet build (Join-Path $root "Forum.sln")

$pw = Join-Path $root "tests\Forum.Tests.E2E\bin\Debug\net8.0\playwright.ps1"
& $pw install chromium

dotnet test (Join-Path $root "tests\Forum.Tests.E2E\Forum.Tests.E2E.csproj") --no-build --logger "console;verbosity=normal"
