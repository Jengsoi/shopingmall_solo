namespace ShoppingMall.Client.Admin;

/// <summary>
/// 매출 현황: 기간을 고르면 총 매출, 상품 TOP5, 카테고리별 비중을 보여준다.
/// 위: 날짜 선택 + 조회 / 가운데 큰 글씨: 총 매출액 / 아래: 막대 차트와 도넛 차트를 반씩 나란히.
/// </summary>
public sealed class DashboardView : UserControl
{
    private readonly DashboardApi _api;
    // 달력에서 날짜를 고르는 칸. 처음에는 둘 다 오늘.
    private readonly CalendarDatePicker _start = new() { SelectedDate = DateTime.Today };
    private readonly CalendarDatePicker _end = new() { SelectedDate = DateTime.Today };
    private readonly TextBlock _total = new()
    {
        Text = "총 매출액: 날짜를 조회하세요.",
        FontSize = 32,
        FontWeight = FontWeight.Bold,
        HorizontalAlignment = HorizontalAlignment.Center,
        TextAlignment = TextAlignment.Center,
    };
    private readonly BarChart _bar = new();
    private readonly DonutChart _donut = new();

    public DashboardView(DashboardApi api)
    {
        _api = api;

        var dateRow = Ui.HStack(8, _start, Ui.Text(" ~ "), _end, Ui.Btn("조회", QueryAsync));
        dateRow.HorizontalAlignment = HorizontalAlignment.Right;

        var totalBox = new Border
        {
            BorderBrush = new SolidColorBrush(Color.Parse("#DCDCDC")),
            BorderThickness = new Thickness(2),
            Padding = new Thickness(15),
            Margin = new Thickness(0, 8),
            Child = _total,
        };

        var top = Ui.VStack(8, dateRow, totalBox);

        // 차트 두 개를 1:1 폭으로 나란히
        var charts = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 12 };
        Grid.SetColumn(_bar, 0);
        Grid.SetColumn(_donut, 1);
        charts.Children.Add(_bar);
        charts.Children.Add(_donut);

        Content = new Border { Padding = new Thickness(12), Child = Ui.Dock(top, charts) };
    }

    /// <summary>기간을 확인하고 서버에서 통계를 받아 총액·차트를 갱신한다.</summary>
    private async Task QueryAsync()
    {
        DateTime start = _start.SelectedDate ?? DateTime.Today;
        DateTime end = _end.SelectedDate ?? DateTime.Today;

        if (end < start)
        {
            await Dialogs.InfoAsync(this, "종료일이 시작일보다 빠릅니다.");
            return;
        }

        var result = await _api.QueryAsync(start, end);
        if (!result.Ok)
        {
            await Dialogs.ErrorAsync(this, result.Message);
            return;
        }

        _total.Text = $"총 매출액: {Fmt.Num(result.Value.TotalSales)} 원";
        _bar.SetData(result.Value.Top5);
        _donut.SetData(result.Value.Categories);
    }
}
