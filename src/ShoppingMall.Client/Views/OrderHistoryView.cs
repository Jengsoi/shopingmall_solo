namespace ShoppingMall.Client.Views;

/// <summary>
/// 주문내역: 내 주문 목록과 선택한 주문의 상품을 보여주고, 결제완료 주문을 취소한다.
/// 취소하면 서버가 주문 수량만큼 재고를 되돌린다.
/// </summary>
public sealed class OrderHistoryView : UserControl
{
    private readonly ShopApi _api;
    private readonly DataGrid _table;
    private readonly ListBox _lines = new() { Height = 150 };
    private readonly TextBlock _detailTitle = Ui.Text("주문 상품", 14, true);
    private readonly Button _cancel;
    private List<OrderSummary> _orders = new();

    /// <summary>주문을 취소해 재고가 바뀌었을 때 (상품 목록 갱신용)</summary>
    public event Action? OrderCancelled;

    public OrderHistoryView(ShopApi api)
    {
        _api = api;

        _table = Ui.Table(
            ("주문번호", nameof(OrderSummary.OrderId), 0),
            ("주문일시", nameof(OrderSummary.OrderedAt), 2),
            ("상태", nameof(OrderSummary.StatusText), 0),
            ("결제금액", nameof(OrderSummary.TotalText), 1));
        _table.SelectionChanged += (_, _) => Ui.Fire(this, LoadSelectedAsync);

        _cancel = Ui.Btn("주문취소", CancelAsync);
        _cancel.IsEnabled = false;

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, 8) };
        var title = Ui.Title("[주문내역]");
        var buttons = Ui.HStack(8, Ui.Btn("새로고침", LoadAsync), _cancel);
        Grid.SetColumn(title, 0);
        Grid.SetColumn(buttons, 1);
        header.Children.Add(title);
        header.Children.Add(buttons);

        var bottom = Ui.VStack(8, _detailTitle, _lines);
        bottom.Margin = new Thickness(0, 12, 0, 0);

        Content = new Border
        {
            Padding = new Thickness(12),
            Child = Ui.Dock(header, _table, bottom),
        };
    }

    private OrderSummary? Selected => _table.SelectedItem as OrderSummary;

    /// <summary>서버에서 내 주문 목록을 가져온다.</summary>
    public async Task LoadAsync()
    {
        var result = await _api.OrderListAsync();
        if (!result.Ok)
        {
            await Dialogs.ErrorAsync(this, result.Message);
            return;
        }

        long? keep = Selected?.OrderId;
        _orders = result.Value;
        _table.ItemsSource = _orders;
        _table.SelectedItem = _orders.FirstOrDefault(o => o.OrderId == keep);
        await LoadSelectedAsync();
    }

    private async Task LoadSelectedAsync()
    {
        var order = Selected;
        _cancel.IsEnabled = order?.CanCancel == true;

        if (order is null)
        {
            _detailTitle.Text = "주문 상품";
            _lines.ItemsSource = null;
            return;
        }

        _detailTitle.Text = $"주문 상품 (주문번호 {order.OrderId} · {order.StatusText})";
        var result = await _api.OrderDetailAsync(order.OrderId);
        _lines.ItemsSource = result.Ok ? result.Value : null;
        if (!result.Ok)
            await Dialogs.ErrorAsync(this, result.Message);
    }

    private async Task CancelAsync()
    {
        var order = Selected;
        if (order is null || !order.CanCancel)
        {
            await Dialogs.InfoAsync(this, "취소할 주문을 선택하세요.");
            return;
        }

        if (!await Dialogs.ConfirmAsync(this,
                $"주문번호 {order.OrderId} ({order.TotalText}) 주문을 취소하시겠습니까?", "주문취소"))
            return;

        var result = await _api.OrderCancelAsync(order.OrderId);
        if (!result.Ok)
        {
            await Dialogs.ErrorAsync(this, result.Message, "실패");
            return;
        }

        await Dialogs.InfoAsync(this, result.Message, "완료");
        OrderCancelled?.Invoke();
        await LoadAsync();
    }
}
