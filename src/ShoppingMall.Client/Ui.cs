using Avalonia.Data;

namespace ShoppingMall.Client;

/// <summary>화면을 코드로 조립할 때 쓰는 작은 도우미들.</summary>
public static class Ui
{
    public static TextBlock Text(string text, double size = 14, bool bold = false) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
        TextWrapping = TextWrapping.Wrap,
        VerticalAlignment = VerticalAlignment.Center,
    };

    public static TextBlock Title(string text) => Text(text, 18, true);

    public static TextBox Input(string watermark = "") => new() { Watermark = watermark };

    public static TextBox PasswordInput(string watermark = "") => new() { Watermark = watermark, PasswordChar = '●' };

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

    public static StackPanel HStack(double spacing, params Control[] children)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = spacing };
        panel.Children.AddRange(children);
        return panel;
    }

    public static StackPanel VStack(double spacing, params Control[] children)
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = spacing };
        panel.Children.AddRange(children);
        return panel;
    }

    /// <summary>왼쪽 라벨 + 오른쪽 입력칸 형태의 폼.</summary>
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

    /// <summary>위쪽에 Top 을 고정하고 나머지 공간을 Center 가 채우는 배치.</summary>
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
                Binding = new Binding(property),
                Width = star > 0
                    ? new DataGridLength(star, DataGridLengthUnitType.Star)
                    : DataGridLength.Auto,
                // 비율 폭만 주면 좁은 열의 헤더가 잘리므로, 헤더 글자 수만큼은 항상 보이게 한다. (Auto 열은 헤더에 자동으로 맞춰짐)
                MinWidth = star > 0 ? header.Length * 14 + 48 : 0,
            });
        }
        return grid;
    }

    /// <summary>비동기 동작을 실행하고, 예외는 오류창으로 보여준다.</summary>
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

    /// <summary>결과를 기다리지 않고 실행한다(화면이 열릴 때 데이터를 불러오는 용도).</summary>
    public static void Fire(Control owner, Func<Task> action) => _ = Guard(owner, action);
}

/// <summary>Avalonia 에는 기본 메시지박스가 없어서 직접 만든 알림/확인/입력 대화상자.</summary>
public static class Dialogs
{
    private static Window? OwnerOf(Control control) => TopLevel.GetTopLevel(control) as Window;

    public static Task InfoAsync(Control owner, string message, string title = "안내") =>
        ShowAsync(owner, title, message, confirm: false);

    public static Task ErrorAsync(Control owner, string message, string title = "오류") =>
        ShowAsync(owner, title, message, confirm: false);

    public static Task<bool> ConfirmAsync(Control owner, string message, string title = "확인") =>
        ShowAsync(owner, title, message, confirm: true);

    private static async Task<bool> ShowAsync(Control owner, string title, string message, bool confirm)
    {
        var parent = OwnerOf(owner);
        if (parent is null)
        {
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

internal sealed class MessageWindow : Window
{
    public MessageWindow(string title, string message, bool confirm)
    {
        Title = title;
        SizeToContent = SizeToContent.WidthAndHeight;
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
