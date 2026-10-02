namespace ShoppingMall.Client.Admin;

/// <summary>
/// 재고관리 화면: 카테고리 관리 / 상품 관리 / 재고 이력 탭.
/// 추가·수정은 대화상자(CategoryWindow, ProductWindow)로 입력받아 서버에 요청하고, 성공하면 목록을 새로 고친다.
/// </summary>
public partial class InventoryView : UserControl
{
    private readonly InventoryApi _api;
    private readonly StockHistoryView _history;
    private List<AdminCategory> _categories = new(); // 상품 추가/수정 창의 카테고리 선택 목록으로도 쓴다

    public InventoryView(InventoryApi api)
    {
        InitializeComponent();
        _api = api;
        _history = new StockHistoryView(api);
        HistoryTab.Content = _history;
    }

    // ------------------------------------------------------------ 이벤트 처리기 (XAML 과 연결)

    // 재고 이력 탭을 열 때마다 최신 이력을 불러온다. (탭 안의 표·콤보박스에서 올라오는 이벤트는 무시)
    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, Tabs)) return;
        e.Handled = true; // 바깥(관리자 화면) 탭까지 올라가지 않게
        if (Tabs.SelectedItem == HistoryTab)
            Ui.Fire(this, _history.LoadAsync);
    }

    private async void RefreshCategories_Click(object sender, RoutedEventArgs e) => await Ui.Guard(this, LoadCategoriesAsync);
    private async void AddCategory_Click(object sender, RoutedEventArgs e) => await Ui.Guard(this, AddCategoryAsync);
    private async void EditCategory_Click(object sender, RoutedEventArgs e) => await Ui.Guard(this, EditCategoryAsync);
    private async void RefreshProducts_Click(object sender, RoutedEventArgs e) => await Ui.Guard(this, LoadProductsAsync);
    private async void AddProduct_Click(object sender, RoutedEventArgs e) => await Ui.Guard(this, AddProductAsync);
    private async void EditProduct_Click(object sender, RoutedEventArgs e) => await Ui.Guard(this, EditProductAsync);

    /// <summary>서버 연결 후 카테고리와 상품 목록을 모두 불러온다.</summary>
    public async Task LoadAllAsync()
    {
        await LoadCategoriesAsync();
        await LoadProductsAsync();
    }

    // ------------------------------------------------------------ 카테고리

    private async Task LoadCategoriesAsync()
    {
        var result = await _api.CategoryListAsync();
        if (!result.Ok)
        {
            Dialogs.Error(this, result.Message, "카테고리 조회 실패");
            return;
        }

        _categories = result.Value;
        CategoryTable.ItemsSource = _categories;
    }

    /// <summary>카테고리 추가 창 → 확인이면 서버에 추가 요청 → 목록 새로고침</summary>
    private async Task AddCategoryAsync()
    {
        var dialog = new CategoryWindow(null);
        if (!Dialogs.ShowDialog(dialog, this)) return;

        var result = await _api.CategoryAddAsync(dialog.CategoryName);
        if (result.Ok)
        {
            Dialogs.Info(this, result.Message, "카테고리 추가");
            await LoadCategoriesAsync();
        }
        else
        {
            Dialogs.Error(this, result.Message, "카테고리 추가 실패");
        }
    }

    /// <summary>선택한 카테고리 수정 창 → 확인이면 서버에 수정 요청 → 목록 새로고침</summary>
    private async Task EditCategoryAsync()
    {
        if (CategoryTable.SelectedItem is not AdminCategory selected)
        {
            Dialogs.Info(this, "수정할 카테고리를 선택하세요.");
            return;
        }

        var dialog = new CategoryWindow(selected);
        if (!Dialogs.ShowDialog(dialog, this)) return;

        var result = await _api.CategoryUpdateAsync(selected.CategoryId, dialog.CategoryName, dialog.IsCategoryActive);
        if (result.Ok)
        {
            Dialogs.Info(this, result.Message, "카테고리 수정");
            await LoadCategoriesAsync();
        }
        else
        {
            Dialogs.Error(this, result.Message, "카테고리 수정 실패");
        }
    }

    // ------------------------------------------------------------ 상품

    /// <summary>상품 목록 새로 불러오기 (수정으로 판매가 끝난 이전 버전 포함)</summary>
    private async Task LoadProductsAsync()
    {
        var result = await _api.ProductListAsync();
        if (!result.Ok)
        {
            Dialogs.Error(this, result.Message, "상품 조회 실패");
            return;
        }

        ProductTable.ItemsSource = result.Value;
    }

    /// <summary>상품 추가. 활성 카테고리가 하나도 없으면 먼저 카테고리를 만들라고 안내한다.</summary>
    private async Task AddProductAsync()
    {
        var active = _categories.Where(c => c.IsActive).ToList();
        if (active.Count == 0)
        {
            Dialogs.Info(this, "사용할 수 있는 카테고리가 없습니다. 먼저 카테고리를 추가하세요.");
            return;
        }

        var dialog = new ProductWindow(active, null);
        if (!Dialogs.ShowDialog(dialog, this)) return;

        var result = await _api.ProductAddAsync(dialog.Result);
        if (result.Ok)
        {
            Dialogs.Info(this, result.Message, "상품 추가");
            await LoadProductsAsync();
        }
        else
        {
            Dialogs.Error(this, result.Message, "상품 추가 실패");
        }
    }

    /// <summary>
    /// 상품 수정. 이미 수정되어 판매가 끝난 이전 버전은 고칠 수 없으므로 화면에서 먼저 막는다.
    /// (서버도 같은 검사를 하지만, 창을 열기 전에 알려 주는 편이 친절하다)
    /// </summary>
    private async Task EditProductAsync()
    {
        if (ProductTable.SelectedItem is not AdminProduct selected)
        {
            Dialogs.Info(this, "수정할 상품을 선택하세요.");
            return;
        }
        if (!selected.IsActive)
        {
            Dialogs.Info(this, "이미 수정되어 비활성화된 이전 버전입니다.\n'판매중' 상태의 최신 상품을 수정하세요.");
            return;
        }

        var active = _categories.Where(c => c.IsActive).ToList();
        if (active.Count == 0)
        {
            Dialogs.Info(this, "사용할 수 있는 카테고리가 없습니다.");
            return;
        }

        var dialog = new ProductWindow(active, selected); // 기존 값이 채워진 상태로 열린다
        if (!Dialogs.ShowDialog(dialog, this)) return;

        var result = await _api.ProductUpdateAsync(selected.ProductId, dialog.Result);
        if (result.Ok)
        {
            Dialogs.Info(this, result.Message, "상품 수정");
            await LoadProductsAsync();
        }
        else
        {
            Dialogs.Error(this, result.Message, "상품 수정 실패");
        }
    }
}
