namespace ShoppingMall.Client.Admin;

/// <summary>
/// 관리자 화면 (admin/admin_main.py). 탭: 매출 현황(대시보드) / 재고 관리 / 주문 관리.
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
        var inventoryApi = new InventoryApi(_inventoryNet);
        var inventory = new InventoryView(inventoryApi);
        var orders = new OrderManageView(inventoryApi);

        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = "📊 매출 현황 (대시보드)", Content = dashboard });
        tabs.Items.Add(new TabItem { Header = "📦 재고 관리", Content = inventory });
        tabs.Items.Add(new TabItem { Header = "🧾 주문 관리", Content = orders });

        // 탭을 열 때마다 최신 데이터를 불러온다. 주문 취소로 바뀐 재고도 재고 관리 탭을 열면 반영된다.
        // (탭 안의 표·ComboBox 에서 올라오는 SelectionChanged 는 무시)
        tabs.SelectionChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Source, tabs)) return;
            if (tabs.SelectedIndex == 1)
                Ui.Fire(this, inventory.LoadAllAsync);
            else if (tabs.SelectedIndex == 2)
                Ui.Fire(this, orders.LoadAsync);
        };

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
