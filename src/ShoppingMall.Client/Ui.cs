namespace ShoppingMall.Client;

/// <summary>화면 코드에서 공통으로 쓰는 도우미.</summary>
public static class Ui
{
    /// <summary>
    /// 비동기 동작을 실행하고, 예외는 오류창으로 보여준다.
    /// 버튼 클릭 같은 이벤트에서 예외가 밖으로 나가면 앱 전체가 종료될 수 있어서 여기서 막는다.
    ///   사용 예) private async void Save_Click(object s, RoutedEventArgs e) => await Ui.Guard(this, SaveAsync);
    /// </summary>
    public static async Task Guard(DependencyObject owner, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Dialogs.Error(owner, "예상하지 못한 오류가 발생했습니다.\n" + ex.Message);
        }
    }

    /// <summary>
    /// 결과를 기다리지 않고 실행한다(화면이 열릴 때 데이터를 불러오는 용도).
    /// 생성자나 일반 이벤트처럼 await 를 쓸 수 없는 곳에서 비동기 작업을 시작할 때 쓴다.
    /// </summary>
    public static async void Fire(DependencyObject owner, Func<Task> action) => await Guard(owner, action);

    /// <summary>
    /// 정수 입력칸의 값을 읽는다. 비어 있거나 숫자가 아니거나 범위를 벗어나면 null.
    /// (WPF 에는 숫자 전용 입력칸이 없어서 TextBox 를 쓰고 여기서 검사한다. "15,000" 처럼 쉼표가 있어도 된다)
    /// </summary>
    public static long? ReadLong(TextBox box, long min, long max)
    {
        string text = (box.Text ?? "").Replace(",", "").Trim();
        return long.TryParse(text, out long value) && value >= min && value <= max ? value : null;
    }
}

/// <summary>
/// 알림/확인/입력 대화상자.
///   Dialogs.Info(this, "저장했습니다.");
///   if (Dialogs.Confirm(this, "삭제할까요?")) { ... }
/// 알림·확인은 WPF 기본 MessageBox 를, 글자 입력은 직접 만든 PromptWindow 를 쓴다.
/// 모두 모달이라 닫을 때까지 뒤의 창을 조작할 수 없다.
/// </summary>
public static class Dialogs
{
    /// <summary>이 컨트롤이 들어 있는 창. 대화상자를 그 창 위(가운데)에 띄우기 위해 쓴다.</summary>
    public static Window? OwnerOf(DependencyObject control) => control as Window ?? Window.GetWindow(control);

    public static void Info(DependencyObject owner, string message, string title = "안내") =>
        Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public static void Error(DependencyObject owner, string message, string title = "오류") =>
        Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    /// <summary>예/아니오 질문. "예" 를 누르면 true.</summary>
    public static bool Confirm(DependencyObject owner, string message, string title = "확인") =>
        Show(owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    /// <summary>글자를 입력받는다. 취소하면 null.</summary>
    public static string? Prompt(DependencyObject owner, string title, string label, string initial = "", bool multiLine = false)
    {
        var dialog = new PromptWindow(title, label, initial, multiLine);
        return ShowDialog(dialog, owner) ? dialog.Value : null;
    }

    /// <summary>
    /// 대화상자 창을 owner 창 위에 모달로 띄우고, 확인(DialogResult = true)으로 닫혔는지 돌려준다.
    /// WPF 의 ShowDialog() 는 창이 닫힐 때까지 기다렸다가 결과를 돌려준다.
    /// </summary>
    public static bool ShowDialog(Window dialog, DependencyObject owner)
    {
        dialog.Owner = OwnerOf(owner);
        return dialog.ShowDialog() == true;
    }

    private static MessageBoxResult Show(DependencyObject owner, string message, string title,
        MessageBoxButton buttons, MessageBoxImage icon)
    {
        Window? window = OwnerOf(owner);
        return window is null
            ? MessageBox.Show(message, title, buttons, icon)
            : MessageBox.Show(window, message, title, buttons, icon);
    }
}
