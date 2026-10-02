namespace ShoppingMall.Client.Views;

/// <summary>
/// 회원가입 대화상자. 아이디 중복확인을 통과해야 가입할 수 있다.
/// 가입에 성공하면 DialogResult = true 로 창을 닫아 LoginView 에 결과를 알린다.
/// </summary>
public partial class SignupWindow : Window
{
    // 서버에 보내는 성별 코드. XAML 콤보박스 항목(선택 안 함 / 남성 / 여성)과 같은 순서(인덱스)끼리 짝이다.
    private static readonly string[] GenderCodes = { "", "M", "F" };

    private readonly ShopApi _api;
    private bool _idChecked; // 지금 입력된 아이디로 중복확인을 통과했는지

    public SignupWindow(ShopApi api)
    {
        InitializeComponent();
        _api = api;
    }

    public string LoginId => (LoginIdBox.Text ?? "").Trim();

    // 아이디를 바꾸면 중복확인을 다시 해야 한다.
    private void LoginIdBox_TextChanged(object sender, TextChangedEventArgs e) => _idChecked = false;

    private async void CheckId_Click(object sender, RoutedEventArgs e) => await Ui.Guard(this, CheckIdAsync);

    private async void Signup_Click(object sender, RoutedEventArgs e) => await Ui.Guard(this, SignupAsync);

    /// <summary>아이디 중복확인. 사용 가능하면 _idChecked = true.</summary>
    private async Task CheckIdAsync()
    {
        if (LoginId.Length == 0)
        {
            Dialogs.Info(this, "아이디를 입력하세요.");
            return;
        }

        var result = await _api.CheckIdAsync(LoginId);
        if (!result.Ok)
        {
            Dialogs.Error(this, result.Message);
            return;
        }

        _idChecked = result.Value;
        Dialogs.Info(this, result.Value ? "사용 가능한 아이디입니다." : "이미 사용 중인 아이디입니다.");
    }

    /// <summary>필수 항목·중복확인·비밀번호 확인을 검사한 뒤 서버에 가입 요청</summary>
    private async Task SignupAsync()
    {
        string password = PasswordBox.Password;
        string name = (NameBox.Text ?? "").Trim();

        if (LoginId.Length == 0 || password.Length == 0 || name.Length == 0)
        {
            Dialogs.Info(this, "아이디, 비밀번호, 이름은 필수입니다.");
            return;
        }
        if (!_idChecked)
        {
            Dialogs.Info(this, "아이디 중복확인을 먼저 해주세요.");
            return;
        }
        if (password != PasswordConfirmBox.Password)
        {
            Dialogs.Info(this, "비밀번호가 일치하지 않습니다.");
            return;
        }

        int genderIndex = Math.Max(0, GenderBox.SelectedIndex); // 선택이 없으면(-1) "선택 안 함"
        var result = await _api.SignupAsync(
            LoginId, password, name,
            (AddressBox.Text ?? "").Trim(), (EmailBox.Text ?? "").Trim(), (PhoneBox.Text ?? "").Trim(),
            GenderCodes[genderIndex]);

        if (result.Ok)
            DialogResult = true; // 창이 닫히고 ShowDialog() 가 true 를 돌려준다
        else
            Dialogs.Error(this, result.Message);
    }
}
