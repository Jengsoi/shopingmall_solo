# 🛒 Basiclike Shopping Mall — C# 버전

**C# / .NET 8** 로 만든 쇼핑몰 프로젝트입니다.
클라이언트 ↔ TCP 소켓 ↔ 서버 ↔ MySQL 구조이며, 통신 프로토콜은 줄바꿈으로 구분한 JSON 입니다.

| 영역 | 사용 기술 |
|---|---|
| GUI | **Avalonia 11** (Windows / Linux / macOS) |
| 통신 | `TcpListener` / `TcpClient` + `async/await` |
| DB | **MySqlConnector** |
| 메시지 | `System.Text.Json` 의 `JsonObject` |

## 폴더 구조

```
ShoppingMall.sln
├─ sql/
│  ├─ schema.sql            테이블 정의
│  └─ member_seed.sql       테스트 계정 (test/1234, admin/admin1234)
├─ src/
│  ├─ ShoppingMall.Protocol      줄바꿈 구분 JSON 메시지 채널, JSON 도우미 (서버·클라이언트 공용)
│  ├─ ShoppingMall.Server        서버 3개를 한 프로그램에서 실행
│  │   ├─ Mall/    쇼핑몰 서버(5000): 회원·상품·장바구니·주문·게시판
│  │   ├─ Admin/   재고관리(6000), 대시보드(6001)
│  │   └─ Data/    DB 접속, 트랜잭션 도우미, 비밀번호 해시
│  ├─ ShoppingMall.Client.Core   화면 없는 통신·모델 계층 (NetworkClient, ShopApi, InventoryApi, DashboardApi)
│  └─ ShoppingMall.Client        Avalonia 화면
├─ tools/e2e_scenario.py    서버 통합 시나리오 테스트 (88개 항목)
├─ run-server.sh / run-client.sh
```

## 실행 방법

1. **.NET 8 SDK** 설치 (https://dotnet.microsoft.com/download)
2. MySQL 에 스키마와 테스트 계정 넣기
   ```bash
   mysql -u root -p < sql/schema.sql
   mysql -u root -p shopping < sql/member_seed.sql
   ```
3. 서버 실행 (쇼핑몰 5000 · 재고관리 6000 · 대시보드 6001 을 한 번에)
   ```bash
   dotnet run --project src/ShoppingMall.Server
   # 하나만: dotnet run --project src/ShoppingMall.Server -- --only mall   (mall | inventory | dashboard)
   ```
4. 클라이언트 실행
   ```bash
   dotnet run --project src/ShoppingMall.Client
   ```
   `test / 1234` 로 로그인하면 쇼핑 화면, `admin / admin1234` 로 로그인하면 관리자 화면(매출 대시보드 + 재고 관리)이 열립니다.

> 처음 빌드할 때 NuGet 에서 `MySqlConnector`, `Avalonia` 패키지를 내려받으므로 인터넷이 필요합니다.
> 리눅스에서는 한글 폰트(예: `fonts-noto-cjk`)가 설치되어 있어야 글자가 보입니다.

### 설정 (환경변수)

| 변수 | 기본값 | 설명 |
|---|---|---|
| `SHOP_DB_HOST` / `SHOP_DB_PORT` | `localhost` / `3306` | MySQL 주소 |
| `SHOP_DB_USER` / `SHOP_DB_PASSWORD` / `SHOP_DB_NAME` | `root` / `1234` / `shopping` | **실제로 쓸 때는 반드시 바꾸세요.** |
| `SHOP_BIND` | `127.0.0.1` | 서버가 열리는 주소 |
| `SHOP_MALL_PORT` / `SHOP_INVENTORY_PORT` / `SHOP_DASHBOARD_PORT` | `5000` / `6000` / `6001` | 서버 포트 |
| `SHOP_HOST` | `127.0.0.1` | (클라이언트) 접속할 서버 주소. 포트 변수는 서버와 같은 이름을 씀 |

## 주요 동작

- 장바구니·회원정보 등은 요청 값이 아니라 **로그인 세션의 회원**을 기준으로 처리합니다.
- 게시판 쓰기/수정/삭제는 로그인이 필요하고, 본인 글·댓글만 수정/삭제할 수 있습니다.
- 주문 상세는 본인 주문(관리자는 전체)만 볼 수 있습니다.
- 재고관리(6000)·대시보드(6001) 서버는 접속 직후 **관리자 계정 로그인**을 요구합니다.
- 주문 시 재고 차감은 `UPDATE ... WHERE stock >= 수량` 한 문장으로 처리해, 동시 주문에서도 재고가 음수가 되지 않습니다.
- 주문/장바구니 수량은 1 이상의 정수만 허용합니다.
- 서버 내부 오류(SQL 오류 등) 내용은 클라이언트에 보내지 않고 서버 로그에만 남깁니다.

## 알아둘 점

- **재고 상태 기준**: 5개 이하를 "재고 부족"으로 표시합니다. 바꾸려면 `InventoryService.LowStockThreshold` 한 곳만 고치면 됩니다.
- **상품을 수정하면 `product_id` 가 바뀝니다** (기존 행 비활성화 + 새 행 생성). 수정 전 상품이 이미 누군가의 장바구니에 있으면 주문 시 "상품을 찾을 수 없습니다"가 나옵니다. 이력 보존을 위한 설계이며, 필요하면 장바구니 목록에서 비활성 상품을 걸러내는 처리를 추가할 수 있습니다.
- 비밀번호는 **SHA-256(솔트 없음)** 으로 저장합니다. (`member_seed.sql` 의 해시와 호환) 실제 서비스라면 bcrypt/Argon2 로 바꾸는 것이 좋습니다.
- 관리자 서버(6000/6001)와 쇼핑몰 서버(5000)는 평문 TCP 입니다. 로컬 개발용이고, 외부에 열려면 TLS 가 필요합니다.

## 테스트

서버와 MySQL 을 띄운 뒤 통합 시나리오 테스트를 실행합니다.
가입/로그인, 장바구니, 주문·재고 차감·롤백, 동시 주문, 재고관리, 게시판 권한, 대시보드 합계 대조 등 88개 항목을 검사합니다.

```bash
pip install pymysql
python3 tools/e2e_scenario.py
```

> 대시보드 합계를 DB 와 대조할 때 `pymysql` 로 직접 접속합니다. DB 접속 정보가 기본값과 다르면 스크립트의 `pymysql.connect` 부분을 고치세요.
