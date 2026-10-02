namespace ShoppingMall.Client.Admin;

/// <summary>
/// 상품 추가/수정 대화상자. 확인을 누르면 DialogResult = true 로 닫히고, 부른 쪽이 Result 를 읽어 서버에 요청한다.
/// 수정 모드에서는 기존 값(재고 포함)을 채워 두므로 바꾸지 않은 항목은 그대로 저장된다.
/// </summary>
public partial class ProductWindow : Window
{
    private const long MaxPrice = 100_000_000;
    private const long MaxStock = 1_000_000;

    /// <param name="categories">선택할 수 있는 카테고리(활성 카테고리만 넘긴다).</param>
    /// <param name="product">null 이면 추가 모드, 있으면 수정 모드.</param>
    public ProductWindow(List<AdminCategory> categories, AdminProduct? product)
    {
        InitializeComponent();
        Title = product is null ? "상품 추가" : "상품 수정";

        CategoryBox.ItemsSource = categories;
        CategoryBox.SelectedIndex = 0; // 기본은 첫 번째 카테고리

        if (product is not null)
        {
            // 상품의 카테고리를 목록에서 찾아 선택 (비활성 카테고리라 목록에 없으면 첫 번째 그대로)
            int index = categories.FindIndex(c => c.CategoryId == product.CategoryId);
            if (index >= 0) CategoryBox.SelectedIndex = index;

            NameBox.Text = product.Name;
            DescriptionBox.Text = product.Description;
            ColorBox.Text = product.Color;
            SizeBox.Text = product.Size;
            PriceBox.Text = product.Price.ToString();
            StockBox.Text = product.Stock.ToString();
        }

        Loaded += (_, _) => NameBox.Focus();
    }

    /// <summary>입력한 상품 정보. 확인으로 닫힌 뒤에 읽는다. (가격·재고는 Ok_Click 에서 이미 검사함)</summary>
    public ProductInput Result => new(
        (CategoryBox.SelectedItem as AdminCategory)?.CategoryId ?? 0,
        (NameBox.Text ?? "").Trim(),
        (DescriptionBox.Text ?? "").Trim(),
        (ColorBox.Text ?? "").Trim(),
        (SizeBox.Text ?? "").Trim(),
        Ui.ReadLong(PriceBox, 0, MaxPrice) ?? 0,
        Ui.ReadLong(StockBox, 0, MaxStock) ?? 0);

    /// <summary>필수 항목과 숫자 칸을 확인하고 창을 닫는다.</summary>
    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (CategoryBox.SelectedItem is null)
        {
            Dialogs.Info(this, "카테고리를 선택하세요.", "입력 오류");
            return;
        }
        if ((NameBox.Text ?? "").Trim().Length == 0)
        {
            Dialogs.Info(this, "상품명을 입력하세요.", "입력 오류");
            return;
        }
        if (Ui.ReadLong(PriceBox, 0, MaxPrice) is null)
        {
            Dialogs.Info(this, $"가격은 0 ~ {MaxPrice:N0} 사이의 숫자로 입력하세요.", "입력 오류");
            return;
        }
        if (Ui.ReadLong(StockBox, 0, MaxStock) is null)
        {
            Dialogs.Info(this, $"재고수량은 0 ~ {MaxStock:N0} 사이의 숫자로 입력하세요.", "입력 오류");
            return;
        }
        DialogResult = true;
    }
}
