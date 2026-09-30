namespace ShoppingMall.Client.Admin;

/// <summary>카테고리 추가/수정 대화상자 (admin/view/inventory_view.py 의 CategoryDialog)</summary>
public sealed class CategoryWindow : Window
{
    private readonly TextBox _name = Ui.Input();
    private readonly CheckBox _active = new() { Content = "활성", IsChecked = true };

    public string CategoryName => (_name.Text ?? "").Trim();
    public bool IsActive => _active.IsChecked == true;

    /// <param name="category">null 이면 추가 모드(항상 활성 상태로 생성), 있으면 수정 모드.</param>
    public CategoryWindow(AdminCategory? category)
    {
        Title = category is null ? "카테고리 추가" : "카테고리 수정";
        Width = 340;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        if (category is not null)
        {
            _name.Text = category.Name;
            _active.IsChecked = category.IsActive;
        }
        else
        {
            _active.IsEnabled = false; // 추가 시에는 항상 활성으로 생성된다.
        }

        var ok = Ui.Btn("확인", () =>
        {
            if (CategoryName.Length == 0)
                return Dialogs.InfoAsync(this, "카테고리명을 입력하세요.", "입력 오류");
            Close(true);
            return Task.CompletedTask;
        });
        var cancel = Ui.Btn("취소", () => { Close(false); return Task.CompletedTask; });
        var buttons = Ui.HStack(8, ok, cancel);
        buttons.HorizontalAlignment = HorizontalAlignment.Right;

        Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 12,
            Children = { Ui.Form(("카테고리명", _name), ("", _active)), buttons },
        };
    }
}
