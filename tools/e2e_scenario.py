# 통합 시나리오 테스트 (서버 + MySQL/MariaDB 가 떠 있어야 함)
#
#   1) mysql -u root -p < sql/schema.sql ; mysql -u root -p shopping < sql/member_seed.sql
#   2) dotnet run --project src/ShoppingMall.Server
#   3) pip install pymysql        (대시보드 합계를 DB 와 대조할 때만 사용)
#   4) python3 tools/e2e_scenario.py
#
# 회원가입 → 로그인 → 장바구니 → 주문(재고 차감/롤백/동시 주문) → 재고관리 → 게시판 → 대시보드 → 탈퇴까지
# 소켓으로 직접 요청을 보내 응답을 검사한다. DB 접속 정보는 아래 pymysql.connect 부분을 환경에 맞게 고치세요.
import socket, json, threading, datetime, sys

class C:
    def __init__(self, port):
        self.s = socket.create_connection(("127.0.0.1", port)); self.f = self.s.makefile("rwb")
    def call(self, **m):
        self.f.write((json.dumps(m, ensure_ascii=False) + "\n").encode()); self.f.flush()
        return json.loads(self.f.readline().decode())
    def many(self, n, **m):
        self.f.write((json.dumps(m, ensure_ascii=False) + "\n").encode()); self.f.flush()
        return [json.loads(self.f.readline().decode()) for _ in range(n)]

fails = 0
def check(name, cond, extra=""):
    global fails
    print(("PASS " if cond else "FAIL ") + name + ("" if cond else f"  -> {extra}"))
    if not cond: fails += 1

ok = lambda r: r.get("status") == "success" or r.get("success") is True
suffix = datetime.datetime.now().strftime("%H%M%S")

# ---------- 관리자(재고 6000) ----------
inv = C(6000)
r = inv.call(type="category_list");                       check("재고: 미인증 요청 거부", not ok(r) and "로그인" in r["message"], r)
r = C(6000).call(type="login", login_id="test", password="1234"); check("재고: 일반회원 로그인 거부", not ok(r), r)
r = C(6000).call(type="login", login_id="admin", password="bad");  check("재고: 잘못된 비번 거부", not ok(r), r)
r = inv.call(type="login", login_id="admin", password="admin1234"); check("재고: 관리자 로그인", ok(r), r)

r = inv.call(type="category_add", name="의류"+suffix);     check("카테고리 추가", ok(r), r); cat = r["category_id"]
r = inv.call(type="category_add", name="의류"+suffix);     check("카테고리 중복 거부", not ok(r), r)
r = inv.call(type="category_add", name="  ");             check("카테고리명 공백 거부", not ok(r), r)
r = inv.call(type="category_update", category_id=cat, name="패션"+suffix, is_active=True); check("카테고리 수정", ok(r), r)
r = inv.call(type="category_update", category_id=cat, name="패션"+suffix, is_active="yes");  check("is_active 타입 검사", not ok(r), r)
r = inv.call(type="category_list");                        check("카테고리 목록", ok(r) and any(c["category_id"]==cat and c["is_active"] is True for c in r["categories"]), r)
r = inv.call(type="category_add", name="비활성"+suffix); dead = r["category_id"]
inv.call(type="category_update", category_id=dead, name="비활성"+suffix, is_active=False)

def padd(**kw):
    d = dict(type="product_add", category_id=cat, name="티셔츠"+suffix, description="면 100%", color="검정", size="L", price=15000, inventory=10); d.update(kw)
    return inv.call(**d)
r = padd();                                               check("상품 추가", ok(r) and r["stock_status"]=="판매 가능", r); p1 = r["product_id"]
r = padd(name="한정판"+suffix, inventory=3, price=30000); check("상품 추가(재고 3 → 재고 부족)", ok(r) and r["stock_status"]=="재고 부족", r); p2 = r["product_id"]
r = padd(category_id=dead);                               check("비활성 카테고리 상품 추가 거부", not ok(r), r)
r = padd(price=-1);                                       check("음수 가격 거부", not ok(r), r)
r = padd(inventory=True);                                 check("재고 bool 거부", not ok(r), r)
r = padd(name=" ");                                       check("상품명 공백 거부", not ok(r), r)
r = inv.call(type="inventory_product_list")
prods = {p["product_id"]: p for p in r["products"]}
check("관리자 상품목록 필드", ok(r) and prods[p1]["stock"]==10 and prods[p1]["inventory"]==10 and prods[p1]["is_active"] is True and prods[p1]["category_name"]=="패션"+suffix, prods.get(p1))

