namespace ShoppingMall.Client.Views;

/// <summary>
/// 상품페이지. 카테고리·검색어로 거른 상품 표, 선택한 상품 정보, 장바구니 담기.
/// 표에서 상품을 고르면 서버에서 상세 정보를 다시 받아 최신 재고를 보여준다.
/// </summary>
public partial class ProductView : UserControl
{
    private readonly ShopApi _api;
    private long? _selectedProductId; // 장바구니에 담을 상품 (상세를 불러온 상품)

    // 코드에서 카테고리 목록을 채우는 동안에는 SelectionChanged 가 발생해도 상품을 다시 불러오지 않게 한다.
    private bool _loading;

    public ProductView(ShopApi api)
    {
        InitializeComponent();
        _api = api;
    }

    // ------------------------------------------------------------ 이벤트 처리기 (XAML 과 연결)

    // 카테고리를 바꾸면 바로 다시 조회
    private void CategoryBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        e.Handled = true; // 바깥 TabControl 까지 올라가지 않게
        if (!_loading) Ui.Fire(this, LoadProductsAsync);
    }

    private async void Search_Click(object sender, RoutedEventArgs e) => await Ui.Guard(this, LoadProductsAsync);

    // 검색어 칸에서 Enter 를 눌러도 조회
    private void KeywordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Ui.Fire(this, LoadProductsAsync);
    }

    private void Table_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        e.Handled = true;
        Ui.Fire(this, ShowDetailAsync);
    }

    private async void AddToCart_Click(object sender, RoutedEventArgs e) => await Ui.Guard(this, AddToCartAsync);

    // ------------------------------------------------------------ 동작

    /// <summary>카테고리 목록을 다시 불러오고 상품 목록도 갱신한다.</summary>
    public async Task RefreshAsync()
    {
        var result = await _api.CategoryListAsync();

        _loading = true;
        var items = new List<CategoryInfo> { new(0, "전체") }; // ID 0 = 전체 카테고리
        if (result.Ok) items.AddRange(result.Value);
        CategoryBox.ItemsSource = items;
        CategoryBox.SelectedIndex = 0;
        _loading = false;

        if (!result.Ok)
            Dialogs.Error(this, result.Message);

        await LoadProductsAsync();
    }

    /// <summary>지금 고른 카테고리·검색어로 상품 목록을 다시 불러온다.</summary>
    public async Task LoadProductsAsync()
    {
        long? categoryId = (CategoryBox.SelectedItem as CategoryInfo)?.CategoryId;
        if (categoryId == 0) categoryId = null;

        var result = await _api.ProductListAsync(categoryId, (KeywordBox.Text ?? "").Trim());
        if (!result.Ok)
        {
            Dialogs.Error(this, result.Message);
            return;
        }

        Table.ItemsSource = result.Value;
        DetailText.Text = "상품을 선택하면 상세 정보가 표시됩니다.";
        AddToCartButton.IsEnabled = false;
        _selectedProductId = null;
    }

    /// <summary>표에서 고른 상품의 상세를 서버에서 받아 아래쪽에 보여준다. 품절이면 담기 버튼을 끈다.</summary>
    private async Task ShowDetailAsync()
    {
        if (Table.SelectedItem is not ProductRow row) return;

        var result = await _api.ProductDetailAsync(row.ProductId);
        if (!result.Ok)
        {
            Dialogs.Error(this, result.Message);
            return;
        }

        _selectedProductId = result.Value.ProductId;
        DetailText.Text = result.Value.Summary;
        AddToCartButton.IsEnabled = result.Value.Stock > 0;
        MessageText.Text = "";
    }

    /// <summary>고른 상품을 수량만큼 장바구니에 담는다. 결과는 아래쪽 문구로 알려 준다.</summary>
    private async Task AddToCartAsync()
    {
        if (_selectedProductId is not long productId)
        {
            MessageText.Text = "먼저 상품을 선택하세요.";
            return;
        }
        if (Ui.ReadLong(QuantityBox, 1, 999) is not long quantity)
        {
            MessageText.Text = "수량은 1~999 사이의 숫자로 입력하세요.";
            return;
        }

        var result = await _api.CartAddAsync(productId, quantity);
        MessageText.Text = result.Ok ? "장바구니에 담았습니다." : result.Message;
    }
}
