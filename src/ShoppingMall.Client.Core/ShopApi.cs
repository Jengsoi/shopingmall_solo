using System.Text.Json.Nodes;
using ShoppingMall.Protocol;

namespace ShoppingMall.Client.Core;

/// <summary>쇼핑몰 서버(포트 5000) API. 기존 Python NetworkClient 의 메서드와 1:1 로 대응한다.</summary>
public sealed class ShopApi
{
    private readonly NetworkClient _net;

    public ShopApi(NetworkClient net)
    {
        _net = net;
    }

    private Task<JsonObject> CallAsync(string action, params (string Key, object? Value)[] fields)
    {
        var request = new JsonObject { ["action"] = action };
        foreach (var (key, value) in fields)
        {
            if (value is null) continue; // null 인 항목은 보내지 않는다(= 수정하지 않음).
            JsonNode? node = value switch
            {
                string s => s,
                int i => i,
                long l => l,
                bool b => b,
                JsonNode n => n,
                _ => throw new ArgumentException($"지원하지 않는 값 형식: {value.GetType()}"),
            };
            request[key] = node;
        }
        return _net.RequestAsync(request);
    }

    private static List<T> ParseList<T>(JsonObject res, Func<JsonObject, T> parse) =>
        res.Arr("data")?.Objects().Select(parse).ToList() ?? new List<T>();

    // ------------------------------------------------------------ 회원

    public async Task<ApiResult> SignupAsync(string loginId, string password, string name,
        string address = "", string email = "", string phone = "", string gender = "") =>
        ApiResult.From(await CallAsync("signup",
            ("login_id", loginId), ("password", password), ("name", name),
            ("address", address), ("email", email), ("phone", phone), ("gender", gender)),
            "회원가입에 실패했습니다.");

    /// <summary>사용 가능한 아이디이면 Value = true.</summary>
    public async Task<ApiResult<bool>> CheckIdAsync(string loginId) =>
        ApiResult<bool>.From(await CallAsync("check_id", ("login_id", loginId)),
            r => r.Obj("data")?.Bool("available") ?? false, "중복확인에 실패했습니다.");

    public async Task<ApiResult<Member>> LoginAsync(string loginId, string password) =>
        ApiResult<Member>.From(await CallAsync("login", ("login_id", loginId), ("password", password)),
            r => Member.Parse(r.Obj("data") ?? new JsonObject()), "로그인에 실패했습니다.");

    public async Task<ApiResult> LogoutAsync() => ApiResult.From(await CallAsync("logout"));

    public async Task<ApiResult<MemberInfo>> MemberInfoAsync() =>
        ApiResult<MemberInfo>.From(await CallAsync("member_info"),
            r => MemberInfo.Parse(r.Obj("data") ?? new JsonObject()), "회원정보 조회 실패");

    public async Task<ApiResult> MemberUpdateAsync(string name, string address, string email, string phone, string gender) =>
        ApiResult.From(await CallAsync("member_update",
            ("name", name), ("address", address), ("email", email), ("phone", phone), ("gender", gender)),
            "수정에 실패했습니다.");

    public async Task<ApiResult> MemberWithdrawAsync() =>
        ApiResult.From(await CallAsync("member_withdraw"), "탈퇴에 실패했습니다.");

    // ------------------------------------------------------------ 상품

    public async Task<ApiResult<List<CategoryInfo>>> CategoryListAsync() =>
        ApiResult<List<CategoryInfo>>.From(await CallAsync("category_list"),
            r => ParseList(r, o => new CategoryInfo(o.Int("category_id") ?? 0, o.Str("name") ?? "")),
            "카테고리 조회 실패");

    public async Task<ApiResult<List<ProductRow>>> ProductListAsync(long? categoryId, string keyword) =>
        ApiResult<List<ProductRow>>.From(
            await CallAsync("product_list", ("category_id", categoryId), ("keyword", keyword)),
            r => ParseList(r, ProductRow.Parse), "상품 조회 실패");

    public async Task<ApiResult<ProductDetail>> ProductDetailAsync(long productId) =>
        ApiResult<ProductDetail>.From(await CallAsync("product_detail", ("product_id", productId)),
            r => ProductDetail.Parse(r.Obj("data") ?? new JsonObject()), "상세 조회 실패");

    // ------------------------------------------------------------ 장바구니 / 주문

