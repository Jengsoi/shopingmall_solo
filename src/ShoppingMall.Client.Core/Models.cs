using System.Globalization;
using System.Text.Json.Nodes;
using ShoppingMall.Protocol;

namespace ShoppingMall.Client.Core;

// ============================================================================
// 서버 응답(JSON)을 화면에서 쓰기 편한 C# 객체로 바꾼 모델들.
//
// 대부분 record 로 만들었다. record 는 값을 담는 용도의 간단한 클래스로, 생성자·비교·출력이 자동으로 만들어진다.
// 각 모델의 Parse() 는 JSON 한 행을 모델로 바꾸고, 값이 빠져 있으면 0 / "" 같은 기본값을 쓴다.
// "...Text" 로 끝나는 속성은 화면 표시용 문구다. (예: Price=15000 → PriceText="15,000원")
// DataGrid 는 열마다 속성 이름(nameof(...))으로 값을 가져와 표시한다.
// ============================================================================

/// <summary>화면에 쓰는 표시 형식 모음.</summary>
public static class Fmt
{
    /// <summary>15000 → "15,000원"</summary>
    public static string Won(long amount) => amount.ToString("N0", CultureInfo.InvariantCulture) + "원";
    /// <summary>15000 → "15,000" (천 단위 쉼표)</summary>
    public static string Num(long n) => n.ToString("N0", CultureInfo.InvariantCulture);
    /// <summary>비어 있으면 "-" 로 표시</summary>
    public static string OrDash(string? s) => string.IsNullOrEmpty(s) ? "-" : s;
}

/// <summary>API 호출 결과. 실패하면 Message 에 사용자에게 보여줄 문구가 들어 있다.</summary>
public class ApiResult
{
    public bool Ok { get; init; }
    public string Message { get; init; } = "";

    /// <summary>
    /// {"status":"success"} 또는 {"success":true} 형식의 응답을 결과로 바꾼다.
    /// (쇼핑몰 서버는 status, 관리자 서버는 success 로 성공을 알려 주므로 둘 다 확인한다)
    /// 서버가 메시지를 안 보냈는데 실패라면 failFallback 을 메시지로 쓴다.
    /// </summary>
    public static ApiResult From(JsonObject res, string failFallback = "요청에 실패했습니다.")
    {
        bool ok = res.IsOk() || res.Bool("success") == true;
        return new ApiResult { Ok = ok, Message = res.Str("message") ?? (ok ? "" : failFallback) };
    }
}

/// <summary>값이 함께 오는 API 결과. 성공일 때만 Value 에 의미 있는 값이 들어 있다.</summary>
public sealed class ApiResult<T> : ApiResult
{
    public T Value { get; init; } = default!;

    public static ApiResult<T> From(JsonObject res, Func<JsonObject, T> parse, string failFallback = "요청에 실패했습니다.")
    {
        var baseResult = ApiResult.From(res, failFallback);
        return new ApiResult<T>
        {
            Ok = baseResult.Ok,
            Message = baseResult.Message,
            Value = baseResult.Ok ? parse(res) : default!, // 실패 응답은 파싱하지 않는다
        };
    }
}

// ---------------------------------------------------------------- 회원

/// <summary>로그인 결과. Role 이 ADMIN 이면 관리자 화면으로 간다.</summary>
public sealed record Member(long MemberId, string LoginId, string Name, string Role)
{
    public bool IsAdmin => Role == "ADMIN";

    public static Member Parse(JsonObject o) => new(
        o.Int("member_id") ?? 0, o.Str("login_id") ?? "", o.Str("name") ?? "", o.Str("role") ?? "USER");
}

/// <summary>내 정보 화면 / 주문 배송 정보에 쓰는 회원 상세</summary>
public sealed record MemberInfo(string LoginId, string Name, string Address, string Email, string Phone, string Gender)
{
    public static MemberInfo Parse(JsonObject o) => new(
        o.Str("login_id") ?? "", o.Str("name") ?? "", o.Str("address") ?? "",
        o.Str("email") ?? "", o.Str("phone") ?? "", o.Str("gender") ?? "");
}

// ---------------------------------------------------------------- 상품 / 장바구니 / 주문

