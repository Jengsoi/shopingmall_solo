namespace ShoppingMall.Client.Views;

/// <summary>
/// 일반 회원 화면 (main.py 의 MainWindow).
/// 탭: 상품페이지 / 장바구니 / 주문내역 / 내 정보 / 게시판, 그리고 장바구니에서 넘어가는 주문·결제 화면.
/// </summary>
public sealed class ShopShell : UserControl
{
    private const int ProductsTab = 0;
    private const int CartTab = 1;
    private const int OrdersTab = 2;

    private readonly ProductView _products;
    private readonly CartView _cart;
    private readonly OrderView _order;
    private readonly OrderHistoryView _history;
    private readonly MemberView _member;
    private readonly BoardView _board;
    private readonly TabControl _tabs = new();
    private readonly ContentControl _root = new();
    private bool _productsStale; // 주문 취소로 재고가 바뀌어 상품 목록을 다시 불러와야 함

    public event Action? LogoutRequested;

    public ShopShell(ShopApi api, Member member)
    {
        _products = new ProductView(api);
        _cart = new CartView(api);
        _order = new OrderView(api);
        _history = new OrderHistoryView(api);
        _member = new MemberView(api);
        _board = new BoardView(api);

        _member.LogoutRequested += () => LogoutRequested?.Invoke();
        // 복구된 재고는 상품페이지 탭을 열 때 반영한다. (보이지 않는 탭의 목록을 바로 바꾸면
        // 그 선택 변경 이벤트가 TabControl 까지 올라와 탭이 상품페이지로 넘어가 버린다)
        _history.OrderCancelled += () => _productsStale = true;

        _tabs.Items.Add(new TabItem { Header = "상품페이지", Content = _products });
        _tabs.Items.Add(new TabItem { Header = "장바구니", Content = _cart });
        _tabs.Items.Add(new TabItem { Header = "주문내역", Content = _history });
        _tabs.Items.Add(new TabItem { Header = "내 정보", Content = _member });
        _tabs.Items.Add(new TabItem { Header = "게시판", Content = _board });

        // 장바구니·주문내역 탭으로 바뀔 때마다 최신 데이터로 갱신한다.
        // (탭 안의 ComboBox 등에서 올라오는 SelectionChanged 는 무시)
        _tabs.SelectionChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Source, _tabs)) return;
            if (_tabs.SelectedIndex == ProductsTab && _productsStale)
            {
                _productsStale = false;
                Ui.Fire(this, _products.LoadProductsAsync);
            }
            else if (_tabs.SelectedIndex == CartTab)
                Ui.Fire(this, _cart.LoadAsync);
            else if (_tabs.SelectedIndex == OrdersTab)
                Ui.Fire(this, _history.LoadAsync);
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
        _tabs.SelectedIndex = CartTab;
    }
}
