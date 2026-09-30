namespace ShoppingMall.Client.Admin;

/// <summary>
/// 상품 추가/수정 대화상자. 확인을 누르면 Close(true) 로 닫히고, 부른 쪽이 Result 를 읽어 서버에 요청한다.
/// 수정 모드에서는 기존 값(재고 포함)을 채워 두므로 바꾸지 않은 항목은 그대로 저장된다.
/// </summary>
public sealed class ProductWindow : Window
{
    private readonly ComboBox _category = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox _name = Ui.Input();
    private readonly TextBox _description = Ui.MultiLine(70);
    private readonly TextBox _color = Ui.Input();
    private readonly TextBox _size = Ui.Input();
    // 숫자 입력칸. 범위를 벗어난 값은 입력할 수 없고, 화살표로 가격은 1000원, 재고는 1개씩 바뀐다.
    private readonly NumericUpDown _price = new() { Minimum = 0, Maximum = 100_000_000, Increment = 1000, Value = 0, FormatString = "N0" };
    private readonly NumericUpDown _stock = new() { Minimum = 0, Maximum = 1_000_000, Increment = 1, Value = 0, FormatString = "N0" };
    private readonly List<AdminCategory> _categories;

    /// <param name="categories">선택할 수 있는 카테고리(활성 카테고리만 넘긴다).</param>
    /// <param name="product">null 이면 추가 모드, 있으면 수정 모드.</param>
    public ProductWindow(List<AdminCategory> categories, AdminProduct? product)
    {
        _categories = categories;

        Title = product is null ? "상품 추가" : "상품 수정";
        Width = 400;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _category.ItemsSource = categories;
        _category.SelectedIndex = 0; // 기본은 첫 번째 카테고리

        if (product is not null)
        {
            // 상품의 카테고리를 목록에서 찾아 선택 (비활성 카테고리라 목록에 없으면 첫 번째 그대로)
            int index = categories.FindIndex(c => c.CategoryId == product.CategoryId);
            if (index >= 0) _category.SelectedIndex = index;

            _name.Text = product.Name;
            _description.Text = product.Description;
            _color.Text = product.Color;
            _size.Text = product.Size;
            _price.Value = product.Price;
            _stock.Value = product.Stock;
        }

        var form = Ui.Form(
            ("카테고리", _category),
            ("상품명", _name),
            ("설명", _description),
            ("색상", _color),
            ("사이즈", _size),
            ("가격(원)", _price),
            ("재고수량", _stock));

        var ok = Ui.Btn("확인", OkAsync);
        var cancel = Ui.Btn("취소", () => { Close(false); return Task.CompletedTask; });
        var buttons = Ui.HStack(8, ok, cancel);
        buttons.HorizontalAlignment = HorizontalAlignment.Right;

        Content = new StackPanel { Margin = new Thickness(20), Spacing = 12, Children = { form, buttons } };
    }

    /// <summary>입력한 상품 정보. 확인을 누른 뒤에 읽는다.</summary>
    public ProductInput Result => new(
        (_category.SelectedItem as AdminCategory)?.CategoryId ?? 0,
        (_name.Text ?? "").Trim(),
        (_description.Text ?? "").Trim(),
        (_color.Text ?? "").Trim(),
        (_size.Text ?? "").Trim(),
        (long)(_price.Value ?? 0),
        (long)(_stock.Value ?? 0));

    /// <summary>필수 항목(카테고리, 상품명)을 확인하고 창을 닫는다.</summary>
    private async Task OkAsync()
    {
        if (_category.SelectedItem is null)
        {
            await Dialogs.InfoAsync(this, "카테고리를 선택하세요.", "입력 오류");
            return;
        }
        if (Result.Name.Length == 0)
        {
            await Dialogs.InfoAsync(this, "상품명을 입력하세요.", "입력 오류");
            return;
        }
        Close(true);
    }
}
