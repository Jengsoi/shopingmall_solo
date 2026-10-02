namespace ShoppingMall.Client.Admin;

/// <summary>
/// 카테고리 추가/수정 대화상자. 확인을 누르면 DialogResult = true 로 닫히고,
/// 부른 쪽(InventoryView)이 CategoryName·IsCategoryActive 를 읽어 서버에 요청한다.
/// </summary>
public partial class CategoryWindow : Window
{
    /// <param name="category">null 이면 추가 모드(항상 활성 상태로 생성), 있으면 수정 모드.</param>
    public CategoryWindow(AdminCategory? category)
    {
        InitializeComponent();
        Title = category is null ? "카테고리 추가" : "카테고리 수정";

        if (category is not null)
        {
            NameBox.Text = category.Name;
            ActiveBox.IsChecked = category.IsActive;
        }
        else
        {
            ActiveBox.IsEnabled = false; // 추가 시에는 항상 활성으로 생성된다.
        }

        Loaded += (_, _) => NameBox.Focus();
    }

    public string CategoryName => (NameBox.Text ?? "").Trim();

    // Window 에 이미 IsActive(창이 활성 상태인지) 속성이 있어서 이름을 다르게 지었다.
    public bool IsCategoryActive => ActiveBox.IsChecked == true;

    // 이름이 비어 있으면 창을 닫지 않고 안내만 한다.
    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (CategoryName.Length == 0)
        {
            Dialogs.Info(this, "카테고리명을 입력하세요.", "입력 오류");
            return;
        }
        DialogResult = true;
    }
}
