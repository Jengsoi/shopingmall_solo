namespace ShoppingMall.Client.Admin;

/// <summary>매출 현황: 기간을 고르면 총 매출, 상품 TOP5, 카테고리별 비중을 보여준다.</summary>
public partial class DashboardView : UserControl
{
    private readonly DashboardApi _api;

    public DashboardView(DashboardApi api)
    {
        InitializeComponent();
        _api = api;

        // 처음에는 시작일·종료일 모두 오늘
        StartPicker.SelectedDate = DateTime.Today;
        EndPicker.SelectedDate = DateTime.Today;
    }

    private async void Query_Click(object sender, RoutedEventArgs e) => await Ui.Guard(this, QueryAsync);

    /// <summary>기간을 확인하고 서버에서 통계를 받아 총액·차트를 갱신한다.</summary>
    private async Task QueryAsync()
    {
        DateTime start = StartPicker.SelectedDate ?? DateTime.Today;
        DateTime end = EndPicker.SelectedDate ?? DateTime.Today;

        if (end < start)
        {
            Dialogs.Info(this, "종료일이 시작일보다 빠릅니다.");
            return;
        }

        var result = await _api.QueryAsync(start, end);
        if (!result.Ok)
        {
            Dialogs.Error(this, result.Message);
            return;
        }

        TotalText.Text = $"총 매출액: {Fmt.Num(result.Value.TotalSales)} 원";
        Bar.SetData(result.Value.Top5);
        Donut.SetData(result.Value.Categories);
    }
}
