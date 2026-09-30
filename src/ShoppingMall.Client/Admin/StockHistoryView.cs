namespace ShoppingMall.Client.Admin;

/// <summary>
/// 재고 변경 이력: 주문·주문취소·상품 등록/수정·재고 차감으로 재고가 바뀐 기록을 최신순으로 보여준다.
/// 상품을 고르면 그 상품의 수정 전/후 버전 이력을 이어서 본다.
/// </summary>
public sealed class StockHistoryView : UserControl
{
    private sealed record ProductChoice(long? ProductId, string Label)
    {
        public override string ToString() => Label;
    }

    private readonly InventoryApi _api;
    private readonly ComboBox _product = new() { MinWidth = 260 };
    private readonly DataGrid _table;
    private bool _loading;

    public StockHistoryView(InventoryApi api)
    {
        _api = api;

        // 열이 많아서 모두 비율 폭으로 나눈다. (Auto 열을 섞으면 데이터가 오기 전 헤더 폭으로 굳어
        // 뒤쪽 열이 잘린다) 비율 열은 헤더 글자 수만큼의 최소 폭이 보장된다.
        _table = Ui.Table(
            ("일시", nameof(StockHistoryEntry.CreatedAt), 2.5),
            ("상품ID", nameof(StockHistoryEntry.ProductId), 0.7),
            ("상품명", nameof(StockHistoryEntry.ProductName), 1.6),
            ("사유", nameof(StockHistoryEntry.ReasonText), 0.9),
            ("변동", nameof(StockHistoryEntry.ChangeText), 0.6),
            ("재고", nameof(StockHistoryEntry.StockText), 1),
            ("주문번호", nameof(StockHistoryEntry.OrderText), 0.8),
            ("처리자", nameof(StockHistoryEntry.MemberText), 1.1));
        _table.SelectionChanged += (_, e) => e.Handled = true; // 바깥 TabControl 까지 올라가지 않게

        _product.SelectionChanged += (_, e) =>
        {
            e.Handled = true;
            if (!_loading) Ui.Fire(this, LoadHistoryAsync);
        };

        var bar = Ui.HStack(8, Ui.Text("상품"), _product, Ui.Btn("새로고침", LoadAsync));
        bar.Margin = new Thickness(0, 8);

        Content = Ui.Dock(bar, _table);
    }

    /// <summary>상품 선택 목록(판매 중인 상품)과 이력을 다시 불러온다.</summary>
    public async Task LoadAsync()
    {
        var products = await _api.ProductListAsync();
        long? keep = (_product.SelectedItem as ProductChoice)?.ProductId;

        var choices = new List<ProductChoice> { new(null, "전체 상품") };
        if (products.Ok)
        {
            choices.AddRange(products.Value
                .Where(p => p.IsActive)
                .Select(p => new ProductChoice(p.ProductId, $"{p.Name} (ID {p.ProductId}, 재고 {Fmt.Num(p.Stock)})")));
        }

        _loading = true;
        _product.ItemsSource = choices;
        _product.SelectedItem = choices.FirstOrDefault(c => c.ProductId == keep) ?? choices[0];
        _loading = false;

        await LoadHistoryAsync();
    }

    private async Task LoadHistoryAsync()
    {
        long? productId = (_product.SelectedItem as ProductChoice)?.ProductId;
        var result = await _api.StockHistoryAsync(productId);
        if (!result.Ok)
        {
            await Dialogs.ErrorAsync(this, result.Message, "재고 이력 조회 실패");
            return;
        }
        _table.ItemsSource = result.Value;
    }
}
