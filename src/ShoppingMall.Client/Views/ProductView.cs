namespace ShoppingMall.Client.Views;

/// <summary>상품페이지 (gui/product_view.py)</summary>
public sealed class ProductView : UserControl
{
    private readonly ShopApi _api;
    private readonly ComboBox _category = new() { MinWidth = 160 };
    private readonly TextBox _keyword = Ui.Input("상품명 검색...");
    private readonly DataGrid _table;
    private readonly TextBlock _detail = Ui.Text("상품을 선택하면 상세 정보가 표시됩니다.");
    private readonly NumericUpDown _quantity = new() { Minimum = 1, Maximum = 999, Value = 1, Increment = 1, FormatString = "0", Width = 120 };
    private readonly Button _addToCart;
    private readonly TextBlock _message = Ui.Text("");
    private long? _selectedProductId;
    private bool _loading;

    public ProductView(ShopApi api)
    {
        _api = api;

        _table = Ui.Table(
            ("상품명", nameof(ProductRow.Name), 3),
            ("색상", nameof(ProductRow.ColorText), 1),
            ("사이즈", nameof(ProductRow.SizeText), 1),
            ("가격", nameof(ProductRow.PriceText), 1.2),
            ("재고", nameof(ProductRow.StockText), 0.8));
        _table.SelectionChanged += (_, _) => Ui.Fire(this, ShowDetailAsync);

        _category.SelectionChanged += (_, _) =>
        {
            if (!_loading) Ui.Fire(this, LoadProductsAsync);
        };
        _keyword.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) Ui.Fire(this, LoadProductsAsync);
        };

        _addToCart = Ui.Btn("장바구니 담기", AddToCartAsync, 120);
        _addToCart.IsEnabled = false;

        var searchRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"), ColumnSpacing = 8 };
        var label = Ui.Text("카테고리");
        var search = Ui.Btn("검색", LoadProductsAsync);
        Grid.SetColumn(label, 0);
        Grid.SetColumn(_category, 1);
        Grid.SetColumn(_keyword, 2);
        Grid.SetColumn(search, 3);
        searchRow.Children.Add(label);
        searchRow.Children.Add(_category);
        searchRow.Children.Add(_keyword);
        searchRow.Children.Add(search);

        var top = Ui.VStack(8, Ui.Title("[상품페이지]"), searchRow);
        top.Margin = new Thickness(0, 0, 0, 8);

        var bottom = Ui.VStack(8,
            Ui.Text("상품 정보", 14, true),
            _detail,
            Ui.HStack(8, Ui.Text("수량"), _quantity, _addToCart),
            _message);
        bottom.Margin = new Thickness(0, 8, 0, 0);

        Content = new Border { Padding = new Thickness(12), Child = Ui.Dock(top, _table, bottom) };
    }

    /// <summary>카테고리 목록을 다시 불러오고 상품 목록도 갱신한다.</summary>
    public async Task RefreshAsync()
    {
        var result = await _api.CategoryListAsync();

        _loading = true;
        var items = new List<CategoryInfo> { new(0, "전체") };
        if (result.Ok) items.AddRange(result.Value);
        _category.ItemsSource = items;
        _category.SelectedIndex = 0;
        _loading = false;

        if (!result.Ok)
            await Dialogs.ErrorAsync(this, result.Message);

        await LoadProductsAsync();
    }

    public async Task LoadProductsAsync()
    {
        long? categoryId = (_category.SelectedItem as CategoryInfo)?.CategoryId;
        if (categoryId == 0) categoryId = null;

        var result = await _api.ProductListAsync(categoryId, (_keyword.Text ?? "").Trim());
        if (!result.Ok)
        {
            await Dialogs.ErrorAsync(this, result.Message);
            return;
        }

        _table.ItemsSource = result.Value;
        _detail.Text = "상품을 선택하면 상세 정보가 표시됩니다.";
        _addToCart.IsEnabled = false;
        _selectedProductId = null;
    }

    private async Task ShowDetailAsync()
    {
        if (_table.SelectedItem is not ProductRow row) return;

        var result = await _api.ProductDetailAsync(row.ProductId);
        if (!result.Ok)
        {
            await Dialogs.ErrorAsync(this, result.Message);
            return;
        }

        _selectedProductId = result.Value.ProductId;
        _detail.Text = result.Value.Summary;
        _addToCart.IsEnabled = result.Value.Stock > 0;
    }

    private async Task AddToCartAsync()
    {
        if (_selectedProductId is not long productId)
        {
            _message.Text = "먼저 상품을 선택하세요.";
            return;
        }

        long quantity = (long)(_quantity.Value ?? 1);
        var result = await _api.CartAddAsync(productId, quantity);
        _message.Text = result.Ok ? "장바구니에 담았습니다." : result.Message;
    }
}
