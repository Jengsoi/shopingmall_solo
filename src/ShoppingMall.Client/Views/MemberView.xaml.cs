namespace ShoppingMall.Client.Views;

/// <summary>내 정보: 회원정보 조회·수정 / 회원탈퇴 / 로그아웃.</summary>
public partial class MemberView : UserControl
{
    // 서버 성별 코드. XAML 콤보박스 항목과 같은 인덱스끼리 짝이다. (SignupWindow 와 같음)
    private static readonly string[] GenderCodes = { "", "M", "F" };

    private readonly ShopApi _api;

    public event Action? LogoutRequested;

    public MemberView(ShopApi api)
    {
        InitializeComponent();
        _api = api;
        GenderBox.SelectionChanged += (_, e) => e.Handled = true; // 바깥 TabControl 까지 올라가지 않게
    }

    private async void Update_Click(object sender, RoutedEventArgs e) => await Ui.Guard(this, UpdateAsync);
    private async void Withdraw_Click(object sender, RoutedEventArgs e) => await Ui.Guard(this, WithdrawAsync);
    private async void Logout_Click(object sender, RoutedEventArgs e) => await Ui.Guard(this, LogoutAsync);

    /// <summary>서버에서 내 정보를 받아 입력칸을 채운다.</summary>
    public async Task LoadInfoAsync()
    {
        var result = await _api.MemberInfoAsync();
        if (!result.Ok)
        {
            Dialogs.Error(this, result.Message);
            return;
        }

        var info = result.Value;
        GreetingText.Text = $"{info.Name}님 환영합니다.";
        LoginIdText.Text = info.LoginId;
        NameBox.Text = info.Name;
        AddressBox.Text = info.Address;
        EmailBox.Text = info.Email;
        PhoneBox.Text = info.Phone;

        // 서버의 성별 코드("M" 등)를 콤보박스 위치로 바꾼다. 모르는 값이면 "선택 안 함".
        int index = Array.IndexOf(GenderCodes, info.Gender);
        GenderBox.SelectedIndex = index >= 0 ? index : 0;
    }

    /// <summary>입력칸의 값으로 회원정보를 수정하고, 성공하면 다시 불러와 화면을 맞춘다.</summary>
    private async Task UpdateAsync()
    {
        string name = (NameBox.Text ?? "").Trim();
        if (name.Length == 0)
        {
            Dialogs.Info(this, "이름을 입력하세요.");
            return;
        }

        var result = await _api.MemberUpdateAsync(
            name, (AddressBox.Text ?? "").Trim(), (EmailBox.Text ?? "").Trim(), (PhoneBox.Text ?? "").Trim(),
            GenderCodes[Math.Max(0, GenderBox.SelectedIndex)]);

        if (result.Ok)
        {
            Dialogs.Info(this, "회원정보가 수정되었습니다.");
            await LoadInfoAsync();
        }
        else
        {
            Dialogs.Error(this, result.Message);
        }
    }

    /// <summary>확인을 받은 뒤 탈퇴. 탈퇴하면 서버 세션이 끊기므로 로그인 화면으로 돌아간다.</summary>
    private async Task WithdrawAsync()
    {
        if (!Dialogs.Confirm(this, "정말 탈퇴하시겠습니까?", "회원탈퇴"))
            return;

        var result = await _api.MemberWithdrawAsync();
        if (result.Ok)
        {
            Dialogs.Info(this, "회원탈퇴가 완료되었습니다.");
            LogoutRequested?.Invoke();
        }
        else
        {
            Dialogs.Error(this, result.Message);
        }
    }

    private async Task LogoutAsync()
    {
        await _api.LogoutAsync();
        LogoutRequested?.Invoke();
    }
}