    public async Task<ApiResult<List<CartItem>>> CartListAsync() =>
        ApiResult<List<CartItem>>.From(await CallAsync("cart_list"),
            r => ParseList(r, CartItem.Parse), "장바구니 조회 실패");

    public async Task<ApiResult> CartAddAsync(long productId, long quantity) =>
        ApiResult.From(await CallAsync("cart_add", ("product_id", productId), ("quantity", quantity)),
            "담기에 실패했습니다.");

    public async Task<ApiResult> CartUpdateAsync(long cartId, long quantity) =>
        ApiResult.From(await CallAsync("cart_update", ("cart_id", cartId), ("quantity", quantity)));

    public async Task<ApiResult> CartDeleteAsync(long cartId) =>
        ApiResult.From(await CallAsync("cart_delete", ("cart_id", cartId)));

    /// <summary>장바구니 항목들을 주문한다. 성공하면 Value = 주문번호. 서버에는 필요한 값만 보낸다.</summary>
    public async Task<ApiResult<long>> OrderCreateAsync(IEnumerable<CartItem> items)
    {
        var array = new JsonArray();
        foreach (var item in items)
        {
            array.Add(new JsonObject
            {
                ["cart_id"] = item.CartId,
                ["product_id"] = item.ProductId,
                ["quantity"] = item.Quantity,
            });
        }

        return ApiResult<long>.From(await CallAsync("order_create", ("order_items", array)),
            r => r.Obj("data")?.Int("order_id") ?? 0, "주문에 실패했습니다.");
    }

    public async Task<ApiResult<List<OrderSummary>>> OrderListAsync() =>
        ApiResult<List<OrderSummary>>.From(await CallAsync("order_list"),
            r => ParseList(r, OrderSummary.Parse), "주문 조회 실패");

    /// <summary>주문에 담긴 상품 목록.</summary>
    public async Task<ApiResult<List<OrderLine>>> OrderDetailAsync(long orderId) =>
        ApiResult<List<OrderLine>>.From(await CallAsync("order_detail", ("order_id", orderId)),
            r => r.Obj("data")?.Arr("items")?.Objects().Select(OrderLine.Parse).ToList() ?? new List<OrderLine>(),
            "주문 조회 실패");

    /// <summary>주문을 취소하고 재고를 되돌린다. 성공 시 Message 에 안내 문구가 들어 있다.</summary>
    public async Task<ApiResult> OrderCancelAsync(long orderId) =>
        ApiResult.From(await CallAsync("order_cancel", ("order_id", orderId)), "주문 취소에 실패했습니다.");

    // ------------------------------------------------------------ 게시판

    public async Task<ApiResult<BoardPage>> BoardListAsync(long page, long size, string keyword) =>
        ApiResult<BoardPage>.From(
            await CallAsync("board_list", ("page", page), ("size", size), ("keyword", keyword)),
            BoardPage.Parse, "게시글 조회 실패");

    public async Task<ApiResult<PostDetail>> BoardDetailAsync(long postId) =>
        ApiResult<PostDetail>.From(await CallAsync("board_detail", ("post_id", postId)),
            PostDetail.Parse, "게시글 조회 실패");

    public async Task<ApiResult> BoardCreateAsync(string title, string content) =>
        ApiResult.From(await CallAsync("board_create", ("title", title), ("content", content)), "저장에 실패했습니다.");

    public async Task<ApiResult> BoardUpdateAsync(long postId, string title, string content) =>
        ApiResult.From(await CallAsync("board_update", ("post_id", postId), ("title", title), ("content", content)),
            "저장에 실패했습니다.");

    public async Task<ApiResult> BoardDeleteAsync(long postId) =>
        ApiResult.From(await CallAsync("board_delete", ("post_id", postId)), "삭제에 실패했습니다.");

    public async Task<ApiResult> CommentCreateAsync(long postId, string content) =>
        ApiResult.From(await CallAsync("comment_create", ("post_id", postId), ("content", content)),
            "댓글 등록에 실패했습니다.");

    public async Task<ApiResult> CommentUpdateAsync(long commentId, string content) =>
        ApiResult.From(await CallAsync("comment_update", ("comment_id", commentId), ("content", content)),
            "댓글 수정에 실패했습니다.");

    public async Task<ApiResult> CommentDeleteAsync(long commentId) =>
        ApiResult.From(await CallAsync("comment_delete", ("comment_id", commentId)), "댓글 삭제에 실패했습니다.");
}
