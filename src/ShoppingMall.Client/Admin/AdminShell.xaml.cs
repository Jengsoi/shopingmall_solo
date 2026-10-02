namespace ShoppingMall.Client.Admin;

/// <summary>
/// 관리자 화면. 탭: 매출 현황(대시보드) / 재고 관리 / 주문 관리.
/// 재고관리(6000)·대시보드(6001) 서버에는 로그인한 관리자 계정으로 다시 인증하며 접속한다.
/// 서버 연결 2개를 직접 들고 있으므로 IDisposable 로 만들어, 로그아웃할 때 연결을 닫는다.
/// </summary>
public partial class AdminShell : UserControl, IDisposable
{
    private readonly NetworkClient _inventoryNet;
    private readonly NetworkClient _dashboardNet;
    private readonly InventoryView _inventory;
    private readonly OrderManageView _orders;

    public event Action? LogoutRequested;

    public AdminShell(Member member, string password)
    {
        InitializeComponent();
        HeaderText.Text = $"관리자 모드 ({member.Name})";

        // 두 관리자 서버에 각각 연결. 연결할 때마다 이 계정으로 자동 로그인한다.
        _inventoryNet = NetworkClient.ForAdmin(AppSettings.Host, AppSettings.InventoryPort, member.LoginId, password);
        _dashboardNet = NetworkClient.ForAdmin(AppSettings.Host, AppSettings.DashboardPort, member.LoginId, password);

        // 재고 관리와 주문 관리는 같은 재고관리 서버(6000) 연결을 함께 쓴다.
        var inventoryApi = new InventoryApi(_inventoryNet);
        _inventory = new InventoryView(inventoryApi);
        _orders = new OrderManageView(inventoryApi);

        DashboardTab.Content = new DashboardView(new DashboardApi(_dashboardNet));
        InventoryTab.Content = _inventory;
        OrdersTab.Content = _orders;
    }

    private void Logout_Click(object sender, RoutedEventArgs e) => LogoutRequested?.Invoke();

    /// <summary>
    /// 탭을 열 때마다 최신 데이터를 불러온다. 주문 취소로 바뀐 재고도 재고 관리 탭을 열면 반영된다.
    /// (탭 안의 표·콤보박스에서 올라오는 SelectionChanged 는 무시)
    /// </summary>
    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, Tabs)) return;

        if (Tabs.SelectedItem == InventoryTab)
            Ui.Fire(this, _inventory.LoadAllAsync);
        else if (Tabs.SelectedItem == OrdersTab)
            Ui.Fire(this, _orders.LoadAsync);
    }

    public void Dispose()
    {
        _inventoryNet.Dispose();
        _dashboardNet.Dispose();
    }
}
