namespace ShoppingMall.Client.Views;

/// <summary>
/// 로그인 화면. 아이디·비밀번호를 받아 쇼핑몰 서버에 로그인하고,
/// 성공하면 LoginSucceeded 이벤트로 MainWindow 에 알린다. (화면 전환은 MainWindow 가 한다)
/// </summary>
public partial class LoginView : UserControl
{
    private readonly ShopApi _api;

    /// <summary>로그인 성공: 회원 정보와, 관리자 서버 접속에 다시 쓸 비밀번호.</summary>
    public event Action<Member, string>? LoginSucceeded;

    public LoginView(ShopApi api)
    {
        InitializeComponent();
        _api = api;
        Loaded += (_, _) => IdBox.Focus(); // 화면이 뜨면 아이디 칸에 바로 입력할 수 있게
    }

    // XAML 의 Click="..." 과 연결된 이벤트 처리기. async void 라서 예외가 새지 않도록 Ui.Guard 로 감싼다.
    private async void Login_Click(object sender, RoutedEventArgs e) => await Ui.Guard(this, LoginAsync);

    private void Signup_Click(object sender, RoutedEventArgs e) => Signup();

    // 비밀번호 칸에서 Enter 를 누르면 로그인 버튼을 누른 것과 같게 한다.
    private async void PwBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            await Ui.Guard(this, LoginAsync);
    }

    /// <summary>입력값 확인 → 서버 로그인 → 성공 시 이벤트 발생</summary>
    private async Task LoginAsync()
    {
        string loginId = (IdBox.Text ?? "").Trim();
        string password = PwBox.Password; // PasswordBox 는 Text 대신 Password 로 읽는다

        if (loginId.Length == 0 || password.Length == 0)
        {
            Dialogs.Info(this, "아이디와 비밀번호를 입력하세요.");
            return;
        }

        var result = await _api.LoginAsync(loginId, password);
        if (!result.Ok)
        {
            Dialogs.Error(this, result.Message);
            return;
        }

        PwBox.Clear(); // 다음에 로그인 화면으로 돌아왔을 때 비밀번호가 남아 있지 않게
        LoginSucceeded?.Invoke(result.Value, password);
    }

    /// <summary>회원가입 창을 띄우고, 가입에 성공하면 아이디 칸을 채워 둔다.</summary>
    private void Signup()
    {
        var dialog = new SignupWindow(_api);
        if (!Dialogs.ShowDialog(dialog, this)) return; // 창이 닫힐 때까지 기다림 (가입 성공이면 true)

        Dialogs.Info(this, "회원가입이 완료되었습니다. 로그인해주세요.");
        IdBox.Text = dialog.LoginId;
        PwBox.Focus();
    }
}