# ---------- 일반 회원(쇼핑몰 5000) ----------
u = C(5000)
newid = "user"+suffix
r = u.call(action="check_id", login_id=newid);            check("아이디 사용 가능", ok(r) and r["data"]["available"] is True, r)
r = u.call(action="signup", login_id=newid, password="pw1234", name="신규", address="광주", email="a@b.c", phone="010", gender="M"); check("회원가입", ok(r), r)
r = u.call(action="signup", login_id=newid, password="pw1234", name="신규");  check("아이디 중복 가입 거부", not ok(r), r)
r = u.call(action="check_id", login_id=newid);            check("아이디 사용 불가", ok(r) and r["data"]["available"] is False, r)
r = u.call(action="cart_list");                           check("비로그인 장바구니 거부", not ok(r), r)
r = u.call(action="login", login_id=newid, password="wrong"); check("로그인 실패", not ok(r), r)
r = u.call(action="login", login_id=newid, password="pw1234"); check("로그인 성공", ok(r) and r["data"]["role"]=="USER", r); me = r["data"]["member_id"]
r = u.call(action="member_info");                         check("회원정보 조회", ok(r) and r["data"]["name"]=="신규" and r["data"]["gender"]=="M", r)
r = u.call(action="member_update", name="신규2", phone="010-1", email=None); check("회원정보 수정(None 무시)", ok(r), r)
r = u.call(action="member_info");                         check("수정 반영", r["data"]["name"]=="신규2" and r["data"]["phone"]=="010-1" and r["data"]["email"]=="a@b.c", r)
r = u.call(action="admin_member_list");                   check("관리자 action 가드", not ok(r), r)

r = u.call(action="category_list");                       check("카테고리(활성만)", ok(r) and any(c["category_id"]==cat for c in r["data"]) and not any(c["category_id"]==dead for c in r["data"]), r)
r = u.call(action="product_list", category_id=cat, keyword="티셔츠"); check("상품 검색", ok(r) and [p["product_id"] for p in r["data"]]==[p1], r)
r = u.call(action="product_list", category_id=None, keyword=""); check("상품 전체", ok(r) and {p1,p2} <= {p["product_id"] for p in r["data"]}, r)
r = u.call(action="product_detail", product_id=p1);        check("상품 상세", ok(r) and r["data"]["category_name"]=="패션"+suffix and r["data"]["price"]==15000, r)
r = u.call(action="product_detail", product_id=999999);   check("없는 상품", not ok(r), r)

r = u.call(action="cart_add", product_id=p1, quantity=2); check("장바구니 담기", ok(r), r)
r = u.call(action="cart_add", product_id=p1, quantity=1); check("장바구니 수량 합산", ok(r), r)
r = u.call(action="cart_add", product_id=p2, quantity=99);check("재고 초과 담기 거부", not ok(r) and "재고" in r["message"], r)
r = u.call(action="cart_add", product_id=p2, quantity=-3);check("음수 수량 담기 거부", not ok(r), r)
r = u.call(action="cart_add", product_id=p2, quantity=1); check("두 번째 상품 담기", ok(r), r)
r = u.call(action="cart_list");                           cart = {c["product_id"]: c for c in r["data"]}
check("장바구니 목록", ok(r) and cart[p1]["quantity"]==3 and cart[p2]["quantity"]==1 and cart[p1]["price"]==15000, r)
r = u.call(action="cart_update", cart_id=cart[p2]["cart_id"], quantity=0); check("수량 0 거부", not ok(r), r)
r = u.call(action="cart_update", cart_id=cart[p2]["cart_id"], quantity=2); check("수량 변경", ok(r), r)

# member_id 위조: 다른 회원(1번)의 장바구니를 보려는 시도
r = u.call(action="cart_list", member_id=1);              check("member_id 위조 무시(내 장바구니만)", ok(r) and len(r["data"])==2, r)

