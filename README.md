# 🛒 Basiclike Shopping Mall — C# 버전

Python(PySide6 + MySQL) 쇼핑몰을 **C# / .NET 8** 로 옮긴 프로젝트입니다.
구조(클라이언트 ↔ TCP 소켓 ↔ 서버 ↔ MySQL)와 통신 프로토콜(줄바꿈으로 구분한 JSON)은 원본 그대로라서,
기존 Python 클라이언트/서버와도 일부 섞어서 쓸 수 있습니다.

| 원본 (Python) | C# |
|---|---|
| PySide6 GUI | **Avalonia 11** (Windows / Linux / macOS) |
| `socket` + `threading` | `TcpListener` / `TcpClient` + `async/await` |
| `mysql.connector`, `pymysql` | **MySqlConnector** (한 가지로 통일) |
| dict 요청/응답 | `System.Text.Json` 의 `JsonObject` |

## 폴더 구조

```
ShoppingMall.sln
├─ sql/
│  ├─ schema.sql            테이블 정의 (※ 소스의 SQL 에서 추정해 만든 파일, 아래 참고)
│  └─ member_seed.sql       테스트 계정 (test/1234, admin/admin1234)
├─ src/
│  ├─ ShoppingMall.Protocol      줄바꿈 구분 JSON 메시지 채널, JSON 도우미 (서버·클라이언트 공용)
│  ├─ ShoppingMall.Server        서버 3개를 한 프로그램에서 실행
│  │   ├─ Mall/    쇼핑몰 서버(5000): 회원·상품·장바구니·주문·게시판   ← handlers/*.py, server.py
│  │   ├─ Admin/   재고관리(6000), 대시보드(6001)                      ← admin/server/*.py
│  │   └─ Data/    DB 접속, 트랜잭션 도우미, 비밀번호 해시
│  ├─ ShoppingMall.Client.Core   화면 없는 통신·모델 계층 (NetworkClient, ShopApi, InventoryApi, DashboardApi)
│  └─ ShoppingMall.Client        Avalonia 화면                         ← gui/*.py, admin/view/*.py, main.py
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
| `SHOP_DB_USER` / `SHOP_DB_PASSWORD` / `SHOP_DB_NAME` | `root` / `1234` / `shopping` | 원본 `db.py` 와 같은 기본값. **실제로 쓸 때는 반드시 바꾸세요.** |
| `SHOP_BIND` | `127.0.0.1` | 서버가 열리는 주소 |
| `SHOP_MALL_PORT` / `SHOP_INVENTORY_PORT` / `SHOP_DASHBOARD_PORT` | `5000` / `6000` / `6001` | 서버 포트 |
| `SHOP_HOST` | `127.0.0.1` | (클라이언트) 접속할 서버 주소. 포트 변수는 서버와 같은 이름을 씀 |

## 원본과 달라진 점

옮기면서 원본의 문제를 발견해 함께 고친 부분입니다. (기능 동작은 그대로 유지)

**보안**
- 요청에 담긴 `member_id` 를 더 이상 믿지 않고 **로그인 세션의 값만** 사용합니다. (원본은 다른 회원의 `member_id` 를 보내면 그 회원의 장바구니·정보를 다룰 수 있었음)
- 게시판 쓰기/수정/삭제는 로그인 필수입니다. (원본은 세션이 없으면 1번 회원으로 처리하던 임시 코드)
- `order_detail` 은 본인 주문(관리자는 전체)만 볼 수 있습니다.
- 재고관리(6000)·대시보드(6001) 서버는 접속만 하면 관리자로 취급하던 것을, **접속 직후 `member` 테이블로 관리자 로그인**을 요구하도록 바꿨습니다. (원본 `inventory_server.py` 의 "임시 로그인" TODO)
- 서버 내부 오류(SQL 오류 등) 내용은 클라이언트에 보내지 않고 서버 로그에만 남깁니다.

**데이터 정합성**
- `order_create` 의 재고 차감을 `UPDATE ... WHERE stock >= 수량` 한 문장으로 바꿔, 동시 주문에서도 재고가 음수가 되지 않습니다. (재고관리의 `decrease_stock` 과 같은 방식)
- 주문/장바구니 수량은 1 이상의 정수만 허용합니다. (원본은 음수 수량 주문으로 재고를 늘릴 수 있었음)

**버그**
- 재고관리 화면의 재고 칸이 비어 있고, 상품 수정 창을 열면 재고가 0 으로 채워져 그대로 저장하면 재고가 0 이 되던 문제(서버는 `stock`, 화면은 `inventory` 를 읽음)를 고쳤습니다.
- 대시보드에서 조회 결과가 없거나 DB 오류가 나면 화면이 죽던 문제를 고쳤습니다.
- 이미 수정되어 비활성화된 이전 버전 상품을 수정하려 하면 화면에서 먼저 안내합니다.

## 알아둘 점

- **`sql/schema.sql` 은 추정본입니다.** 원본에는 `쇼핑몰_스키마_최종_v2.sql` 이 들어 있지 않아서, 소스의 SQL 문에서 테이블·컬럼을 뽑아 만들었습니다. 팀에서 쓰던 스키마 파일이 있으면 그것을 쓰세요.
- **재고 상태 기준**: 원본 코드는 5개 이하를 "재고 부족"으로 표시하지만, 원본 README 에는 "10개 이하 품절임박"이라고 적혀 있습니다. C# 버전은 코드 동작(5개)을 따랐고, 바꾸려면 `InventoryService.LowStockThreshold` 한 곳만 고치면 됩니다.
- **상품을 수정하면 `product_id` 가 바뀝니다** (기존 행 비활성화 + 새 행 생성). 수정 전 상품이 이미 누군가의 장바구니에 있으면 주문 시 "상품을 찾을 수 없습니다"가 나옵니다. 이력 보존 설계의 트레이드오프이며, 필요하면 장바구니 목록에서 비활성 상품을 걸러내는 처리를 추가할 수 있습니다.
- 비밀번호는 원본과 같은 **SHA-256(솔트 없음)** 을 그대로 씁니다. (`member_seed.sql` 의 해시와 호환) 실제 서비스라면 bcrypt/Argon2 로 바꾸는 것이 좋습니다.
- 관리자 서버(6000/6001)와 쇼핑몰 서버(5000)는 평문 TCP 입니다. 로컬 개발용이고, 외부에 열려면 TLS 가 필요합니다.

## 검증 상태

이 코드를 작성한 환경에서는 NuGet 저장소 접속이 막혀 있어서, **실제 `MySqlConnector` 와 `Avalonia` 패키지로는 빌드하지 못했습니다.** 대신 다음과 같이 확인했습니다.

| 대상 | 확인 방법 | 결과 |
|---|---|---|
| 서버 · 프로토콜 · 통신 계층 | 패키지 대신 같은 인터페이스의 임시 어댑터를 붙여 컴파일 (경고 0) | ✅ |
| 서버 전체 동작 | 실제 MariaDB 10.11 에 붙여 `tools/e2e_scenario.py` 실행 — 가입/로그인, 장바구니, 주문·재고 차감·롤백, **동시 주문 8건 → 재고 3개만 성공**, 재고관리, 게시판 권한, 대시보드 합계 대조 등 **88개 항목 통과** | ✅ |
| 클라이언트 통신·모델 계층 | 서버 오류/미접속/병렬 50건 요청 | ✅ |
| Avalonia 화면 | Avalonia API 를 흉내 낸 임시 스텁으로 컴파일만 확인 (화면을 실제로 띄워 보지는 못함) | ⚠️ |

따라서 **처음 `dotnet build` 할 때 Avalonia 쪽에서 API 이름 차이로 컴파일 오류가 날 수 있고, 화면 배치·글꼴은 직접 실행해서 확인이 필요합니다.**
서버는 실제 드라이버로 한 번 더 확인하려면, 서버를 띄운 뒤 `python3 tools/e2e_scenario.py` 를 실행해 보세요.
