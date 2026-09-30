namespace ShoppingMall.Client.Views;

/// <summary>내 정보: 회원정보 조회·수정 / 회원탈퇴 / 로그아웃. 아이디는 바꿀 수 없어서 글자로만 보여준다.</summary>
public sealed class MemberView : UserControl
{
    // 화면 문구와 서버 코드. 같은 인덱스끼리 짝이다. (SignupWindow 와 같음)
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

    /// <summary>서버에서 내 정보를 받아 입력칸을 채운다.</summary>
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

        // 서버의 성별 코드("M" 등)를 콤보박스 위치로 바꾼다. 모르는 값이면 "선택 안 함".
        int index = Array.IndexOf(GenderCodes, info.Gender);
        _gender.SelectedIndex = index >= 0 ? index : 0;
    }

    /// <summary>입력칸의 값으로 회원정보를 수정하고, 성공하면 다시 불러와 화면을 맞춘다.</summary>
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

    /// <summary>확인을 받은 뒤 탈퇴. 탈퇴하면 서버 세션이 끊기므로 로그인 화면으로 돌아간다.</summary>
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
