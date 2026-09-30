using Avalonia.Data;

namespace ShoppingMall.Client;

/// <summary>
/// 화면을 코드로 조립할 때 쓰는 작은 도우미들.
/// 이 프로젝트는 XAML 대신 C# 코드로 화면을 만든다. 같은 설정(글자 크기, 줄바꿈 등)을 매번 쓰지 않도록 여기 모아 두었다.
///   예) Ui.Dock(Ui.Title("[장바구니]"), 목록, 버튼줄)  →  위: 제목 / 가운데: 목록 / 아래: 버튼
/// </summary>
public static class Ui
{
    /// <summary>글자 (길면 자동 줄바꿈)</summary>
    public static TextBlock Text(string text, double size = 14, bool bold = false) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
        TextWrapping = TextWrapping.Wrap,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>화면 제목용 큰 굵은 글자</summary>
    public static TextBlock Title(string text) => Text(text, 18, true);

    /// <summary>한 줄 입력칸. watermark 는 비어 있을 때 흐리게 보이는 안내 문구.</summary>
    public static TextBox Input(string watermark = "") => new() { Watermark = watermark };

    /// <summary>비밀번호 입력칸 (입력한 글자를 ● 로 가림)</summary>
    public static TextBox PasswordInput(string watermark = "") => new() { Watermark = watermark, PasswordChar = '●' };

    /// <summary>여러 줄 입력칸 (게시글 본문 등). Enter 로 줄바꿈 가능.</summary>
    public static TextBox MultiLine(double height, string watermark = "") => new()
    {
        Watermark = watermark,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap,
        Height = height,
    };

    /// <summary>클릭 시 비동기 동작을 실행하는 버튼. 예외가 나도 앱이 죽지 않고 오류창을 띄운다.</summary>
    public static Button Btn(string text, Func<Task> onClick, double minWidth = 80)
    {
        var button = new Button { Content = text, MinWidth = minWidth, HorizontalContentAlignment = HorizontalAlignment.Center };
        button.Click += async (_, _) => await Guard(button, onClick);
        return button;
    }

    /// <summary>자식들을 가로로 나란히 놓는 패널</summary>
    public static StackPanel HStack(double spacing, params Control[] children)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = spacing };
        panel.Children.AddRange(children);
        return panel;
    }

    /// <summary>자식들을 세로로 쌓는 패널</summary>
    public static StackPanel VStack(double spacing, params Control[] children)
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = spacing };
        panel.Children.AddRange(children);
        return panel;
    }

    /// <summary>
    /// 왼쪽 라벨 + 오른쪽 입력칸 형태의 폼.
    /// 2열 Grid: 첫 열("Auto")은 가장 긴 라벨 폭에 맞추고, 둘째 열("*")은 남은 폭을 모두 입력칸에 준다.
    /// </summary>
    public static Grid Form(params (string Label, Control Input)[] rows)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), RowSpacing = 8, ColumnSpacing = 12 };
        for (int i = 0; i < rows.Length; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            var label = Text(rows[i].Label);
            Grid.SetRow(label, i);
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            var input = rows[i].Input;
            Grid.SetRow(input, i);
            Grid.SetColumn(input, 1);
            grid.Children.Add(input);
        }
        return grid;
    }

    /// <summary>
    /// 위쪽에 Top 을, 아래쪽에 Bottom 을 고정하고 나머지 공간을 Center 가 채우는 배치.
    /// 창 크기가 바뀌면 가운데(보통 목록·표)만 늘어나거나 줄어든다.
    /// DockPanel 은 마지막에 추가한 자식이 남은 공간을 채우므로(LastChildFill) center 를 맨 나중에 넣는다.
    /// </summary>
    public static DockPanel Dock(Control? top, Control center, Control? bottom = null)
    {
        var dock = new DockPanel { LastChildFill = true };
        if (top is not null)
        {
            DockPanel.SetDock(top, Avalonia.Controls.Dock.Top);
            dock.Children.Add(top);
        }
        if (bottom is not null)
        {
            DockPanel.SetDock(bottom, Avalonia.Controls.Dock.Bottom);
            dock.Children.Add(bottom);
        }
        dock.Children.Add(center);
        return dock;
    }

    /// <summary>
    /// 읽기 전용 표(DataGrid). 열마다 (헤더 글자, 표시할 속성 이름, 폭 비율)을 준다.
    ///   폭 비율 &gt; 0 : 남은 폭을 비율대로 나눠 가진다 (예: 3 과 1 이면 3:1)
    ///   폭 비율 = 0 : 내용 폭에 맞춘다 (Auto)
    /// 표에 ItemsSource 로 모델 목록을 넣으면, 각 열이 모델의 해당 속성 값을 보여준다.
    /// </summary>
    public static DataGrid Table(params (string Header, string Property, double Star)[] columns)
    {
        var grid = new DataGrid
        {
            IsReadOnly = true,
            AutoGenerateColumns = false,
            SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            CanUserResizeColumns = true,
        };
        foreach (var (header, property, star) in columns)
        {
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(property), // 행 모델의 이 속성 값을 칸에 표시
                Width = star > 0
                    ? new DataGridLength(star, DataGridLengthUnitType.Star)
                    : DataGridLength.Auto,
                // 비율 폭만 주면 좁은 열의 헤더가 잘리므로, 헤더 글자 수만큼은 항상 보이게 한다. (Auto 열은 헤더에 자동으로 맞춰짐)
                MinWidth = star > 0 ? header.Length * 14 + 48 : 0,
            });
        }
        return grid;
    }

    /// <summary>
    /// 비동기 동작을 실행하고, 예외는 오류창으로 보여준다.
    /// 버튼 클릭 같은 이벤트에서 예외가 밖으로 나가면 앱 전체가 종료될 수 있어서 여기서 막는다.
    /// </summary>
    public static async Task Guard(Control owner, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            await Dialogs.ErrorAsync(owner, "예상하지 못한 오류가 발생했습니다.\n" + ex.Message);
        }
    }

    /// <summary>
    /// 결과를 기다리지 않고 실행한다(화면이 열릴 때 데이터를 불러오는 용도).
    /// 생성자나 일반 이벤트 핸들러처럼 await 를 쓸 수 없는 곳에서 비동기 작업을 시작할 때 쓴다.
    /// </summary>
    public static void Fire(Control owner, Func<Task> action) => _ = Guard(owner, action);
}

