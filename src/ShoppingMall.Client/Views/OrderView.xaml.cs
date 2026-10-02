namespace ShoppingMall.Client.Views;

/// <summary>
/// 주문/결제 확인 화면. 장바구니에서 고른 상품, 결제 금액, 배송 정보(회원정보)를 보여준다.
/// 실제 주문 생성(order_create) 호출은 여기 "결제하기" 버튼에서 일어난다.
/// 결제가 끝나거나 뒤로가기를 누르면 이벤트로 ShopShell 에 알려 장바구니 탭으로 돌아간다.
/// </summary>
public partial class OrderView : UserControl
{
    private readonly ShopApi _api;
    private List<CartItem> _items = new(); // 주문할 항목

    public event Action? OrderCompleted;
    public event Action? OrderCancelled;

    public OrderView(ShopApi api)
    {
        InitializeComponent();
        _api = api;
    }

    private void Back_Click(object sender, RoutedEventArgs e) => OrderCancelled?.Invoke();

    private async void Pay_Click(object sender, RoutedEventArgs e) => await Ui.Guard(this, PayAsync);

    /// <summary>장바구니에서 넘어온 주문 대상 항목을 표시하고, 최신 회원정보(배송 정보)를 불러온다.</summary>
    public async Task SetItemsAsync(List<CartItem> items)
    {
        _items = items;
        ItemList.ItemsSource = items;
        TotalText.Text = "결제 금액: " + Fmt.Won(items.Sum(i => i.Subtotal));

        var info = await _api.MemberInfoAsync();
        if (info.Ok)
        {
            NameText.Text = "이름: " + Fmt.OrDash(info.Value.Name);
            PhoneText.Text = "전화번호: " + (info.Value.Phone.Length > 0 ? info.Value.Phone : "미등록");
            AddressText.Text = "주소: " + (info.Value.Address.Length > 0 ? info.Value.Address : "미등록");
        }
        else
        {
            NameText.Text = "이름: -";
            PhoneText.Text = "전화번호: -";
            AddressText.Text = "주소: -";
        }
    }

    /// <summary>
    /// 주문 생성. 서버에는 필요한 값(ID·수량)만 보낸다. 이름/가격/재고는 서버가 최신값으로 다시 조회한다.
    /// </summary>
    private async Task PayAsync()
    {
        if (_items.Count == 0)
        {
            Dialogs.Info(this, "주문할 상품이 없습니다.");
            return;
        }

        var result = await _api.OrderCreateAsync(_items);
        if (!result.Ok)
        {
            Dialogs.Error(this, result.Message, "실패");
            return;
        }

        Dialogs.Info(this, $"주문이 완료되었습니다. (주문번호 {result.Value})", "완료");
        _items = new List<CartItem>();
        OrderCompleted?.Invoke();
    }
}
