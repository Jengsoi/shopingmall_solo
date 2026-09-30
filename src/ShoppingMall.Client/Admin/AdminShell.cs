namespace ShoppingMall.Client.Admin;

/// <summary>
/// 관리자 화면. 탭: 매출 현황(대시보드) / 재고 관리 / 주문 관리.
/// 재고관리(6000)·대시보드(6001) 서버에는 로그인한 관리자 계정으로 다시 인증하며 접속한다.
/// 서버 연결 2개를 직접 들고 있으므로 IDisposable 로 만들어, 로그아웃할 때 연결을 닫는다.
/// </summary>
public sealed class AdminShell : UserControl, IDisposable
{
    private readonly NetworkClient _inventoryNet;
    private readonly NetworkClient _dashboardNet;

    public event Action? LogoutRequested;

    public AdminShell(Member member, string password)
    {
        // 두 관리자 서버에 각각 연결. 연결할 때마다 이 계정으로 자동 로그인한다.
        _inventoryNet = NetworkClient.ForAdmin(AppSettings.Host, AppSettings.InventoryPort, member.LoginId, password);
        _dashboardNet = NetworkClient.ForAdmin(AppSettings.Host, AppSettings.DashboardPort, member.LoginId, password);

        var dashboard = new DashboardView(new DashboardApi(_dashboardNet));
        // 재고 관리와 주문 관리는 같은 재고관리 서버(6000) 연결을 함께 쓴다.
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

        // 맨 위 줄: 왼쪽에 "관리자 모드 (이름)", 오른쪽 끝에 로그아웃
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
