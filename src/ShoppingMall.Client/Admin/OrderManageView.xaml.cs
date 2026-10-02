namespace ShoppingMall.Client.Admin;

/// <summary>
/// 주문 관리: 전체 주문을 상태별로 보고, 한 단계씩 진행(결제완료 → 배송준비중 → 배송중 → 배송완료)하거나
/// 배송완료 전 주문을 취소한다. 취소하면 서버가 재고를 되돌린다.
/// </summary>
public partial class OrderManageView : UserControl
{
    private readonly InventoryApi _api;
    private bool _loading; // 코드로 표를 다시 채우는 중에는 선택 변경 이벤트를 무시

    public OrderManageView(InventoryApi api)
    {
        InitializeComponent();
        _api = api;

        // 상태 필터 목록: "전체" + 상태별 한글 이름. 0번(전체)을 빼면 OrderStatus.All 과 순서가 같다.
        _loading = true;
        FilterBox.ItemsSource = new[] { "전체" }.Concat(OrderStatus.All.Select(OrderStatus.Label)).ToList();
        FilterBox.SelectedIndex = 0;
        _loading = false;

        LineList.SelectionChanged += (_, e) => e.Handled = true;
    }

    private AdminOrder? Selected => Table.SelectedItem as AdminOrder;

    /// <summary>필터에서 고른 상태 값 (전체면 null)</summary>
    private string? FilterStatus => FilterBox.SelectedIndex > 0 ? OrderStatus.All[FilterBox.SelectedIndex - 1] : null;

    // ------------------------------------------------------------ 이벤트 처리기 (XAML 과 연결)

    private void FilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        e.Handled = true; // 바깥 TabControl 까지 올라가지 않게
        if (!_loading) Ui.Fire(this, LoadAsync);
    }

    private void Table_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        e.Handled = true;
        if (!_loading) Ui.Fire(this, LoadSelectedAsync);
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await Ui.Guard(this, LoadAsync);
    private async void Advance_Click(object sender, RoutedEventArgs e) => await Ui.Guard(this, AdvanceAsync);
    private async void Cancel_Click(object sender, RoutedEventArgs e) => await Ui.Guard(this, CancelAsync);

    // ------------------------------------------------------------ 동작

    /// <summary>필터 조건으로 주문 목록을 불러온다. 보고 있던 주문은 다시 선택해 둔다.</summary>
    public async Task LoadAsync()
    {
        var result = await _api.OrderListAsync(FilterStatus);
        if (!result.Ok)
        {
            Dialogs.Error(this, result.Message, "주문 조회 실패");
            return;
        }

        long? keep = Selected?.OrderId;
        _loading = true;
        Table.ItemsSource = result.Value;
        Table.SelectedItem = result.Value.FirstOrDefault(o => o.OrderId == keep);
        _loading = false;
        await LoadSelectedAsync();
    }

    /// <summary>
    /// 선택한 주문의 상품을 보여주고, 버튼 상태를 맞춘다.
    /// 다음 단계 버튼은 "→ 배송중" 처럼 바뀔 상태를 글자로 보여주고, 더 진행할 수 없으면 꺼진다.
    /// </summary>
    private async Task LoadSelectedAsync()
    {
        var order = Selected;
        string? next = order?.NextStatus;
        AdvanceButton.Content = next is null ? "다음 단계로" : $"→ {OrderStatus.Label(next)}";
        AdvanceButton.IsEnabled = next is not null;
        CancelButton.IsEnabled = order?.CanCancel == true;

        if (order is null)
        {
            DetailTitle.Text = "주문 상품";
            LineList.ItemsSource = null;
            return;
        }

        DetailTitle.Text = $"주문 상품 (주문번호 {order.OrderId} · {order.MemberText} · {order.StatusText})";
        var result = await _api.OrderDetailAsync(order.OrderId);
        LineList.ItemsSource = result.Ok ? result.Value : null;
        if (!result.Ok)
            Dialogs.Error(this, result.Message);
    }

    /// <summary>확인을 받은 뒤 주문을 다음 단계로 진행한다. 실패해도 목록을 새로 고쳐 최신 상태를 보여준다.</summary>
    private async Task AdvanceAsync()
    {
        // 속성 패턴: 선택한 주문이 있고 NextStatus 가 null 이 아니면 order, next 에 담는다.
        if (Selected is not { NextStatus: { } next } order)
            return;

        if (!Dialogs.Confirm(this,
                $"주문번호 {order.OrderId}을(를) '{order.StatusText}' → '{OrderStatus.Label(next)}'(으)로 변경하시겠습니까?", "주문 상태 변경"))
            return;

        var result = await _api.OrderAdvanceAsync(order.OrderId, next);
        if (!result.Ok)
            Dialogs.Error(this, result.Message, "실패");
        await LoadAsync();
    }

    /// <summary>확인을 받은 뒤 관리자 취소 (배송완료 전까지). 서버가 재고를 되돌린다.</summary>
    private async Task CancelAsync()
    {
        if (Selected is not { CanCancel: true } order)
            return;

        if (!Dialogs.Confirm(this,
                $"주문번호 {order.OrderId} ({order.MemberText}, {order.TotalText}) 주문을 취소하시겠습니까?\n주문 수량만큼 재고가 복구됩니다.", "주문취소"))
            return;

        var result = await _api.OrderCancelAsync(order.OrderId);
        if (result.Ok)
            Dialogs.Info(this, result.Message, "완료");
        else
            Dialogs.Error(this, result.Message, "실패");
        await LoadAsync();
    }
}