/// <summary>상품페이지 카테고리 선택 상자의 항목</summary>
public sealed record CategoryInfo(long CategoryId, string Name)
{
    // ComboBox 는 항목의 ToString() 결과를 글자로 보여준다.
    public override string ToString() => Name;
}

/// <summary>상품 목록 한 줄. DataGrid 에서 속성 이름으로 바인딩한다.</summary>
public sealed record ProductRow(long ProductId, string Name, string Color, string Size, long Price, long Stock)
{
    public string PriceText => Fmt.Won(Price);
    public string StockText => Stock == 0 ? "품절" : Fmt.Num(Stock);
    public string ColorText => Fmt.OrDash(Color);
    public string SizeText => Fmt.OrDash(Size);

    public static ProductRow Parse(JsonObject o) => new(
        o.Int("product_id") ?? 0, o.Str("name") ?? "", o.Str("color") ?? "", o.Str("size") ?? "",
        o.Int("price") ?? 0, o.Int("stock") ?? 0);
}

/// <summary>상품 상세 (상품페이지 아래쪽 "상품 정보")</summary>
public sealed record ProductDetail(long ProductId, string Name, string Description, string Color, string Size, long Price, long Stock)
{
    /// <summary>예: "티셔츠 (검정 / L) | 15,000원 | 재고 7개" + 줄바꿈 + 설명</summary>
    public string Summary
    {
        get
        {
            string option = string.Join(" / ", new[] { Color, Size }.Where(s => !string.IsNullOrEmpty(s)));
            string title = option.Length > 0 ? $"{Name} ({option})" : Name;
            string stock = Stock == 0 ? "품절" : $"{Fmt.Num(Stock)}개";
            return $"{title} | {Fmt.Won(Price)} | 재고 {stock}\n{Description}";
        }
    }

    public static ProductDetail Parse(JsonObject o) => new(
        o.Int("product_id") ?? 0, o.Str("name") ?? "", o.Str("description") ?? "",
        o.Str("color") ?? "", o.Str("size") ?? "", o.Int("price") ?? 0, o.Int("stock") ?? 0);
}

/// <summary>장바구니 한 항목. 주문 화면에도 그대로 넘겨 쓴다.</summary>
public sealed record CartItem(long CartId, long ProductId, string Name, long Price, long Quantity)
{
    public long Subtotal => Price * Quantity;
    // ListBox 는 ToString() 결과를 한 줄로 보여준다.
    public override string ToString() => $"{Name} | {Fmt.Won(Price)} x {Quantity}개";

    public static CartItem Parse(JsonObject o) => new(
        o.Int("cart_id") ?? 0, o.Int("product_id") ?? 0, o.Str("name") ?? "",
        o.Int("price") ?? 0, o.Int("quantity") ?? 0);
}

/// <summary>주문 상태: 결제완료 → 배송준비중 → 배송중 → 배송완료, 그리고 주문취소. (서버 OrderStatus 와 같은 값)</summary>
public static class OrderStatus
{
    public const string Paid = "PAID";
    public const string Preparing = "PREPARING";
    public const string Shipping = "SHIPPING";
    public const string Delivered = "DELIVERED";
    public const string Cancelled = "CANCELLED";

    public static readonly string[] All = { Paid, Preparing, Shipping, Delivered, Cancelled };

    /// <summary>서버 상태 값 → 화면 표시용 한글</summary>
    public static string Label(string status) => status switch
    {
        Paid => "결제완료",
        Preparing => "배송준비중",
        Shipping => "배송중",
        Delivered => "배송완료",
        Cancelled => "주문취소",
        _ => status,
    };

    /// <summary>관리자가 진행할 수 있는 다음 단계. 없으면 null.</summary>
    public static string? Next(string status) => status switch
    {
        Paid => Preparing,
        Preparing => Shipping,
        Shipping => Delivered,
        _ => null,
    };

    /// <summary>고객은 결제완료 상태에서만, 관리자는 배송완료 전까지 취소할 수 있다.</summary>
    public static bool CanCancel(string status, bool byAdmin) =>
        status == Paid || (byAdmin && status is Preparing or Shipping);
}