/// <summary>
/// Avalonia 에는 기본 메시지박스가 없어서 직접 만든 알림/확인/입력 대화상자.
///   await Dialogs.InfoAsync(this, "저장했습니다.");
///   if (await Dialogs.ConfirmAsync(this, "삭제할까요?")) { ... }
/// 대화상자는 모달(ShowDialog)이라, 닫을 때까지 뒤의 창을 조작할 수 없다.
/// </summary>
public static class Dialogs
{
    /// <summary>이 컨트롤이 들어 있는 창 (대화상자를 그 창 가운데에 띄우기 위해)</summary>
    private static Window? OwnerOf(Control control) => TopLevel.GetTopLevel(control) as Window;

    public static Task InfoAsync(Control owner, string message, string title = "안내") =>
        ShowAsync(owner, title, message, confirm: false);

    public static Task ErrorAsync(Control owner, string message, string title = "오류") =>
        ShowAsync(owner, title, message, confirm: false);

    /// <summary>예/아니오 질문. "예" 를 누르면 true.</summary>
    public static Task<bool> ConfirmAsync(Control owner, string message, string title = "확인") =>
        ShowAsync(owner, title, message, confirm: true);

    private static async Task<bool> ShowAsync(Control owner, string title, string message, bool confirm)
    {
        var parent = OwnerOf(owner);
        if (parent is null)
        {
            // 아직 창에 붙지 않은 화면에서 호출된 경우: 대화상자를 띄울 수 없으니 콘솔에만 남긴다.
            Console.Error.WriteLine($"[{title}] {message}");
            return false;
        }

        var dialog = new MessageWindow(title, message, confirm);
        return await dialog.ShowDialog<bool>(parent);
    }

    /// <summary>글자를 입력받는다. 취소하면 null.</summary>
    public static async Task<string?> PromptAsync(Control owner, string title, string label, string initial = "", bool multiLine = false)
    {
        var parent = OwnerOf(owner);
        if (parent is null) return null;

        var dialog = new PromptWindow(title, label, initial, multiLine);
        return await dialog.ShowDialog<string?>(parent);
    }
}

/// <summary>알림/확인 대화상자 창. 누른 버튼에 따라 Close(true/false) 로 결과를 돌려준다.</summary>
internal sealed class MessageWindow : Window
{
    public MessageWindow(string title, string message, bool confirm)
    {
        Title = title;
        SizeToContent = SizeToContent.WidthAndHeight; // 문구 길이에 맞춰 창 크기를 정한다
        MinWidth = 320;
        MaxWidth = 560;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var ok = new Button { Content = confirm ? "예" : "확인", MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
        ok.Click += (_, _) => Close(true);

        var buttons = Ui.HStack(8, ok);
        buttons.HorizontalAlignment = HorizontalAlignment.Right;

        if (confirm)
        {
            var no = new Button { Content = "아니오", MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
            no.Click += (_, _) => Close(false);
            buttons.Children.Add(no);
        }

        var text = Ui.Text(message);
        text.MaxWidth = 500;

        Content = new StackPanel { Margin = new Thickness(20), Spacing = 16, Children = { text, buttons } };
    }
}

/// <summary>글자 입력 대화상자 창. 확인이면 입력한 글자, 취소면 null 을 돌려준다.</summary>
internal sealed class PromptWindow : Window
{
    public PromptWindow(string title, string label, string initial, bool multiLine)
    {
        Title = title;
        Width = 420;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var input = multiLine ? Ui.MultiLine(120) : Ui.Input();
        input.Text = initial;

        var ok = new Button { Content = "확인", MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
        ok.Click += (_, _) => Close(input.Text ?? "");
        var cancel = new Button { Content = "취소", MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
        cancel.Click += (_, _) => Close(null);

        var buttons = Ui.HStack(8, ok, cancel);
        buttons.HorizontalAlignment = HorizontalAlignment.Right;

        Content = new StackPanel { Margin = new Thickness(20), Spacing = 12, Children = { Ui.Text(label), input, buttons } };
    }
}
