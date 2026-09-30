namespace ShoppingMall.Client.Views;

/// <summary>
/// 일반 회원 화면 (main.py 의 MainWindow).
/// 탭: 상품페이지 / 장바구니 / 내 정보 / 게시판, 그리고 장바구니에서 넘어가는 주문·결제 화면.
/// </summary>
public sealed class ShopShell : UserControl
{
    private readonly ProductView _products;
    private readonly CartView _cart;
    private readonly OrderView _order;
    private readonly MemberView _member;
    private readonly BoardView _board;
    private readonly TabControl _tabs = new();
    private readonly ContentControl _root = new();

    public event Action? LogoutRequested;

    public ShopShell(ShopApi api, Member member)
    {
        _products = new ProductView(api);
        _cart = new CartView(api);
        _order = new OrderView(api);
        _member = new MemberView(api);
        _board = new BoardView(api);

        _member.LogoutRequested += () => LogoutRequested?.Invoke();

        _tabs.Items.Add(new TabItem { Header = "상품페이지", Content = _products });
        _tabs.Items.Add(new TabItem { Header = "장바구니", Content = _cart });
        _tabs.Items.Add(new TabItem { Header = "내 정보", Content = _member });
        _tabs.Items.Add(new TabItem { Header = "게시판", Content = _board });

        // 장바구니 탭으로 바뀔 때마다 최신 데이터로 갱신한다.
        // (탭 안의 ComboBox 등에서 올라오는 SelectionChanged 는 무시)
        _tabs.SelectionChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Source, _tabs)) return;
            if (_tabs.SelectedIndex == 1)
                Ui.Fire(this, _cart.LoadAsync);
        };

        _cart.OrderRequested += items => Ui.Fire(this, () => ShowOrderAsync(items));
        _order.OrderCompleted += () => Ui.Fire(this, BackToCartAsync);
        _order.OrderCancelled += () => Ui.Fire(this, BackToCartAsync);

        _root.Content = _tabs;
        Content = _root;
    }

    /// <summary>화면이 열린 뒤 각 탭의 데이터를 서버에서 불러온다.</summary>
    public async Task InitializeAsync()
    {
        await _products.RefreshAsync();
        await _cart.LoadAsync();
        await _member.LoadInfoAsync();
        await _board.LoadPostsAsync();
    }

    private async Task ShowOrderAsync(List<CartItem> items)
    {
        await _order.SetItemsAsync(items);
        _root.Content = _order;
    }

    private async Task BackToCartAsync()
    {
        await _cart.LoadAsync(); // 결제로 빠진 상품 반영
        _root.Content = _tabs;
        _tabs.SelectedIndex = 1;
    }
}
