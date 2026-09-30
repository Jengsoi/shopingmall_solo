using ShoppingMall.Client.Admin;
using ShoppingMall.Client.Views;

namespace ShoppingMall.Client;

/// <summary>
/// 최상위 창. 로그인 화면 ↔ (일반 회원: 쇼핑 화면 / 관리자: 관리자 화면) 을 전환한다.
/// 창은 하나만 두고, 안에 들어가는 내용(_host.Content)만 바꿔 끼우는 방식이다.
/// 서버 주소는 환경변수 SHOP_HOST 등으로 바꿀 수 있다(기본 127.0.0.1).
/// </summary>
public sealed class MainWindow : Window
{
    // 쇼핑몰 서버(5000) 연결. 로그인도 이 연결로 하므로 로그인 화면·쇼핑 화면이 함께 쓴다.
    private readonly NetworkClient _net = new(AppSettings.Host, AppSettings.MallPort);
    private readonly ShopApi _api;

    // 현재 화면을 담는 자리. 여기에 LoginView / ShopShell / AdminShell 중 하나를 넣는다.
    private readonly ContentControl _host = new();

    public MainWindow()
    {
        _api = new ShopApi(_net);

        Title = "쇼핑몰";
        Width = 950;
        Height = 750;
        MinWidth = 700;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        Content = _host;
        ShowLogin();
    }

    /// <summary>로그인 화면으로 전환 (프로그램 시작, 로그아웃, 회원탈퇴 후)</summary>
    private void ShowLogin()
    {
        Title = "쇼핑몰";
        var login = new LoginView(_api);
        login.LoginSucceeded += OnLoginSucceeded;
        _host.Content = login;
    }

    /// <summary>
    /// 로그인 성공 → 권한에 따라 화면을 고른다.
    /// 관리자 화면은 재고관리·대시보드 서버에 다시 로그인해야 해서 비밀번호도 함께 넘긴다.
    /// </summary>
    private void OnLoginSucceeded(Member member, string password)
    {
        if (member.IsAdmin)
        {
            Title = "통합 매장 관리 시스템";
            var admin = new AdminShell(member, password);
            admin.LogoutRequested += async () =>
            {
                await Ui.Guard(this, () => _api.LogoutAsync());
                admin.Dispose(); // 관리자 서버 연결 2개를 닫는다
                ShowLogin();
            };
            _host.Content = admin;
        }
        else
        {
            Title = "쇼핑몰";
            var shop = new ShopShell(_api, member);
            shop.LogoutRequested += ShowLogin;
            _host.Content = shop;
            Ui.Fire(shop, shop.InitializeAsync); // 각 탭의 데이터를 서버에서 불러온다
        }
    }

    /// <summary>창을 닫을 때 서버 연결을 정리한다.</summary>
    protected override void OnClosed(EventArgs e)
    {
        _net.Dispose();
        (_host.Content as IDisposable)?.Dispose(); // 관리자 화면이면 그 연결들도
        base.OnClosed(e);
    }
}
