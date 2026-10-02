namespace ShoppingMall.Client.Views;

/// <summary>
/// 장바구니. 목록에서 여러 항목을 골라 삭제하거나 구매할 수 있다.
/// 구매 버튼은 바로 주문하지 않고 OrderRequested 로 선택 항목을 넘겨 주문/결제 확인 화면으로 이동한다.
/// </summary>
public partial class CartView : UserControl
{
    private readonly ShopApi _api;
    private List<CartItem> _items = new();

    /// <summary>구매 버튼: 주문할 항목 목록을 ShopShell 에 넘긴다.</summary>
    public event Action<List<CartItem>>? OrderRequested;

    public CartView(ShopApi api)
    {
        InitializeComponent();
        _api = api;
        // 목록 선택이 바뀌는 이벤트가 바깥 TabControl 까지 올라가지 않게 여기서 멈춘다.
        ItemList.SelectionChanged += (_, e) => e.Handled = true;
    }

    private async void DeleteAll_Click(object sender, RoutedEventArgs e) => await Ui.Guard(this, DeleteAllAsync);
    private async void DeleteSelected_Click(object sender, RoutedEventArgs e) => await Ui.Guard(this, DeleteSelectedAsync);
    private void OrderSelected_Click(object sender, RoutedEventArgs e) => OrderSelected();
    private void OrderAll_Click(object sender, RoutedEventArgs e) => OrderAll();

    /// <summary>서버에서 실제 장바구니 목록을 가져온다.</summary>
    public async Task LoadAsync()
    {
        var result = await _api.CartListAsync();
        if (!result.Ok)
        {
            Dialogs.Error(this, result.Message);
            return;
        }

        _items = result.Value;
        ItemList.ItemsSource = _items;
        TotalText.Text = "총합계: " + Fmt.Won(_items.Sum(i => i.Subtotal));
    }

    /// <summary>목록에서 선택한 항목들</summary>
    private List<CartItem> Selected() => ItemList.SelectedItems.OfType<CartItem>().ToList();

    /// <summary>모든 항목 삭제 (서버에는 항목마다 삭제 요청)</summary>
    private async Task DeleteAllAsync()
    {
        foreach (var item in _items.ToList()) // 복사본으로 돌면서 삭제
            await _api.CartDeleteAsync(item.CartId);
        await LoadAsync();
    }

    private async Task DeleteSelectedAsync()
    {
        foreach (var item in Selected())
            await _api.CartDeleteAsync(item.CartId);
        await LoadAsync();
    }

    private void OrderAll()
    {
        if (_items.Count == 0)
        {
            Dialogs.Info(this, "장바구니가 비어있습니다.");
            return;
        }
        OrderRequested?.Invoke(_items.ToList());
    }

    private void OrderSelected()
    {
        var selected = Selected();
        if (selected.Count == 0)
        {
            Dialogs.Info(this, "주문할 상품을 선택하세요.");
            return;
        }
        OrderRequested?.Invoke(selected);
    }
}
