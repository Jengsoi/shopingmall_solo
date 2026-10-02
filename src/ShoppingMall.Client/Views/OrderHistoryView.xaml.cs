namespace ShoppingMall.Client.Views;

/// <summary>
/// 주문내역: 내 주문 목록과 선택한 주문의 상품을 보여주고, 결제완료 주문을 취소한다.
/// 취소하면 서버가 주문 수량만큼 재고를 되돌린다.
/// </summary>
public partial class OrderHistoryView : UserControl
{
    private readonly ShopApi _api;
    private List<OrderSummary> _orders = new(); // 지금 표에 보이는 주문 목록
    private bool _loading; // 코드로 표를 다시 채우는 중에는 선택 변경 이벤트를 무시

    /// <summary>주문을 취소해 재고가 바뀌었을 때 (상품 목록 갱신용)</summary>
    public event Action? OrderCancelled;

    public OrderHistoryView(ShopApi api)
    {
        InitializeComponent();
        _api = api;
        LineList.SelectionChanged += (_, e) => e.Handled = true; // 바깥 TabControl 까지 올라가지 않게
    }

    private OrderSummary? Selected => Table.SelectedItem as OrderSummary;

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await Ui.Guard(this, LoadAsync);

    private async void Cancel_Click(object sender, RoutedEventArgs e) => await Ui.Guard(this, CancelAsync);

    // 주문을 고르면 아래쪽에 그 주문의 상품을 보여준다.
    private void Table_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        e.Handled = true;
        if (!_loading) Ui.Fire(this, LoadSelectedAsync);
    }

    /// <summary>서버에서 내 주문 목록을 가져온다.</summary>
    public async Task LoadAsync()
    {
        var result = await _api.OrderListAsync();
        if (!result.Ok)
        {
            Dialogs.Error(this, result.Message);
            return;
        }

        // 새로 불러와도 보고 있던 주문의 선택이 풀리지 않도록 주문번호로 다시 찾아 선택한다.
        long? keep = Selected?.OrderId;
        _loading = true;
        _orders = result.Value;
        Table.ItemsSource = _orders;
        Table.SelectedItem = _orders.FirstOrDefault(o => o.OrderId == keep);
        _loading = false;
        await LoadSelectedAsync();
    }

    /// <summary>선택한 주문의 상품을 불러오고, 취소 가능한 상태(결제완료)일 때만 취소 버튼을 켠다.</summary>
    private async Task LoadSelectedAsync()
    {
        var order = Selected;
        CancelButton.IsEnabled = order?.CanCancel == true;

        if (order is null)
        {
            DetailTitle.Text = "주문 상품";
            LineList.ItemsSource = null;
            return;
        }

        DetailTitle.Text = $"주문 상품 (주문번호 {order.OrderId} · {order.StatusText})";
        var result = await _api.OrderDetailAsync(order.OrderId);
        LineList.ItemsSource = result.Ok ? result.Value : null;
        if (!result.Ok)
            Dialogs.Error(this, result.Message);
    }

    /// <summary>확인을 받은 뒤 주문 취소. 서버가 재고를 되돌리고, 안내 문구를 돌려준다.</summary>
    private async Task CancelAsync()
    {
        var order = Selected;
        if (order is null || !order.CanCancel)
        {
            Dialogs.Info(this, "취소할 주문을 선택하세요.");
            return;
        }

        if (!Dialogs.Confirm(this, $"주문번호 {order.OrderId} ({order.TotalText}) 주문을 취소하시겠습니까?", "주문취소"))
            return;

        var result = await _api.OrderCancelAsync(order.OrderId);
        if (!result.Ok)
        {
            Dialogs.Error(this, result.Message, "실패");
            return;
        }

        Dialogs.Info(this, result.Message, "완료");
        OrderCancelled?.Invoke();
        await LoadAsync();
    }
}
