#!/usr/bin/env bash
# Build và chạy ứng dụng. --reset để xóa DB và đổ lại dữ liệu mẫu.
set -e
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
WEB="$ROOT/src/Forum.Web"

if [ "$1" = "--reset" ]; then
  rm -f "$WEB/"forum.db*
  echo "Đã xóa DB — sẽ đổ lại dữ liệu mẫu khi chạy."
fi

dotnet build "$ROOT/Forum.sln"
echo "Chạy tại http://localhost:5080 (Ctrl+C để dừng)"
dotnet run --project "$WEB" --launch-profile http
