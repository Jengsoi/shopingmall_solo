namespace ShoppingMall.Client.Admin;

/// <summary>
/// 재고 변경 이력: 주문·주문취소·상품 등록/수정·재고 차감으로 재고가 바뀐 기록을 최신순으로 보여준다.
/// 상품을 고르면 그 상품의 수정 전/후 버전 이력을 이어서 본다.
/// </summary>
public partial class StockHistoryView : UserControl
{
    /// <summary>상품 선택 상자 항목. ProductId 가 null 이면 "전체 상품".</summary>
    private sealed record ProductChoice(long? ProductId, string Label);

    private readonly InventoryApi _api;
    private bool _loading; // 코드로 선택 상자를 채우는 중에는 이력 조회를 하지 않는다

    public StockHistoryView(InventoryApi api)
    {
        InitializeComponent();
        _api = api;
    }

    private void ProductBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        e.Handled = true; // 바깥 TabControl 까지 올라가지 않게
        if (!_loading) Ui.Fire(this, LoadHistoryAsync);
    }

    private void Table_SelectionChanged(object sender, SelectionChangedEventArgs e) => e.Handled = true;

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await Ui.Guard(this, LoadAsync);

    /// <summary>상품 선택 목록(판매 중인 상품)과 이력을 다시 불러온다.</summary>
    public async Task LoadAsync()
    {
        var products = await _api.ProductListAsync();
        long? keep = (ProductBox.SelectedItem as ProductChoice)?.ProductId;

        // 판매 중인 상품만 고를 수 있게 한다. (어느 버전을 골라도 서버가 수정 전/후 이력을 모두 보여준다)
        var choices = new List<ProductChoice> { new(null, "전체 상품") };
        if (products.Ok)
        {
            choices.AddRange(products.Value
                .Where(p => p.IsActive)
                .Select(p => new ProductChoice(p.ProductId, $"{p.Name} (ID {p.ProductId}, 재고 {Fmt.Num(p.Stock)})")));
        }

        _loading = true;
        ProductBox.ItemsSource = choices;
        ProductBox.SelectedItem = choices.FirstOrDefault(c => c.ProductId == keep) ?? choices[0];
        _loading = false;

        await LoadHistoryAsync();
    }

    /// <summary>선택한 상품(또는 전체)의 재고 이력을 불러온다.</summary>
    private async Task LoadHistoryAsync()
    {
        long? productId = (ProductBox.SelectedItem as ProductChoice)?.ProductId;
        var result = await _api.StockHistoryAsync(productId);
        if (!result.Ok)
        {
            Dialogs.Error(this, result.Message, "재고 이력 조회 실패");
            return;
        }
        Table.ItemsSource = result.Value;
    }
}
