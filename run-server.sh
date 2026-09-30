#!/usr/bin/env bash
# 서버 3개(쇼핑몰 5000 / 재고관리 6000 / 대시보드 6001)를 한 번에 실행
cd "$(dirname "$0")" && dotnet run --project src/ShoppingMall.Server -- "$@"