# ---------- 주문 ----------
r = u.call(action="order_create", order_items=[{"cart_id": cart[p1]["cart_id"], "product_id": p1, "quantity": 3}, {"cart_id": cart[p2]["cart_id"], "product_id": p2, "quantity": 2}])
check("주문 생성", ok(r) and r["data"]["order_id"]>0, r); oid = r["data"]["order_id"]
r = u.call(action="cart_list");                           check("주문한 장바구니 항목 삭제", ok(r) and r["data"]==[], r)
prods = {p["product_id"]: p for p in inv.call(type="inventory_product_list")["products"]}
check("재고 차감(10→7, 3→1)", prods[p1]["stock"]==7 and prods[p2]["stock"]==1, (prods[p1]["stock"], prods[p2]["stock"]))
check("재고 상태 갱신", prods[p2]["stock_status"]=="재고 부족", prods[p2])
r = u.call(action="order_list");                          check("주문 목록", ok(r) and r["data"][0]["order_id"]==oid and r["data"][0]["status"]=="PAID", r)
r = u.call(action="order_detail", order_id=oid);          check("주문 상세", ok(r) and len(r["data"]["items"])==2 and sum(i["price"]*i["quantity"] for i in r["data"]["items"])==15000*3+30000*2, r)

# 롤백: 두 번째 상품 재고 부족 → 첫 상품 차감도 취소, 주문도 남지 않아야 함
before = u.call(action="order_list")["data"]
r = u.call(action="order_create", order_items=[{"product_id": p1, "quantity": 1}, {"product_id": p2, "quantity": 5}])
check("재고 부족 주문 거부", not ok(r) and "재고" in r["message"], r)
prods = {p["product_id"]: p for p in inv.call(type="inventory_product_list")["products"]}
check("롤백: 재고 그대로", prods[p1]["stock"]==7 and prods[p2]["stock"]==1, (prods[p1]["stock"], prods[p2]["stock"]))
check("롤백: 주문 안 생김", u.call(action="order_list")["data"]==before)
r = u.call(action="order_create", order_items=[{"product_id": p1, "quantity": -5}]); check("음수 수량 주문 거부(재고 증가 방지)", not ok(r), r)
r = u.call(action="order_create", order_items=[]);        check("빈 주문 거부", not ok(r), r)

# 다른 회원 주문 조회 차단
other = C(5000); other.call(action="login", login_id="test", password="1234")
r = other.call(action="order_detail", order_id=oid);      check("남의 주문 상세 조회 차단", not ok(r), r)

# ---------- 주문 취소 & 재고 복구 ----------
r = u.call(action="order_list");                          check("주문 목록 결제금액", ok(r) and r["data"][0]["total_price"]==15000*3+30000*2, r)
r = u.call(action="order_create", order_items=[{"product_id": p2, "quantity": 1}]); oc = r["data"]["order_id"]
stock = {p["product_id"]: p for p in inv.call(type="inventory_product_list")["products"]}[p2]["stock"]
check("취소용 주문 생성(재고 1→0)", ok(r) and stock == 0, (r, stock))
r = C(5000).call(action="order_cancel", order_id=oc);    check("비로그인 주문 취소 거부", not ok(r), r)
r = other.call(action="order_cancel", order_id=oc);      check("남의 주문 취소 거부", not ok(r), r)
r = u.call(action="order_cancel", order_id=999999);      check("없는 주문 취소 거부", not ok(r), r)
r = u.call(action="order_cancel", order_id=oc);          check("주문 취소", ok(r), r)
prods = {p["product_id"]: p for p in inv.call(type="inventory_product_list")["products"]}
check("취소 후 재고 복구(0→1)", prods[p2]["stock"]==1 and prods[p2]["stock_status"]=="재고 부족", prods[p2])
r = u.call(action="order_list");                          check("취소 상태 반영", ok(r) and {o["order_id"]: o["status"] for o in r["data"]}[oc]=="CANCELLED", r)
r = u.call(action="order_cancel", order_id=oc);          check("이미 취소된 주문 재취소 거부", not ok(r), r)
stock = {p["product_id"]: p for p in inv.call(type="inventory_product_list")["products"]}[p2]["stock"]
check("재취소 시도 후 재고 그대로(1)", stock == 1, stock)
r = u.call(action="order_detail", order_id=oc);          check("취소된 주문 상세 조회", ok(r) and r["data"]["status"]=="CANCELLED" and len(r["data"]["items"])==1, r)

