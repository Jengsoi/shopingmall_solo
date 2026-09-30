namespace ShoppingMall.Client.Views;

/// <summary>로그인 화면 (gui/login_view.py)</summary>
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

        _pw.KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter)
                await Ui.Guard(this, LoginAsync);
        };

        var form = Ui.Form(("아이디", _id), ("비밀번호", _pw));
        var buttons = Ui.HStack(8, Ui.Btn("로그인", LoginAsync), Ui.Btn("회원가입", SignupAsync));

        Content = new StackPanel
        {
            Spacing = 16,
            Width = 360,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { Ui.Text("[로그인]", 22, true), form, buttons },
        };
    }

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

        _pw.Text = "";
        LoginSucceeded?.Invoke(result.Value, password);
    }

    private async Task SignupAsync()
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        var dialog = new SignupWindow(_api);
        bool signedUp = await dialog.ShowDialog<bool>(owner);
        if (!signedUp) return;

        await Dialogs.InfoAsync(this, "회원가입이 완료되었습니다. 로그인해주세요.");
        _id.Text = dialog.LoginId;
        _pw.Focus();
    }
}
