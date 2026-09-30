#!/usr/bin/env bash
# 클라이언트(쇼핑몰 + 관리자 화면) 실행
cd "$(dirname "$0")" && dotnet run --project src/ShoppingMall.Client
