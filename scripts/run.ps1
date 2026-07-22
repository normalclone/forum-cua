# Build và chạy ứng dụng. -Reset để xóa DB và đổ lại dữ liệu mẫu.
param([switch]$Reset)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$web = Join-Path $root "src\Forum.Web"

if ($Reset) {
    Get-ChildItem -Path $web -Filter "forum.db*" -ErrorAction SilentlyContinue | Remove-Item -Force
    Write-Host "Đã xóa DB — sẽ đổ lại dữ liệu mẫu khi chạy." -ForegroundColor Yellow
}

dotnet build (Join-Path $root "Forum.sln")
Write-Host "Chạy tại http://localhost:5080 (Ctrl+C để dừng)" -ForegroundColor Green
dotnet run --project $web --launch-profile http