# ---------- 동시성: 재고 3짜리를 8명이 1개씩 ----------
r = padd(name="동시성"+suffix, inventory=3, price=1000); pc = r["product_id"]
results = []
def buyer(i):
    c = C(5000); c.call(action="login", login_id="test", password="1234")
    results.append(c.call(action="order_create", order_items=[{"product_id": pc, "quantity": 1}]))
ts = [threading.Thread(target=buyer, args=(i,)) for i in range(8)]
[t.start() for t in ts]; [t.join() for t in ts]
success = sum(1 for r in results if ok(r))
stock = {p["product_id"]: p for p in inv.call(type="inventory_product_list")["products"]}[pc]["stock"]
check("동시 주문: 정확히 3건만 성공", success == 3 and stock == 0, (success, stock))

# ---------- 상품 수정(이력 보존) & 재고 차감 API ----------
r = inv.call(type="product_update", product_id=p1, category_id=cat, name="티셔츠 V2"+suffix, description="", color="", size="XL", price=17000, inventory=20)
check("상품 수정 → 새 상품 생성", ok(r) and r["old_product_id"]==p1 and r["new_product_id"]!=p1, r); p1new = r["new_product_id"]
prods = {p["product_id"]: p for p in inv.call(type="inventory_product_list")["products"]}
check("기존 상품 비활성 + 새 상품 활성", prods[p1]["is_active"] is False and prods[p1new]["is_active"] is True and prods[p1new]["description"]=="" and prods[p1new]["stock"]==20, (prods[p1], prods[p1new]))
r = inv.call(type="product_update", product_id=p1, category_id=cat, name="x", price=1, inventory=1); check("비활성 상품 재수정 거부", not ok(r), r)
r = u.call(action="product_detail", product_id=p1);       check("고객: 비활성 상품 안 보임", not ok(r), r)
r = inv.call(type="stock_decrease", items=[{"product_id": p1new, "quantity": 5}, {"product_id": p2, "quantity": 99}])
check("stock_decrease: 하나라도 부족하면 전체 롤백", not ok(r), r)
stock = {p["product_id"]: p for p in inv.call(type="inventory_product_list")["products"]}[p1new]["stock"]
check("stock_decrease 롤백 확인(20 그대로)", stock == 20, stock)
r = inv.call(type="stock_decrease", items=[{"product_id": p1new, "quantity": 5}]); check("stock_decrease 성공", ok(r), r)

# 주문 후 상품이 수정된 경우: 취소는 되지만 비활성(이전 버전) 상품의 재고는 되돌리지 않는다
r = u.call(action="order_cancel", order_id=oid)
check("수정된 상품이 포함된 주문 취소", ok(r) and "복구되지 않았습니다" in r["message"] and "티셔츠"+suffix in r["message"], r)
prods = {p["product_id"]: p for p in inv.call(type="inventory_product_list")["products"]}
check("판매 중 상품만 재고 복구(p2 1→3, 새 상품 15 그대로)", prods[p2]["stock"]==3 and prods[p1new]["stock"]==15, (prods[p2]["stock"], prods[p1new]["stock"]))
r = inv.call(type="bogus");                               check("재고: 알 수 없는 type", not ok(r), r)

