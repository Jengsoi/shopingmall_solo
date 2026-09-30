# 🛒 Basiclike Shopping Mall

C# / .NET 8 로 만든 데스크톱 쇼핑몰입니다.
고객은 상품을 둘러보고 장바구니에 담아 주문하고 게시판을 이용할 수 있으며,
관리자는 매출 대시보드와 재고 관리 화면으로 매장을 운영합니다.

클라이언트와 서버는 TCP 소켓으로 통신하고(줄바꿈으로 구분한 JSON 메시지), 데이터는 MySQL 에 저장합니다.

## 기능

**쇼핑 (일반 회원)**
- 회원가입 · 로그인 · 내 정보 수정 · 회원 탈퇴
- 카테고리별 상품 목록, 상품명 검색, 상품 상세
- 장바구니 담기 · 수량 변경 · 삭제
- 주문 및 주문 내역 · 주문 상세 조회
- 게시판: 글 목록/검색/페이지, 글쓰기 · 수정 · 삭제, 댓글

**관리자**
- 매출 대시보드: 기간별 총 매출액, 매출 TOP 5 막대 차트, 카테고리별 매출 비중 도넛 차트
- 재고 관리: 카테고리 추가/수정/비활성화, 상품 추가/수정, 재고 수량과 재고 상태(판매 가능 · 재고 부족 · 품절) 표시

## 기술 스택

| 영역 | 사용 기술 |
|---|---|
| 언어 / 런타임 | C# / .NET 8 |
| GUI | Avalonia 11 (Windows / Linux / macOS) |
| 통신 | `TcpListener` / `TcpClient` + `async/await`, `System.Text.Json` |
| DB | MySQL + MySqlConnector |

## 구조

```
ShoppingMall.sln
├─ sql/
│  ├─ schema.sql            테이블 정의
│  └─ member_seed.sql       테스트 계정 (test/1234, admin/admin1234)
├─ src/
│  ├─ ShoppingMall.Protocol      줄바꿈 구분 JSON 메시지 채널, JSON 도우미 (서버·클라이언트 공용)
│  ├─ ShoppingMall.Server        서버 3개를 한 프로그램에서 실행
│  │   ├─ Mall/    쇼핑몰 서버(5000): 회원·상품·장바구니·주문·게시판
│  │   ├─ Admin/   재고관리 서버(6000), 대시보드 서버(6001)
│  │   └─ Data/    DB 접속, 트랜잭션 도우미, 비밀번호 해시
│  ├─ ShoppingMall.Client.Core   통신·모델 계층 (NetworkClient, ShopApi, InventoryApi, DashboardApi)
│  └─ ShoppingMall.Client        Avalonia 화면 (쇼핑 화면 / 관리자 화면)
├─ tools/e2e_scenario.py    서버 통합 시나리오 테스트
├─ run-server.sh / run-client.sh
```

| 서버 | 포트 | 역할 |
|---|---|---|
| 쇼핑몰 | 5000 | 회원, 상품, 장바구니, 주문, 게시판 |
| 재고관리 | 6000 | 카테고리·상품·재고 관리 (관리자 로그인 필요) |
| 대시보드 | 6001 | 매출 통계 (관리자 로그인 필요) |

## 실행 방법

1. **.NET 8 SDK** 와 **MySQL** 설치
2. 스키마와 테스트 계정 넣기
   ```bash
   mysql -u root -p < sql/schema.sql
   mysql -u root -p shopping < sql/member_seed.sql
   ```
3. 서버 실행 (쇼핑몰 · 재고관리 · 대시보드 서버를 한 번에)
   ```bash
   dotnet run --project src/ShoppingMall.Server
   # 하나만: dotnet run --project src/ShoppingMall.Server -- --only mall   (mall | inventory | dashboard)
   ```
4. 클라이언트 실행
   ```bash
   dotnet run --project src/ShoppingMall.Client
   ```
   `test / 1234` 로 로그인하면 쇼핑 화면, `admin / admin1234` 로 로그인하면 관리자 화면이 열립니다.

> 리눅스에서는 한글 폰트(예: `fonts-noto-cjk`)가 설치되어 있어야 글자가 보입니다.

### 설정 (환경변수)

| 변수 | 기본값 | 설명 |
|---|---|---|
| `SHOP_DB_HOST` / `SHOP_DB_PORT` | `localhost` / `3306` | MySQL 주소 |
| `SHOP_DB_USER` / `SHOP_DB_PASSWORD` / `SHOP_DB_NAME` | `root` / `1234` / `shopping` | MySQL 계정 · DB 이름 |
| `SHOP_BIND` | `127.0.0.1` | 서버가 열리는 주소 |
| `SHOP_MALL_PORT` / `SHOP_INVENTORY_PORT` / `SHOP_DASHBOARD_PORT` | `5000` / `6000` / `6001` | 서버 포트 |
| `SHOP_HOST` | `127.0.0.1` | (클라이언트) 접속할 서버 주소. 포트 변수는 서버와 같은 이름을 씀 |

## 테스트

서버와 MySQL 을 띄운 뒤 통합 시나리오 테스트를 실행합니다.
가입/로그인, 장바구니, 주문·재고 차감·롤백, 동시 주문, 재고관리, 게시판 권한, 대시보드 합계 등을 검사합니다.

```bash
pip install pymysql
python3 tools/e2e_scenario.py
```
