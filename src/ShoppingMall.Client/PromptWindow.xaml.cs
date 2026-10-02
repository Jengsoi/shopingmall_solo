namespace ShoppingMall.Client;

/// <summary>글자 입력 대화상자. Dialogs.Prompt 가 띄운다.</summary>
public partial class PromptWindow : Window
{
    public PromptWindow(string title, string label, string initial, bool multiLine)
    {
        InitializeComponent(); // XAML 에 정의한 컨트롤들을 만든다 (x:Name 붙인 것은 필드로 쓸 수 있게 됨)

        Title = title;
        LabelText.Text = label;
        Input.Text = initial;

        if (multiLine)
        {
            // 여러 줄 입력: Enter 로 줄바꿈 (이때는 Enter 로 확인되지 않는다)
            Input.AcceptsReturn = true;
            Input.TextWrapping = TextWrapping.Wrap;
            Input.Height = 120;
            Input.VerticalContentAlignment = VerticalAlignment.Top;
        }

        Loaded += (_, _) => Input.Focus();
    }

    /// <summary>입력한 글자 (확인으로 닫힌 경우에만 의미 있음)</summary>
    public string Value => Input.Text ?? "";

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true; // 창이 닫히고 ShowDialog() 가 true 를 돌려준다
}
