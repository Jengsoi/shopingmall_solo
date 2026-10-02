namespace ShoppingMall.Client.Views;

/// <summary>
/// 일반 회원 화면. 탭 5개(상품페이지 / 장바구니 / 주문내역 / 내 정보 / 게시판)와 주문·결제 화면.
/// 화면끼리 직접 부르지 않고 이벤트(OrderRequested, OrderCompleted 등)로 이 클래스에 알리면,
/// 여기서 화면 전환과 새로고침을 맡는다.
/// </summary>
public partial class ShopShell : UserControl
{
    private readonly ProductView _products;
    private readonly CartView _cart;
    private readonly OrderView _order;
    private readonly OrderHistoryView _history;
    private readonly MemberView _member;
    private readonly BoardView _board;
    private bool _productsStale; // 주문 취소로 재고가 바뀌어 상품 목록을 다시 불러와야 함

    public event Action? LogoutRequested;

    public ShopShell(ShopApi api)
    {
        InitializeComponent();

        _products = new ProductView(api);
        _cart = new CartView(api);
        _order = new OrderView(api);
        _history = new OrderHistoryView(api);
        _member = new MemberView(api);
        _board = new BoardView(api);

        ProductsTab.Content = _products;
        CartTab.Content = _cart;
        OrdersTab.Content = _history;
        MemberTab.Content = _member;
        BoardTab.Content = _board;
        OrderHost.Content = _order;

        _member.LogoutRequested += () => LogoutRequested?.Invoke();
        // 주문을 취소하면 재고가 바뀌므로, 다음에 상품페이지 탭을 열 때 목록을 다시 불러온다.
        _history.OrderCancelled += () => _productsStale = true;

        // 장바구니 "구매" → 주문·결제 화면으로, 결제 완료나 뒤로가기 → 다시 장바구니 탭으로
        _cart.OrderRequested += items => Ui.Fire(this, () => ShowOrderAsync(items));
        _order.OrderCompleted += () => Ui.Fire(this, BackToCartAsync);
        _order.OrderCancelled += () => Ui.Fire(this, BackToCartAsync);
    }

    /// <summary>화면이 열린 뒤 각 탭의 데이터를 서버에서 불러온다.</summary>
    public async Task InitializeAsync()
    {
        await _products.RefreshAsync();
        await _cart.LoadAsync();
        await _member.LoadInfoAsync();
        await _board.LoadPostsAsync();
    }

    /// <summary>
    /// 탭이 바뀔 때마다 장바구니·주문내역은 최신 데이터로 갱신한다.
    /// SelectionChanged 는 탭 안의 표·콤보박스에서 일어난 것도 위로 올라오므로(라우트 이벤트),
    /// 이벤트를 일으킨 것이 탭 묶음 자신일 때만 처리한다.
    /// </summary>
    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, Tabs)) return;

        if (Tabs.SelectedItem == ProductsTab && _productsStale)
        {
            _productsStale = false;
            Ui.Fire(this, _products.LoadProductsAsync);
        }
        else if (Tabs.SelectedItem == CartTab)
            Ui.Fire(this, _cart.LoadAsync);
        else if (Tabs.SelectedItem == OrdersTab)
            Ui.Fire(this, _history.LoadAsync);
    }

    /// <summary>선택한 장바구니 항목으로 주문·결제 화면을 채우고, 탭 대신 그 화면을 보여준다.</summary>
    private async Task ShowOrderAsync(List<CartItem> items)
    {
        await _order.SetItemsAsync(items);
        Tabs.Visibility = Visibility.Collapsed;
        OrderHost.Visibility = Visibility.Visible;
    }

    /// <summary>주문·결제 화면을 닫고 장바구니 탭으로 돌아온다.</summary>
    private async Task BackToCartAsync()
    {
        await _cart.LoadAsync(); // 결제로 빠진 상품 반영
        OrderHost.Visibility = Visibility.Collapsed;
        Tabs.Visibility = Visibility.Visible;
        Tabs.SelectedItem = CartTab;
    }
}
