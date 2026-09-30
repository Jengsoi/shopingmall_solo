namespace ShoppingMall.Client.Admin;

/// <summary>
/// 주문 관리: 전체 주문을 상태별로 보고, 한 단계씩 진행(결제완료 → 배송준비중 → 배송중 → 배송완료)하거나
/// 배송완료 전 주문을 취소한다. 취소하면 서버가 재고를 되돌린다.
/// </summary>
public sealed class OrderManageView : UserControl
{
    private static readonly string[] FilterLabels =
        new[] { "전체" }.Concat(OrderStatus.All.Select(OrderStatus.Label)).ToArray();

    private readonly InventoryApi _api;
    private readonly ComboBox _filter = new() { ItemsSource = FilterLabels, SelectedIndex = 0, MinWidth = 140 };
    private readonly DataGrid _table;
    private readonly ListBox _lines = new() { Height = 140 };
    private readonly TextBlock _detailTitle = Ui.Text("주문 상품", 14, true);
    private readonly Button _advance;
    private readonly Button _cancel;
    private bool _loading;

    public OrderManageView(InventoryApi api)
    {
        _api = api;

        _table = Ui.Table(
            ("주문번호", nameof(AdminOrder.OrderId), 0),
            ("주문일시", nameof(AdminOrder.OrderedAt), 1.5),
            ("주문자", nameof(AdminOrder.MemberText), 1.5),
            ("상태", nameof(AdminOrder.StatusText), 0),
            ("결제금액", nameof(AdminOrder.TotalText), 1));
        _table.SelectionChanged += (_, e) =>
        {
            e.Handled = true; // 바깥 TabControl 까지 올라가지 않게
            if (!_loading) Ui.Fire(this, LoadSelectedAsync);
        };
        _filter.SelectionChanged += (_, e) =>
        {
            e.Handled = true;
            Ui.Fire(this, LoadAsync);
        };

        _advance = Ui.Btn("다음 단계로", AdvanceAsync, 150);
        _cancel = Ui.Btn("주문취소", CancelAsync);
        _advance.IsEnabled = _cancel.IsEnabled = false;

        var left = Ui.HStack(8, Ui.Text("상태"), _filter, Ui.Btn("새로고침", LoadAsync));
        var right = Ui.HStack(8, _advance, _cancel);
        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(0, 0, 0, 8) };
        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 2);
        bar.Children.Add(left);
        bar.Children.Add(right);

        var bottom = Ui.VStack(8, _detailTitle, _lines);
        bottom.Margin = new Thickness(0, 12, 0, 0);

        Content = new Border
        {
            Padding = new Thickness(12),
            Child = Ui.Dock(Ui.VStack(8, Ui.Title("주문관리"), bar), _table, bottom),
        };
    }

    private AdminOrder? Selected => _table.SelectedItem as AdminOrder;

    private string? FilterStatus => _filter.SelectedIndex > 0 ? OrderStatus.All[_filter.SelectedIndex - 1] : null;

    public async Task LoadAsync()
    {
        var result = await _api.OrderListAsync(FilterStatus);
        if (!result.Ok)
        {
            await Dialogs.ErrorAsync(this, result.Message, "주문 조회 실패");
            return;
        }

        long? keep = Selected?.OrderId;
        _loading = true;
        _table.ItemsSource = result.Value;
        _table.SelectedItem = result.Value.FirstOrDefault(o => o.OrderId == keep);
        _loading = false;
        await LoadSelectedAsync();
    }

    private async Task LoadSelectedAsync()
    {
        var order = Selected;
        string? next = order?.NextStatus;
        _advance.Content = next is null ? "다음 단계로" : $"→ {OrderStatus.Label(next)}";
        _advance.IsEnabled = next is not null;
        _cancel.IsEnabled = order?.CanCancel == true;

        if (order is null)
        {
            _detailTitle.Text = "주문 상품";
            _lines.ItemsSource = null;
            return;
        }

        _detailTitle.Text = $"주문 상품 (주문번호 {order.OrderId} · {order.MemberText} · {order.StatusText})";
        var result = await _api.OrderDetailAsync(order.OrderId);
        _lines.ItemsSource = result.Ok ? result.Value : null;
        if (!result.Ok)
            await Dialogs.ErrorAsync(this, result.Message);
    }

    private async Task AdvanceAsync()
    {
        if (Selected is not { NextStatus: { } next } order)
            return;

        if (!await Dialogs.ConfirmAsync(this,
                $"주문번호 {order.OrderId}을(를) '{order.StatusText}' → '{OrderStatus.Label(next)}'(으)로 변경하시겠습니까?", "주문 상태 변경"))
            return;

        var result = await _api.OrderAdvanceAsync(order.OrderId, next);
        if (!result.Ok)
            await Dialogs.ErrorAsync(this, result.Message, "실패");
        await LoadAsync();
    }

    private async Task CancelAsync()
    {
        if (Selected is not { CanCancel: true } order)
            return;

        if (!await Dialogs.ConfirmAsync(this,
                $"주문번호 {order.OrderId} ({order.MemberText}, {order.TotalText}) 주문을 취소하시겠습니까?\n주문 수량만큼 재고가 복구됩니다.", "주문취소"))
            return;

        var result = await _api.OrderCancelAsync(order.OrderId);
        if (result.Ok)
        {
            await Dialogs.InfoAsync(this, result.Message, "완료");
        }
        else
        {
            await Dialogs.ErrorAsync(this, result.Message, "실패");
        }
        await LoadAsync();
    }
}
