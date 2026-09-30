namespace ShoppingMall.Client.Views;

/// <summary>회원가입 대화상자 (gui/signup_view.py)</summary>
public sealed class SignupWindow : Window
{
    private static readonly string[] GenderLabels = { "선택 안 함", "남성", "여성" };
    private static readonly string[] GenderCodes = { "", "M", "F" };

    private readonly ShopApi _api;
    private readonly TextBox _loginId = Ui.Input("아이디");
    private readonly TextBox _password = Ui.PasswordInput();
    private readonly TextBox _passwordConfirm = Ui.PasswordInput();
    private readonly TextBox _name = Ui.Input();
    private readonly TextBox _address = Ui.Input();
    private readonly TextBox _email = Ui.Input();
    private readonly TextBox _phone = Ui.Input();
    private readonly ComboBox _gender = new() { ItemsSource = GenderLabels, SelectedIndex = 0 };
    private bool _idChecked;

    public string LoginId => (_loginId.Text ?? "").Trim();

    public SignupWindow(ShopApi api)
    {
        _api = api;

        Title = "회원가입";
        Width = 460;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        // 아이디를 바꾸면 중복확인을 다시 해야 한다.
        _loginId.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
                _idChecked = false;
        };

        var idRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        var check = Ui.Btn("중복확인", CheckIdAsync);
        Grid.SetColumn(_loginId, 0);
        Grid.SetColumn(check, 1);
        idRow.Children.Add(_loginId);
        idRow.Children.Add(check);

        _gender.HorizontalAlignment = HorizontalAlignment.Stretch;

        var form = Ui.Form(
            ("아이디", idRow),
            ("비밀번호", _password),
            ("비밀번호 확인", _passwordConfirm),
            ("이름", _name),
            ("주소", _address),
            ("이메일", _email),
            ("전화번호", _phone),
            ("성별", _gender));

        var cancel = Ui.Btn("취소", () => { Close(false); return Task.CompletedTask; });
        var buttons = Ui.HStack(8, Ui.Btn("가입하기", SignupAsync), cancel);
        buttons.HorizontalAlignment = HorizontalAlignment.Right;

        Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16,
            Children = { Ui.Text("[회원가입]", 18, true), form, buttons },
        };
    }

    private async Task CheckIdAsync()
    {
        if (LoginId.Length == 0)
        {
            await Dialogs.InfoAsync(this, "아이디를 입력하세요.");
            return;
        }

        var result = await _api.CheckIdAsync(LoginId);
        if (!result.Ok)
        {
            await Dialogs.ErrorAsync(this, result.Message);
            return;
        }

        _idChecked = result.Value;
        if (result.Value)
            await Dialogs.InfoAsync(this, "사용 가능한 아이디입니다.");
        else
            await Dialogs.InfoAsync(this, "이미 사용 중인 아이디입니다.");
    }

    private async Task SignupAsync()
    {
        string password = _password.Text ?? "";
        string name = (_name.Text ?? "").Trim();

        if (LoginId.Length == 0 || password.Length == 0 || name.Length == 0)
        {
            await Dialogs.InfoAsync(this, "아이디, 비밀번호, 이름은 필수입니다.");
            return;
        }
        if (!_idChecked)
        {
            await Dialogs.InfoAsync(this, "아이디 중복확인을 먼저 해주세요.");
            return;
        }
        if (password != (_passwordConfirm.Text ?? ""))
        {
            await Dialogs.InfoAsync(this, "비밀번호가 일치하지 않습니다.");
            return;
        }

        int genderIndex = Math.Max(0, _gender.SelectedIndex);
        var result = await _api.SignupAsync(
            LoginId, password, name,
            (_address.Text ?? "").Trim(), (_email.Text ?? "").Trim(), (_phone.Text ?? "").Trim(),
            GenderCodes[genderIndex]);

        if (result.Ok)
            Close(true);
        else
            await Dialogs.ErrorAsync(this, result.Message);
    }
}
