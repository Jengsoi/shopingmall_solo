using System.Globalization;
using System.Text.Json.Nodes;
using ShoppingMall.Protocol;

namespace ShoppingMall.Client.Core;

/// <summary>화면에 쓰는 표시 형식 모음.</summary>
public static class Fmt
{
    public static string Won(long amount) => amount.ToString("N0", CultureInfo.InvariantCulture) + "원";
    public static string Num(long n) => n.ToString("N0", CultureInfo.InvariantCulture);
    public static string OrDash(string? s) => string.IsNullOrEmpty(s) ? "-" : s;
}

/// <summary>API 호출 결과. 실패하면 Message 에 사용자에게 보여줄 문구가 들어 있다.</summary>
public class ApiResult
{
    public bool Ok { get; init; }
    public string Message { get; init; } = "";

    /// <summary>{"status":"success"} 또는 {"success":true} 형식의 응답을 결과로 바꾼다.</summary>
    public static ApiResult From(JsonObject res, string failFallback = "요청에 실패했습니다.")
    {
        bool ok = res.IsOk() || res.Bool("success") == true;
        return new ApiResult { Ok = ok, Message = res.Str("message") ?? (ok ? "" : failFallback) };
    }
}

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
            Value = baseResult.Ok ? parse(res) : default!,
        };
    }
}

// ---------------------------------------------------------------- 회원

public sealed record Member(long MemberId, string LoginId, string Name, string Role)
{
    public bool IsAdmin => Role == "ADMIN";

    public static Member Parse(JsonObject o) => new(
        o.Int("member_id") ?? 0, o.Str("login_id") ?? "", o.Str("name") ?? "", o.Str("role") ?? "USER");
}

public sealed record MemberInfo(string LoginId, string Name, string Address, string Email, string Phone, string Gender)
{
    public static MemberInfo Parse(JsonObject o) => new(
        o.Str("login_id") ?? "", o.Str("name") ?? "", o.Str("address") ?? "",
        o.Str("email") ?? "", o.Str("phone") ?? "", o.Str("gender") ?? "");
}

// ---------------------------------------------------------------- 상품 / 장바구니 / 주문

public sealed record CategoryInfo(long CategoryId, string Name)
{
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

public sealed record ProductDetail(long ProductId, string Name, string Description, string Color, string Size, long Price, long Stock)
{
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

public sealed record CartItem(long CartId, long ProductId, string Name, long Price, long Quantity)
{
    public long Subtotal => Price * Quantity;
    public override string ToString() => $"{Name} | {Fmt.Won(Price)} x {Quantity}개";

    public static CartItem Parse(JsonObject o) => new(
        o.Int("cart_id") ?? 0, o.Int("product_id") ?? 0, o.Str("name") ?? "",
        o.Int("price") ?? 0, o.Int("quantity") ?? 0);
}

public static class OrderStatus
{
    public const string Paid = "PAID";
    public const string Cancelled = "CANCELLED";

    public static string Label(string status) => status switch
    {
        Paid => "결제완료",
        Cancelled => "주문취소",
        _ => status,
    };
}

public sealed record OrderSummary(long OrderId, string Status, string OrderedAt, long TotalPrice)
{
    public string StatusText => OrderStatus.Label(Status);
    public string TotalText => Fmt.Won(TotalPrice);
    public bool CanCancel => Status == OrderStatus.Paid;

    public static OrderSummary Parse(JsonObject o) => new(
        o.Int("order_id") ?? 0, o.Str("status") ?? "", o.Str("ordered_at") ?? "", o.Int("total_price") ?? 0);
}

public sealed record OrderLine(long ProductId, string ProductName, long Price, long Quantity)
{
    public long Subtotal => Price * Quantity;
    public override string ToString() => $"{ProductName} | {Fmt.Won(Price)} x {Quantity}개 = {Fmt.Won(Subtotal)}";

    public static OrderLine Parse(JsonObject o) => new(
        o.Int("product_id") ?? 0, o.Str("product_name") ?? "", o.Int("price") ?? 0, o.Int("quantity") ?? 0);
}

// ---------------------------------------------------------------- 게시판

public sealed record PostSummary(long PostId, string Title, string Author, string CreatedAt, long CommentCount)
{
    public static PostSummary Parse(JsonObject o) => new(
        o.Int("post_id") ?? 0, o.Str("title") ?? "", o.Str("author") ?? "", o.Str("created_at") ?? "",
        o.Int("comment_count") ?? 0);
}

public sealed record BoardPage(List<PostSummary> Posts, long Total, long Page, long Size)
{
    public long LastPage => Math.Max(1, (Total + Size - 1) / Math.Max(1, Size));

    public static BoardPage Parse(JsonObject res)
    {
        var data = res.Obj("data") ?? new JsonObject();
        var posts = data.Arr("posts")?.Objects().Select(PostSummary.Parse).ToList() ?? new List<PostSummary>();
        return new BoardPage(posts, data.Int("total") ?? 0, data.Int("page") ?? 1, data.Int("size") ?? 20);
    }
}

public sealed record CommentInfo(long CommentId, string Author, string Content, string CreatedAt)
{
    public override string ToString() => $"{Fmt.OrDash(Author)} : {Content}  ({CreatedAt})";

    public static CommentInfo Parse(JsonObject o) => new(
        o.Int("comment_id") ?? 0, o.Str("author") ?? "", o.Str("content") ?? "", o.Str("created_at") ?? "");
}

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

public sealed record AdminCategory(long CategoryId, string Name, bool IsActive)
{
    public string ActiveText => IsActive ? "활성" : "비활성";

    public override string ToString() => Name;

    public static AdminCategory Parse(JsonObject o) => new(
        o.Int("category_id") ?? 0, o.Str("name") ?? "", o.Bool("is_active") ?? false);
}

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

/// <summary>상품 추가/수정 입력값. 서버에는 재고를 "inventory" 라는 이름으로 보낸다.</summary>
public sealed record ProductInput(long CategoryId, string Name, string Description, string Color, string Size, long Price, long Stock);

// ---------------------------------------------------------------- 관리자: 대시보드

public sealed record SalesEntry(string Name, long Sales);

public sealed record DashboardData(long TotalSales, List<SalesEntry> Top5, List<SalesEntry> Categories);
