using ShoppingMall.Client.Admin;
using ShoppingMall.Client.Views;

namespace ShoppingMall.Client;

/// <summary>
/// 최상위 창. 로그인 화면 ↔ (일반 회원: 쇼핑 화면 / 관리자: 관리자 화면) 을 전환한다.
/// 서버 주소는 환경변수 SHOP_HOST 등으로 바꿀 수 있다(기본 127.0.0.1).
/// </summary>
public sealed class MainWindow : Window
{
    private readonly NetworkClient _net = new(AppSettings.Host, AppSettings.MallPort);
    private readonly ShopApi _api;
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

    private void ShowLogin()
    {
        Title = "쇼핑몰";
        var login = new LoginView(_api);
        login.LoginSucceeded += OnLoginSucceeded;
        _host.Content = login;
    }

    private void OnLoginSucceeded(Member member, string password)
    {
        if (member.IsAdmin)
        {
            Title = "통합 매장 관리 시스템";
            var admin = new AdminShell(member, password);
            admin.LogoutRequested += async () =>
            {
                await Ui.Guard(this, () => _api.LogoutAsync());
                admin.Dispose();
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
            Ui.Fire(shop, shop.InitializeAsync);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _net.Dispose();
        (_host.Content as IDisposable)?.Dispose();
        base.OnClosed(e);
    }
}
