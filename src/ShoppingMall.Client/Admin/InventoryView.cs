namespace ShoppingMall.Client.Admin;

/// <summary>재고관리 화면: 카테고리 관리 / 상품 관리 탭 (admin/view/inventory_view.py 의 InventoryWidget)</summary>
public sealed class InventoryView : UserControl
{
    private readonly InventoryApi _api;
    private readonly DataGrid _categoryTable;
    private readonly DataGrid _productTable;
    private List<AdminCategory> _categories = new();

    public InventoryView(InventoryApi api)
    {
        _api = api;

        _categoryTable = Ui.Table(
            ("카테고리ID", nameof(AdminCategory.CategoryId), 1),
            ("카테고리명", nameof(AdminCategory.Name), 3),
            ("활성 여부", nameof(AdminCategory.ActiveText), 1));
        _categoryTable.DoubleTapped += (_, _) => Ui.Fire(this, EditCategoryAsync);

        _productTable = Ui.Table(
            // 짧은 값만 들어가는 열은 내용 폭(0 = Auto)에 맞추고, 남는 폭은 카테고리·상품명에 준다.
            ("상품ID", nameof(AdminProduct.ProductId), 0),
            ("카테고리", nameof(AdminProduct.CategoryName), 1),
            ("상품명", nameof(AdminProduct.Name), 2),
            ("색상", nameof(AdminProduct.Color), 0),
            ("사이즈", nameof(AdminProduct.Size), 0),
            ("가격", nameof(AdminProduct.PriceText), 0),
            ("재고", nameof(AdminProduct.Stock), 0),
            ("재고 상태", nameof(AdminProduct.StockStatus), 0),
            ("상태", nameof(AdminProduct.ActiveText), 0));
        _productTable.DoubleTapped += (_, _) => Ui.Fire(this, EditProductAsync);

        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = "카테고리 관리", Content = BuildTab(
            _categoryTable, LoadCategoriesAsync, AddCategoryAsync, EditCategoryAsync) });
        tabs.Items.Add(new TabItem { Header = "상품 관리", Content = BuildTab(
            _productTable, LoadProductsAsync, AddProductAsync, EditProductAsync) });

        Content = new Border { Padding = new Thickness(12), Child = Ui.Dock(Ui.Title("재고관리"), tabs) };
    }

    private static Control BuildTab(DataGrid table, Func<Task> refresh, Func<Task> add, Func<Task> edit)
    {
        var left = Ui.Btn("새로고침", refresh);
        var right = Ui.HStack(8, Ui.Btn("추가", add), Ui.Btn("수정", edit));

        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(0, 8) };
        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 2);
        bar.Children.Add(left);
        bar.Children.Add(right);

        return Ui.Dock(bar, table);
    }

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
            await Dialogs.ErrorAsync(this, result.Message, "카테고리 조회 실패");
            return;
        }

        _categories = result.Value;
        _categoryTable.ItemsSource = _categories;
    }

    private async Task AddCategoryAsync()
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        var dialog = new CategoryWindow(null);
        if (!await dialog.ShowDialog<bool>(owner)) return;

        var result = await _api.CategoryAddAsync(dialog.CategoryName);
        if (result.Ok)
        {
            await Dialogs.InfoAsync(this, result.Message, "카테고리 추가");
            await LoadCategoriesAsync();
        }
        else
        {
            await Dialogs.ErrorAsync(this, result.Message, "카테고리 추가 실패");
        }
    }

    private async Task EditCategoryAsync()
    {
        if (_categoryTable.SelectedItem is not AdminCategory selected)
        {
            await Dialogs.InfoAsync(this, "수정할 카테고리를 선택하세요.");
            return;
        }
        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        var dialog = new CategoryWindow(selected);
        if (!await dialog.ShowDialog<bool>(owner)) return;

        var result = await _api.CategoryUpdateAsync(selected.CategoryId, dialog.CategoryName, dialog.IsActive);
        if (result.Ok)
        {
            await Dialogs.InfoAsync(this, result.Message, "카테고리 수정");
            await LoadCategoriesAsync();
        }
        else
        {
            await Dialogs.ErrorAsync(this, result.Message, "카테고리 수정 실패");
        }
    }

    // ------------------------------------------------------------ 상품

    private async Task LoadProductsAsync()
    {
        var result = await _api.ProductListAsync();
        if (!result.Ok)
        {
            await Dialogs.ErrorAsync(this, result.Message, "상품 조회 실패");
            return;
        }

        _productTable.ItemsSource = result.Value;
    }

    private async Task AddProductAsync()
    {
        var active = _categories.Where(c => c.IsActive).ToList();
        if (active.Count == 0)
        {
            await Dialogs.InfoAsync(this, "사용할 수 있는 카테고리가 없습니다. 먼저 카테고리를 추가하세요.");
            return;
        }
        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        var dialog = new ProductWindow(active, null);
        if (!await dialog.ShowDialog<bool>(owner)) return;

        var result = await _api.ProductAddAsync(dialog.Result);
        if (result.Ok)
        {
            await Dialogs.InfoAsync(this, result.Message, "상품 추가");
            await LoadProductsAsync();
        }
        else
        {
            await Dialogs.ErrorAsync(this, result.Message, "상품 추가 실패");
        }
    }

    private async Task EditProductAsync()
    {
        if (_productTable.SelectedItem is not AdminProduct selected)
        {
            await Dialogs.InfoAsync(this, "수정할 상품을 선택하세요.");
            return;
        }
        if (!selected.IsActive)
        {
            await Dialogs.InfoAsync(this, "이미 수정되어 비활성화된 이전 버전입니다.\n'판매중' 상태의 최신 상품을 수정하세요.");
            return;
        }

        var active = _categories.Where(c => c.IsActive).ToList();
        if (active.Count == 0)
        {
            await Dialogs.InfoAsync(this, "사용할 수 있는 카테고리가 없습니다.");
            return;
        }
        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        var dialog = new ProductWindow(active, selected);
        if (!await dialog.ShowDialog<bool>(owner)) return;

        var result = await _api.ProductUpdateAsync(selected.ProductId, dialog.Result);
        if (result.Ok)
        {
            await Dialogs.InfoAsync(this, result.Message, "상품 수정");
            await LoadProductsAsync();
        }
        else
        {
            await Dialogs.ErrorAsync(this, result.Message, "상품 수정 실패");
        }
    }
}