/// <summary>고객 주문내역 목록의 한 행</summary>
public sealed record OrderSummary(long OrderId, string Status, string OrderedAt, long TotalPrice)
{
    public string StatusText => OrderStatus.Label(Status);
    public string TotalText => Fmt.Won(TotalPrice);
    public bool CanCancel => OrderStatus.CanCancel(Status, byAdmin: false);

    public static OrderSummary Parse(JsonObject o) => new(
        o.Int("order_id") ?? 0, o.Str("status") ?? "", o.Str("ordered_at") ?? "", o.Int("total_price") ?? 0);
}

/// <summary>주문에 담긴 상품 한 줄 (주문 당시의 상품명·가격)</summary>
public sealed record OrderLine(long ProductId, string ProductName, long Price, long Quantity)
{
    public long Subtotal => Price * Quantity;
    public override string ToString() => $"{ProductName} | {Fmt.Won(Price)} x {Quantity}개 = {Fmt.Won(Subtotal)}";

    public static OrderLine Parse(JsonObject o) => new(
        o.Int("product_id") ?? 0, o.Str("product_name") ?? "", o.Int("price") ?? 0, o.Int("quantity") ?? 0);
}

// ---------------------------------------------------------------- 게시판

/// <summary>게시판 목록의 한 행</summary>
public sealed record PostSummary(long PostId, string Title, string Author, string CreatedAt, long CommentCount)
{
    public static PostSummary Parse(JsonObject o) => new(
        o.Int("post_id") ?? 0, o.Str("title") ?? "", o.Str("author") ?? "", o.Str("created_at") ?? "",
        o.Int("comment_count") ?? 0);
}

/// <summary>게시판 한 페이지 (글 목록 + 전체 글 수 + 현재 페이지 정보)</summary>
public sealed record BoardPage(List<PostSummary> Posts, long Total, long Page, long Size)
{
    /// <summary>마지막 페이지 번호. 올림 나눗셈: 글 41개, 페이지당 20개 → 3페이지. 글이 없어도 최소 1.</summary>
    public long LastPage => Math.Max(1, (Total + Size - 1) / Math.Max(1, Size));

    public static BoardPage Parse(JsonObject res)
    {
        var data = res.Obj("data") ?? new JsonObject();
        var posts = data.Arr("posts")?.Objects().Select(PostSummary.Parse).ToList() ?? new List<PostSummary>();
        return new BoardPage(posts, data.Int("total") ?? 0, data.Int("page") ?? 1, data.Int("size") ?? 20);
    }
}

/// <summary>댓글 한 개</summary>
public sealed record CommentInfo(long CommentId, string Author, string Content, string CreatedAt)
{
    public override string ToString() => $"{Fmt.OrDash(Author)} : {Content}  ({CreatedAt})";

    public static CommentInfo Parse(JsonObject o) => new(
        o.Int("comment_id") ?? 0, o.Str("author") ?? "", o.Str("content") ?? "", o.Str("created_at") ?? "");
}

/// <summary>게시글 상세 (본문 + 댓글 목록)</summary>
public sealed record PostDetail(long PostId, string Title, string Content, string Author, string CreatedAt, List<CommentInfo> Comments)
{
    public string Meta => $"작성자: {Fmt.OrDash(Author)}  |  작성일: {CreatedAt}";

    public static PostDetail Parse(JsonObject res)
    {
        var o = res.Obj("data") ?? new JsonObject();
        var comments = o.Arr("comments")?.Objects().Select(CommentInfo.Parse).ToList() ?? new List<CommentInfo>();
        return new PostDetail(o.Int("post_id") ?? 0, o.Str("title") ?? "", o.Str("content") ?? "",
            o.Str("author") ?? "", o.Str("created_at") ?? "", comments);
    }
}

// ---------------------------------------------------------------- 관리자: 재고관리

/// <summary>관리자 카테고리 목록의 한 행 (비활성 포함)</summary>
public sealed record AdminCategory(long CategoryId, string Name, bool IsActive)
{
    public string ActiveText => IsActive ? "활성" : "비활성";

    public override string ToString() => Name;

    public static AdminCategory Parse(JsonObject o) => new(
        o.Int("category_id") ?? 0, o.Str("name") ?? "", o.Bool("is_active") ?? false);
}

