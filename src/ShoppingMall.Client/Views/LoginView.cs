namespace ShoppingMall.Client.Views;

/// <summary>
/// 로그인 화면. 아이디·비밀번호를 받아 쇼핑몰 서버에 로그인하고,
/// 성공하면 LoginSucceeded 이벤트로 MainWindow 에 알린다. (화면 전환은 MainWindow 가 한다)
/// </summary>
public sealed class LoginView : UserControl
{
    private readonly ShopApi _api;
    private readonly TextBox _id = Ui.Input("아이디");
    private readonly TextBox _pw = Ui.PasswordInput("비밀번호");

    /// <summary>로그인 성공: 회원 정보와, 관리자 서버 접속에 다시 쓸 비밀번호.</summary>
    public event Action<Member, string>? LoginSucceeded;

    public LoginView(ShopApi api)
    {
        _api = api;

        // 비밀번호 칸에서 Enter 를 누르면 로그인 버튼을 누른 것과 같게 한다.
        _pw.KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter)
                await Ui.Guard(this, LoginAsync);
        };

        var form = Ui.Form(("아이디", _id), ("비밀번호", _pw));
        var buttons = Ui.HStack(8, Ui.Btn("로그인", LoginAsync), Ui.Btn("회원가입", SignupAsync));

        // 폭 360 짜리 세로 묶음을 창 한가운데에 놓는다.
        Content = new StackPanel
        {
            Spacing = 16,
            Width = 360,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { Ui.Text("[로그인]", 22, true), form, buttons },
        };
    }

    /// <summary>입력값 확인 → 서버 로그인 → 성공 시 이벤트 발생</summary>
    private async Task LoginAsync()
    {
        string loginId = (_id.Text ?? "").Trim();
        string password = _pw.Text ?? "";

        if (loginId.Length == 0 || password.Length == 0)
        {
            await Dialogs.InfoAsync(this, "아이디와 비밀번호를 입력하세요.");
            return;
        }

        var result = await _api.LoginAsync(loginId, password);
        if (!result.Ok)
        {
            await Dialogs.ErrorAsync(this, result.Message);
            return;
        }

        _pw.Text = ""; // 다음에 로그인 화면으로 돌아왔을 때 비밀번호가 남아 있지 않게
        LoginSucceeded?.Invoke(result.Value, password);
    }

    /// <summary>회원가입 창을 띄우고, 가입에 성공하면 아이디 칸을 채워 둔다.</summary>
    private async Task SignupAsync()
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        var dialog = new SignupWindow(_api);
        bool signedUp = await dialog.ShowDialog<bool>(owner); // 창이 닫힐 때까지 기다림 (가입 성공이면 true)
        if (!signedUp) return;

        await Dialogs.InfoAsync(this, "회원가입이 완료되었습니다. 로그인해주세요.");
        _id.Text = dialog.LoginId;
        _pw.Focus();
    }
}
