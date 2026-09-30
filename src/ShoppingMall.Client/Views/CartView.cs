namespace ShoppingMall.Client.Views;

/// <summary>
/// 장바구니. 목록에서 여러 항목을 골라 삭제하거나 구매할 수 있다.
/// 구매 버튼은 바로 주문하지 않고 OrderRequested 로 선택 항목을 넘겨 주문/결제 확인 화면으로 이동한다.
/// </summary>
public sealed class CartView : UserControl
{
    private readonly ShopApi _api;
    // Multiple | Toggle: 클릭할 때마다 선택/해제가 바뀌어서 Ctrl 없이도 여러 개를 고를 수 있다.
    private readonly ListBox _list = new() { SelectionMode = SelectionMode.Multiple | SelectionMode.Toggle };
    private readonly TextBlock _total = Ui.Text("총합계: 0원", 16, true);
    private List<CartItem> _items = new();

    /// <summary>구매 버튼: 주문할 항목 목록을 ShopShell 에 넘긴다.</summary>
    public event Action<List<CartItem>>? OrderRequested;

    public CartView(ShopApi api)
    {
        _api = api;

        var buttons = Ui.HStack(8,
            Ui.Btn("전체삭제", DeleteAllAsync),
            Ui.Btn("선택삭제", DeleteSelectedAsync),
            Ui.Btn("선택구매", OrderSelectedAsync),
            Ui.Btn("전체구매", OrderAllAsync));
        buttons.Margin = new Thickness(0, 8, 0, 0);

        var bottom = Ui.VStack(8, _total, buttons);
        bottom.Margin = new Thickness(0, 8, 0, 0);

        Content = new Border
        {
            Padding = new Thickness(12),
            Child = Ui.Dock(Ui.Title("[장바구니]"), _list, bottom),
        };
    }

    /// <summary>서버에서 실제 장바구니 목록을 가져온다.</summary>
    public async Task LoadAsync()
    {
        var result = await _api.CartListAsync();
        if (!result.Ok)
        {
            await Dialogs.ErrorAsync(this, result.Message);
            return;
        }

        _items = result.Value;
        _list.ItemsSource = _items;
        _total.Text = "총합계: " + Fmt.Won(_items.Sum(i => i.Subtotal));
    }

    /// <summary>목록에서 선택한 항목들</summary>
    private List<CartItem> Selected() => _list.SelectedItems?.OfType<CartItem>().ToList() ?? new List<CartItem>();

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

    private async Task OrderAllAsync()
    {
        if (_items.Count == 0)
        {
            await Dialogs.InfoAsync(this, "장바구니가 비어있습니다.");
            return;
        }
        OrderRequested?.Invoke(_items.ToList());
    }

    private async Task OrderSelectedAsync()
    {
        var selected = Selected();
        if (selected.Count == 0)
        {
            await Dialogs.InfoAsync(this, "주문할 상품을 선택하세요.");
            return;
        }
        OrderRequested?.Invoke(selected);
    }
}