/// <summary>관리자 상품 목록의 한 행. 수정으로 판매가 끝난 이전 버전도 "비활성(이전 버전)" 으로 함께 보인다.</summary>
public sealed record AdminProduct(
    long ProductId, long CategoryId, string CategoryName, string Name, string Description,
    string Color, string Size, long Price, long Stock, bool IsActive, string StockStatus)
{
    public string PriceText => Fmt.Num(Price);
    public string ActiveText => IsActive ? "판매중" : "비활성(이전 버전)";

    public static AdminProduct Parse(JsonObject o) => new(
        o.Int("product_id") ?? 0, o.Int("category_id") ?? 0, o.Str("category_name") ?? "",
        o.Str("name") ?? "", o.Str("description") ?? "", o.Str("color") ?? "", o.Str("size") ?? "",
        o.Int("price") ?? 0, o.Int("stock") ?? 0, o.Bool("is_active") ?? false, o.Str("stock_status") ?? "");
}

/// <summary>관리자 주문 관리 목록의 한 행.</summary>
public sealed record AdminOrder(long OrderId, string Status, string OrderedAt, string LoginId, string MemberName, long TotalPrice)
{
    public string StatusText => OrderStatus.Label(Status);
    public string TotalText => Fmt.Won(TotalPrice);
    public string MemberText => $"{MemberName} ({LoginId})";
    public string? NextStatus => OrderStatus.Next(Status); // "다음 단계로" 버튼에 쓸 상태 (없으면 버튼 비활성)
    public bool CanCancel => OrderStatus.CanCancel(Status, byAdmin: true);

    public static AdminOrder Parse(JsonObject o) => new(
        o.Int("order_id") ?? 0, o.Str("status") ?? "", o.Str("ordered_at") ?? "",
        o.Str("login_id") ?? "", o.Str("member_name") ?? "", o.Int("total_price") ?? 0);
}

/// <summary>재고 변경 이력 한 줄. 예: 2026-09-30 16:03 | 티셔츠 | 주문취소 | +3 | 15 → 18 | 주문 127 | admin</summary>
public sealed record StockHistoryEntry(
    long HistoryId, string CreatedAt, long ProductId, string ProductName, long ChangeQty,
    long StockBefore, long StockAfter, string Reason, long? OrderId, string MemberLoginId)
{
    public string ReasonText => Reason switch
    {
        "ORDER" => "주문",
        "ORDER_CANCEL" => "주문취소",
        "PRODUCT_ADD" => "상품등록",
        "PRODUCT_UPDATE" => "상품수정",
        "STOCK_DECREASE" => "재고차감",
        _ => Reason,
    };

    // 늘어난 수량은 + 를 붙여서 표시
    public string ChangeText => ChangeQty > 0 ? $"+{Fmt.Num(ChangeQty)}" : Fmt.Num(ChangeQty);
    public string StockText => $"{Fmt.Num(StockBefore)} → {Fmt.Num(StockAfter)}";
    public string OrderText => OrderId is long id ? id.ToString() : "-";
    public string MemberText => Fmt.OrDash(MemberLoginId);

    public static StockHistoryEntry Parse(JsonObject o) => new(
        o.Int("history_id") ?? 0, o.Str("created_at") ?? "", o.Int("product_id") ?? 0, o.Str("product_name") ?? "",
        o.Int("change_qty") ?? 0, o.Int("stock_before") ?? 0, o.Int("stock_after") ?? 0,
        o.Str("reason") ?? "", o.Int("order_id"), o.Str("member_login_id") ?? "");
}

/// <summary>상품 추가/수정 입력값. 서버에는 재고를 "inventory" 라는 이름으로 보낸다.</summary>
public sealed record ProductInput(long CategoryId, string Name, string Description, string Color, string Size, long Price, long Stock);

// ---------------------------------------------------------------- 관리자: 대시보드

/// <summary>차트 항목 하나 (상품명 또는 카테고리명 + 매출액)</summary>
public sealed record SalesEntry(string Name, long Sales);

/// <summary>대시보드 조회 결과 (총 매출, TOP5, 카테고리별 매출)</summary>
public sealed record DashboardData(long TotalSales, List<SalesEntry> Top5, List<SalesEntry> Categories);
