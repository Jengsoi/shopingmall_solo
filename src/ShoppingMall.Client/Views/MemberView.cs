namespace ShoppingMall.Client.Views;

/// <summary>내 정보: 회원정보 수정 / 회원탈퇴 / 로그아웃 (gui/member_view.py)</summary>
public sealed class MemberView : UserControl
{
    private static readonly string[] GenderLabels = { "선택 안 함", "남성", "여성" };
    private static readonly string[] GenderCodes = { "", "M", "F" };

    private readonly ShopApi _api;
    private readonly TextBlock _greeting = Ui.Text("", 16, true);
    private readonly TextBlock _loginId = Ui.Text("");
    private readonly TextBox _name = Ui.Input();
    private readonly TextBox _address = Ui.Input();
    private readonly TextBox _email = Ui.Input();
    private readonly TextBox _phone = Ui.Input();
    private readonly ComboBox _gender = new() { ItemsSource = GenderLabels, SelectedIndex = 0 };

    public event Action? LogoutRequested;

    public MemberView(ShopApi api)
    {
        _api = api;
        _gender.HorizontalAlignment = HorizontalAlignment.Stretch;

        var form = Ui.Form(
            ("아이디", _loginId),
            ("이름", _name),
            ("주소", _address),
            ("이메일", _email),
            ("전화번호", _phone),
            ("성별", _gender));

        var buttons = Ui.HStack(8,
            Ui.Btn("정보수정", UpdateAsync),
            Ui.Btn("회원탈퇴", WithdrawAsync),
            Ui.Btn("로그아웃", LogoutAsync));

        Content = new StackPanel
        {
            Margin = new Thickness(12),
            Spacing = 16,
            Children = { _greeting, form, buttons },
        };
    }

    public async Task LoadInfoAsync()
    {
        var result = await _api.MemberInfoAsync();
        if (!result.Ok)
        {
            await Dialogs.ErrorAsync(this, result.Message);
            return;
        }

        var info = result.Value;
        _greeting.Text = $"{info.Name}님 환영합니다.";
        _loginId.Text = info.LoginId;
        _name.Text = info.Name;
        _address.Text = info.Address;
        _email.Text = info.Email;
        _phone.Text = info.Phone;

        int index = Array.IndexOf(GenderCodes, info.Gender);
        _gender.SelectedIndex = index >= 0 ? index : 0;
    }

    private async Task UpdateAsync()
    {
        string name = (_name.Text ?? "").Trim();
        if (name.Length == 0)
        {
            await Dialogs.InfoAsync(this, "이름을 입력하세요.");
            return;
        }

        var result = await _api.MemberUpdateAsync(
            name, (_address.Text ?? "").Trim(), (_email.Text ?? "").Trim(), (_phone.Text ?? "").Trim(),
            GenderCodes[Math.Max(0, _gender.SelectedIndex)]);

        if (result.Ok)
        {
            await Dialogs.InfoAsync(this, "회원정보가 수정되었습니다.");
            await LoadInfoAsync();
        }
        else
        {
            await Dialogs.ErrorAsync(this, result.Message);
        }
    }

    private async Task WithdrawAsync()
    {
        if (!await Dialogs.ConfirmAsync(this, "정말 탈퇴하시겠습니까?", "회원탈퇴"))
            return;

        var result = await _api.MemberWithdrawAsync();
        if (result.Ok)
        {
            await Dialogs.InfoAsync(this, "회원탈퇴가 완료되었습니다.");
            LogoutRequested?.Invoke();
        }
        else
        {
            await Dialogs.ErrorAsync(this, result.Message);
        }
    }

    private async Task LogoutAsync()
    {
        await _api.LogoutAsync();
        LogoutRequested?.Invoke();
    }
}
