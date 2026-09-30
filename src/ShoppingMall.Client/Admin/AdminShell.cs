namespace ShoppingMall.Client.Admin;

/// <summary>
/// 관리자 화면 (admin/admin_main.py). 탭: 매출 현황(대시보드) / 재고 관리.
/// 재고관리(6000)·대시보드(6001) 서버에는 로그인한 관리자 계정으로 다시 인증하며 접속한다.
/// </summary>
public sealed class AdminShell : UserControl, IDisposable
{
    private readonly NetworkClient _inventoryNet;
    private readonly NetworkClient _dashboardNet;

    public event Action? LogoutRequested;

    public AdminShell(Member member, string password)
    {
        _inventoryNet = NetworkClient.ForAdmin(AppSettings.Host, AppSettings.InventoryPort, member.LoginId, password);
        _dashboardNet = NetworkClient.ForAdmin(AppSettings.Host, AppSettings.DashboardPort, member.LoginId, password);

        var dashboard = new DashboardView(new DashboardApi(_dashboardNet));
        var inventory = new InventoryView(new InventoryApi(_inventoryNet));

        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = "📊 매출 현황 (대시보드)", Content = dashboard });
        tabs.Items.Add(new TabItem { Header = "📦 재고 관리", Content = inventory });

        var logout = Ui.Btn("로그아웃", () => { LogoutRequested?.Invoke(); return Task.CompletedTask; });
        var topBar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(12, 8) };
        var label = Ui.Text($"관리자 모드 ({member.Name})", 14, true);
        Grid.SetColumn(label, 0);
        Grid.SetColumn(logout, 1);
        topBar.Children.Add(label);
        topBar.Children.Add(logout);

        Content = Ui.Dock(topBar, tabs);

        // 재고관리 탭은 화면이 열릴 때 목록을 불러온다.
        AttachedToVisualTree += (_, _) => Ui.Fire(this, inventory.LoadAllAsync);
    }

    public void Dispose()
    {
        _inventoryNet.Dispose();
        _dashboardNet.Dispose();
    }
}
