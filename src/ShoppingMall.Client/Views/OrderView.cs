namespace ShoppingMall.Client.Views;

/// <summary>
/// 주문/결제 확인 화면. 장바구니에서 고른 상품, 결제 금액, 배송 정보(회원정보)를 보여준다.
/// 실제 주문 생성(order_create) 호출은 여기 "결제하기" 버튼에서 일어난다.
/// 결제가 끝나거나 뒤로가기를 누르면 이벤트로 ShopShell 에 알려 장바구니 탭으로 돌아간다.
/// </summary>
public sealed class OrderView : UserControl
{
    private readonly ShopApi _api;
    private readonly ListBox _list = new();
    private readonly TextBlock _total = Ui.Text("결제 금액: 0원", 16, true);
    private readonly TextBlock _name = Ui.Text("");
    private readonly TextBlock _phone = Ui.Text("");
    private readonly TextBlock _address = Ui.Text("");
    private List<CartItem> _items = new(); // 주문할 항목

    public event Action? OrderCompleted;
    public event Action? OrderCancelled;

    public OrderView(ShopApi api)
    {
        _api = api;

        var buttons = Ui.HStack(8,
            Ui.Btn("뒤로가기", () => { OrderCancelled?.Invoke(); return Task.CompletedTask; }),
            Ui.Btn("결제하기", PayAsync));

        var bottom = Ui.VStack(8, _name, _phone, _address, _total, buttons);
        bottom.Margin = new Thickness(0, 8, 0, 0);

        Content = new Border
        {
            Padding = new Thickness(12),
            Child = Ui.Dock(Ui.Title("[주문/결제]"), _list, bottom),
        };
    }

    /// <summary>장바구니에서 넘어온 주문 대상 항목을 표시하고, 최신 회원정보(배송 정보)를 불러온다.</summary>
    public async Task SetItemsAsync(List<CartItem> items)
    {
        _items = items;
        _list.ItemsSource = items;
        _total.Text = "결제 금액: " + Fmt.Won(items.Sum(i => i.Subtotal));

        var info = await _api.MemberInfoAsync();
        if (info.Ok)
        {
            _name.Text = "이름: " + Fmt.OrDash(info.Value.Name);
            _phone.Text = "전화번호: " + (info.Value.Phone.Length > 0 ? info.Value.Phone : "미등록");
            _address.Text = "주소: " + (info.Value.Address.Length > 0 ? info.Value.Address : "미등록");
        }
        else
        {
            _name.Text = "이름: -";
            _phone.Text = "전화번호: -";
            _address.Text = "주소: -";
        }
    }

    private async Task PayAsync()
    {
        if (_items.Count == 0)
        {
            await Dialogs.InfoAsync(this, "주문할 상품이 없습니다.");
            return;
        }

        // 서버에는 필요한 값만 보낸다. 이름/가격/재고는 서버가 최신값으로 다시 조회한다.
        var result = await _api.OrderCreateAsync(_items);
        if (!result.Ok)
        {
            await Dialogs.ErrorAsync(this, result.Message, "실패");
            return;
        }

        await Dialogs.InfoAsync(this, $"주문이 완료되었습니다. (주문번호 {result.Value})", "완료");
        _items = new List<CartItem>();
        OrderCompleted?.Invoke();
    }
}