# ---------- 게시판 ----------
g = C(5000)
r = g.call(action="board_create", title="x", content="y"); check("비로그인 글쓰기 거부", not ok(r), r)
g.call(action="login", login_id=newid, password="pw1234")
r = g.call(action="board_create", title="첫 글"+suffix, content="안녕"); check("글쓰기", ok(r), r); post = r["data"]["post_id"]
r = g.call(action="comment_create", post_id=post, content="댓글1"); check("댓글 등록", ok(r), r); cm = r["data"]["comment_id"]
r = g.call(action="board_list", page=1, size=20, keyword="첫 글"+suffix); check("게시글 검색/목록", ok(r) and r["data"]["total"]==1 and r["data"]["posts"][0]["comment_count"]==1 and r["data"]["posts"][0]["author"]=="신규2", r)
r = g.call(action="board_detail", post_id=post);          check("게시글 상세+댓글", ok(r) and r["data"]["content"]=="안녕" and r["data"]["comments"][0]["content"]=="댓글1", r)
r = other.call(action="board_update", post_id=post, title="해킹");  check("남의 글 수정 거부", not ok(r), r)
r = other.call(action="board_delete", post_id=post);     check("남의 글 삭제 거부", not ok(r), r)
r = other.call(action="comment_update", comment_id=cm, content="해킹"); check("남의 댓글 수정 거부", not ok(r), r)
r = other.call(action="comment_delete", comment_id=cm);  check("남의 댓글 삭제 거부", not ok(r), r)
r = g.call(action="board_update", post_id=post, title="수정됨"+suffix);  check("내 글 수정", ok(r), r)
r = g.call(action="comment_update", comment_id=cm, content="댓글 수정");  check("내 댓글 수정", ok(r), r)
r = g.call(action="board_detail", post_id=post);          check("수정 반영", r["data"]["title"]=="수정됨"+suffix and r["data"]["comments"][0]["content"]=="댓글 수정", r)
r = g.call(action="board_list", page=1, size=1000, keyword=""); check("size 상한 보정", ok(r) and r["data"]["size"]==100, r)
r = g.call(action="board_delete", post_id=post);         check("글 삭제(댓글 포함)", ok(r), r)
r = g.call(action="board_detail", post_id=post);         check("삭제 확인", not ok(r), r)

# ---------- 대시보드 ----------
d = C(6001)
r = d.many(1, type="data", start="2000-01-01 00:00:00", end="2100-01-01 00:00:00"); check("대시보드: 미인증 거부", r[0]["type"]=="error", r)
r = C(6001).call(type="login", login_id="test", password="1234"); check("대시보드: 일반회원 거부", not ok(r), r)
d.call(type="login", login_id="admin", password="admin1234")
today = datetime.date.today().isoformat()
msgs = d.many(3, type="data", start=today+" 00:00:00", end=today+" 23:59:59")
types = [m["type"] for m in msgs]
total = int(msgs[0]["content"][0]["total_sales"])
top = msgs[1]["content"]; cats = msgs[2]["content"]
check("대시보드: 메시지 3종", types == ["total_sales","product_top5","category_sales"], types)
import pymysql
db = pymysql.connect(host="127.0.0.1", user="root", password="1234", database="shopping"); cur = db.cursor()
cur.execute("select sum(price*quantity) from order_item oi join orders o on o.order_id=oi.order_id where date(o.ordered_at)=curdate() and o.status<>'CANCELLED'"); expect = int(cur.fetchone()[0])
check("대시보드: 총매출 = DB 합계(취소 주문 제외)", total == expect and total > 0, (total, expect))
cur.execute("select count(*) from orders where date(ordered_at)=curdate() and status='CANCELLED'")
check("대시보드: 오늘 취소 주문 존재(제외 검증용)", cur.fetchone()[0] >= 2)
check("대시보드: TOP5 정렬/개수", len(top) <= 5 and [int(t["total_sales"]) for t in top] == sorted([int(t["total_sales"]) for t in top], reverse=True), top)
check("대시보드: 카테고리 합 = 총매출", sum(int(c["total_sales"]) for c in cats) == total, (cats, total))
r = d.many(1, type="data", start="", end=""); check("대시보드: 기간 누락 거부", r[0]["type"]=="error", r)

# ---------- 회원 탈퇴 ----------
r = u.call(action="member_withdraw");                     check("회원 탈퇴", ok(r), r)
r = u.call(action="cart_list");                           check("탈퇴 후 세션 종료", not ok(r), r)
r = C(5000).call(action="login", login_id=newid, password="pw1234"); check("탈퇴 계정 로그인 거부", not ok(r) and "탈퇴" in r["message"], r)

print("\n실패:", fails)
sys.exit(1 if fails else 0)
